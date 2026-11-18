import { beforeEach, describe, expect, it, vi } from 'vitest'
import { encolar, pendientes, rechazadas, sincronizar, vaciar } from './colaOffline'

// RQ_21 · CU-03 flujo alterno 6A: guardar en el dispositivo y sincronizar al volver la señal.

const respuesta = (status: number, cuerpo: unknown = {}) =>
  new Response(JSON.stringify(cuerpo), { status, headers: { 'Content-Type': 'application/json' } })

const operacion = (clave: string, descripcion = 'Seguimiento semana 2') => ({
  clave,
  metodo: 'PUT' as const,
  ruta: `/api/acompanamientos/a1/seguimientos/${clave}`,
  cuerpo: { avance: 40 },
  descripcion,
})

describe('cola sin conexión', () => {
  beforeEach(async () => {
    await vaciar()
  })

  it('guarda las operaciones en orden de llegada', async () => {
    await encolar(operacion('1'))
    await encolar(operacion('2'))

    const cola = await pendientes()
    expect(cola.map((o) => o.clave)).toEqual(['1', '2'])
  })

  it('una nueva versión de la misma operación reemplaza a la pendiente', async () => {
    await encolar(operacion('2', 'primera'))
    await encolar(operacion('2', 'corregida'))

    const cola = await pendientes()
    expect(cola).toHaveLength(1)
    expect(cola[0]?.descripcion).toBe('corregida')
  })

  it('al sincronizar envía todo y vacía la cola', async () => {
    await encolar(operacion('1'))
    await encolar(operacion('2'))
    const enviar = vi.fn().mockResolvedValue(respuesta(200))

    const resultado = await sincronizar(enviar)

    expect(enviar).toHaveBeenCalledTimes(2)
    expect(resultado.enviadas).toBe(2)
    expect(await pendientes()).toHaveLength(0)
  })

  it('si se cae la red se detiene y conserva lo que falta', async () => {
    await encolar(operacion('1'))
    await encolar(operacion('2'))
    const enviar = vi.fn().mockResolvedValueOnce(respuesta(200)).mockRejectedValueOnce(new TypeError('Failed to fetch'))

    const resultado = await sincronizar(enviar)

    expect(resultado.detenida).toBe(true)
    expect((await pendientes()).map((o) => o.clave)).toEqual(['2'])
  })

  it('un error del servidor (5xx) o una sesión vencida (401) no descartan el registro', async () => {
    await encolar(operacion('1'))

    await sincronizar(vi.fn().mockResolvedValue(respuesta(503)))
    await sincronizar(vi.fn().mockResolvedValue(respuesta(401)))

    expect(await pendientes()).toHaveLength(1)
  })

  it('un rechazo de negocio (4xx) sale de la cola y queda visible para el tutor', async () => {
    await encolar(operacion('1'))
    const enviar = vi.fn().mockResolvedValue(respuesta(400, { codigo: 'seguimiento_invalido', detail: 'La semana debe estar entre 1 y 5.' }))

    const resultado = await sincronizar(enviar)

    expect(resultado.rechazadas).toBe(1)
    expect(await pendientes()).toHaveLength(0)
    const lista = await rechazadas()
    expect(lista[0]?.error).toBe('La semana debe estar entre 1 y 5.')
  })
})
