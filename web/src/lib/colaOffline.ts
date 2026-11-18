import { createStore, del, get, set, values } from 'idb-keyval'

/**
 * RQ_21 · CU-03 flujo alterno 6A. Los registros hechos sin señal se guardan en IndexedDB
 * y se envían en orden cuando vuelve la conexión. Los Id los genera el cliente, así que
 * reenviar no duplica nada en el servidor.
 */
export interface Operacion {
  clave: string
  metodo: 'POST' | 'PUT'
  ruta: string
  cuerpo: unknown
  descripcion: string
  creadaEn: number
}

export interface OperacionRechazada extends Operacion {
  error: string
  rechazadaEn: number
}

export interface ResultadoSincronizacion {
  enviadas: number
  rechazadas: number
  detenida: boolean
}

const almacen = createStore('sses-sin-conexion', 'cola')
const CLAVE_RECHAZADAS = '__rechazadas__'
const oyentes = new Set<() => void>()
let secuencia = 0

const avisar = () => oyentes.forEach((fn) => fn())

export function suscribir(fn: () => void) {
  oyentes.add(fn)
  return () => {
    oyentes.delete(fn)
  }
}

export async function encolar(op: Omit<Operacion, 'creadaEn'>) {
  // Date.now() + secuencia: conserva el orden aunque dos registros caigan en el mismo milisegundo.
  await set(op.clave, { ...op, creadaEn: Date.now() + secuencia++ / 1000 }, almacen)
  avisar()
}

export async function pendientes(): Promise<Operacion[]> {
  const todas = (await values<Operacion | OperacionRechazada[]>(almacen)).filter(
    (v): v is Operacion => !Array.isArray(v),
  )
  return todas.sort((a, b) => a.creadaEn - b.creadaEn)
}

export async function rechazadas(): Promise<OperacionRechazada[]> {
  return (await get<OperacionRechazada[]>(CLAVE_RECHAZADAS, almacen)) ?? []
}

export async function descartarRechazadas() {
  await del(CLAVE_RECHAZADAS, almacen)
  avisar()
}

export async function vaciar() {
  for (const op of await pendientes()) await del(op.clave, almacen)
  await del(CLAVE_RECHAZADAS, almacen)
}

let enCurso: Promise<ResultadoSincronizacion> | null = null

export function sincronizar(enviar: (op: Operacion) => Promise<Response>): Promise<ResultadoSincronizacion> {
  // Un solo envío a la vez: el evento "online" y el arranque pueden coincidir.
  enCurso ??= procesar(enviar).finally(() => {
    enCurso = null
  })
  return enCurso
}

async function procesar(enviar: (op: Operacion) => Promise<Response>): Promise<ResultadoSincronizacion> {
  const resultado: ResultadoSincronizacion = { enviadas: 0, rechazadas: 0, detenida: false }

  for (const op of await pendientes()) {
    let respuesta: Response
    try {
      respuesta = await enviar(op)
    } catch {
      resultado.detenida = true // sin red: se reintenta en la próxima conexión
      break
    }

    if (respuesta.ok) {
      await del(op.clave, almacen)
      resultado.enviadas++
      continue
    }

    if (respuesta.status === 401 || respuesta.status === 408 || respuesta.status === 429 || respuesta.status >= 500) {
      resultado.detenida = true // sesión vencida o servidor ocupado: no se pierde el registro
      break
    }

    const detalle = await respuesta
      .json()
      .then((d: { detail?: string }) => d.detail)
      .catch(() => undefined)
    const lista = await rechazadas()
    lista.push({ ...op, error: detalle ?? `Rechazado (${respuesta.status})`, rechazadaEn: Date.now() })
    await set(CLAVE_RECHAZADAS, lista, almacen)
    await del(op.clave, almacen)
    resultado.rechazadas++
  }

  avisar()
  return resultado
}
