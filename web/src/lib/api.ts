import type { Sesion } from './tipos'

const CLAVE_SESION = 'sses.sesion'

interface CuerpoError {
  codigo?: string
  detail?: string
  campos?: string[]
  intentosRestantes?: number
  bloqueadoHasta?: string
}

export class ApiError extends Error {
  readonly status: number
  readonly codigo: string
  readonly campos: string[]
  readonly datos: CuerpoError

  constructor(status: number, datos: CuerpoError) {
    super(datos.detail ?? `Error ${status}`)
    this.status = status
    this.codigo = datos.codigo ?? 'desconocido'
    this.campos = datos.campos ?? []
    this.datos = datos
  }
}

export const esErrorDeRed = (error: unknown) => error instanceof TypeError

export function mensajeDeError(error: unknown): string {
  if (error instanceof ApiError) return error.message
  if (esErrorDeRed(error)) return 'No hay conexión. Revisa la señal e inténtalo de nuevo.'
  return 'Hay un problema con el servicio. Inténtalo más tarde.'
}

export function leerSesion(): Sesion | null {
  try {
    const crudo = localStorage.getItem(CLAVE_SESION)
    if (!crudo) return null
    const sesion = JSON.parse(crudo) as Sesion
    return new Date(sesion.expira) > new Date() ? sesion : null
  } catch {
    return null
  }
}

export function guardarSesion(sesion: Sesion | null) {
  try {
    if (sesion) localStorage.setItem(CLAVE_SESION, JSON.stringify(sesion))
    else localStorage.removeItem(CLAVE_SESION)
  } catch {
    // Almacenamiento bloqueado: la sesión vive solo en memoria.
  }
}

type AlExpirar = () => void
let alExpirar: AlExpirar = () => {}
export const configurarExpiracion = (fn: AlExpirar) => {
  alExpirar = fn
}

/** fetch con el token de la sesión; no lanza: devuelve la respuesta tal cual (lo usa la cola sin conexión). */
export function enviar(metodo: string, ruta: string, cuerpo?: unknown): Promise<Response> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (cuerpo !== undefined) headers['Content-Type'] = 'application/json'
  const token = leerSesion()?.token
  if (token) headers.Authorization = `Bearer ${token}`
  return fetch(ruta, { method: metodo, headers, body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo) })
}

export async function api<T>(ruta: string, opciones: { metodo?: string; cuerpo?: unknown } = {}): Promise<T> {
  const respuesta = await enviar(opciones.metodo ?? 'GET', ruta, opciones.cuerpo)
  const texto = await respuesta.text()
  const datos = texto ? JSON.parse(texto) : undefined

  if (!respuesta.ok) {
    if (respuesta.status === 401 && !ruta.endsWith('/auth/login')) alExpirar()
    throw new ApiError(respuesta.status, (datos as CuerpoError) ?? {})
  }
  return datos as T
}
