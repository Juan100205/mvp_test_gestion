import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { Rutas } from '../App'
import { pendientes, vaciar } from '../lib/colaOffline'
import { ejemplo, iniciarSesionComo, pintar, simularApi, simularSinSenal } from '../test/utilidades'

// CU-03: acompañamiento pedagógico mensual con seguimiento semanal.

const base = (acompanamientos: unknown[] = [ejemplo.acompanamiento()]) => ({
  'GET /api/estrategias/7': { cuerpo: ejemplo.estrategia() },
  'GET /api/estrategias/7/acompanamientos': { cuerpo: acompanamientos },
})

describe('Acompañamientos', () => {
  beforeEach(() => {
    iniciarSesionComo('Tutor')
    vi.useFakeTimers({ toFake: ['Date'], now: new Date(2026, 10, 18, 10) })
  })
  afterEach(async () => {
    vi.useRealTimers()
    vi.restoreAllMocks()
    await vaciar()
  })

  it('muestra los acompañamientos del mes con su ciclo de semanas', async () => {
    simularApi(base())
    pintar(<Rutas />, { ruta: '/estrategias/7/acompanamientos' })

    expect(await screen.findByText(/mejorar la planeación de clase/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /semana 2: 40 %/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /semana 3/i })).toHaveAttribute('aria-current', 'date')
  })

  it('registra un acompañamiento individual con un Id generado en el dispositivo', async () => {
    const llamadas = simularApi({
      ...base([]),
      'POST /api/acompanamientos': (cuerpo) => ({
        status: 201,
        cuerpo: ejemplo.acompanamiento({ id: (cuerpo as { id: string }).id, seguimientos: [], avanceActual: 0 }),
      }),
    })
    pintar(<Rutas />, { ruta: '/estrategias/7/acompanamientos' })

    await userEvent.click(await screen.findByRole('button', { name: /registrar acompañamiento/i }))
    const hoja = screen.getByRole('dialog')
    await userEvent.click(within(hoja).getByRole('radio', { name: /marta lucía ruiz/i }))
    await userEvent.type(within(hoja).getByLabelText(/objetivo/i), 'Mejorar la planeación de clase')
    await userEvent.click(within(hoja).getByRole('button', { name: /guardar acompañamiento/i }))

    await waitFor(() => expect(llamadas.some((l) => l.metodo === 'POST')).toBe(true))
    const post = llamadas.find((l) => l.metodo === 'POST')!
    expect(post.cuerpo).toMatchObject({ estrategiaId: 7, tipo: 'Individual', periodo: '2026-11', docenteIds: [1] })
    expect((post.cuerpo as { id: string }).id).toMatch(/^[0-9a-f-]{36}$/)
  })

  it('el grupal exige al menos dos docentes', async () => {
    simularApi(base([]))
    pintar(<Rutas />, { ruta: '/estrategias/7/acompanamientos' })

    await userEvent.click(await screen.findByRole('button', { name: /registrar acompañamiento/i }))
    const hoja = screen.getByRole('dialog')
    await userEvent.click(within(hoja).getByRole('radio', { name: /grupal/i }))
    await userEvent.click(within(hoja).getByRole('checkbox', { name: /marta lucía ruiz/i }))
    await userEvent.type(within(hoja).getByLabelText(/objetivo/i), 'Objetivo')
    await userEvent.click(within(hoja).getByRole('button', { name: /guardar acompañamiento/i }))

    expect(within(hoja).getByText(/al menos dos docentes/i)).toBeInTheDocument()
  })

  it('flujo alterno 4A: si ya existe en el mes, avisa y muestra el existente', async () => {
    simularApi({
      ...base([]),
      'POST /api/acompanamientos': { status: 200, cuerpo: ejemplo.acompanamiento({ existente: true }) },
    })
    pintar(<Rutas />, { ruta: '/estrategias/7/acompanamientos' })

    await userEvent.click(await screen.findByRole('button', { name: /registrar acompañamiento/i }))
    const hoja = screen.getByRole('dialog')
    await userEvent.click(within(hoja).getByRole('radio', { name: /marta lucía ruiz/i }))
    await userEvent.type(within(hoja).getByLabelText(/objetivo/i), 'Otro objetivo')
    await userEvent.click(within(hoja).getByRole('button', { name: /guardar acompañamiento/i }))

    expect(await screen.findByText(/ya tiene un acompañamiento en noviembre/i)).toBeInTheDocument()
    expect(screen.getByText(/mejorar la planeación de clase/i)).toBeInTheDocument()
  })

  it('registra el seguimiento de la semana', async () => {
    const llamadas = simularApi({
      ...base(),
      'PUT /api/acompanamientos/a1/seguimientos/3': { cuerpo: ejemplo.acompanamiento() },
    })
    pintar(<Rutas />, { ruta: '/estrategias/7/acompanamientos' })

    await userEvent.click(await screen.findByRole('button', { name: /semana 3/i }))
    const hoja = screen.getByRole('dialog')
    await userEvent.type(within(hoja).getByLabelText(/actividades/i), 'Taller de evaluación')
    await userEvent.click(within(hoja).getByRole('button', { name: /guardar seguimiento/i }))

    await waitFor(() => expect(llamadas.some((l) => l.metodo === 'PUT')).toBe(true))
    expect(llamadas.find((l) => l.metodo === 'PUT')!.cuerpo).toMatchObject({ actividades: 'Taller de evaluación', avance: 40 })
  })

  it('RQ_21 · 6A: sin señal guarda el seguimiento en el dispositivo y lo marca pendiente', async () => {
    simularApi(base())
    pintar(<Rutas />, { ruta: '/estrategias/7/acompanamientos' })
    await screen.findByText(/mejorar la planeación de clase/i)
    const restaurar = simularSinSenal()

    try {
      await userEvent.click(screen.getByRole('button', { name: /semana 3/i }))
      const hoja = screen.getByRole('dialog')
      await userEvent.type(within(hoja).getByLabelText(/actividades/i), 'Visita en zona rural')
      await userEvent.click(within(hoja).getByRole('button', { name: /guardar seguimiento/i }))

      expect(await screen.findByRole('button', { name: /semana 3: 40 %, pendiente de enviar/i })).toBeInTheDocument()
      expect(await pendientes()).toHaveLength(1)
      expect((await pendientes())[0]!.clave).toBe('seg:a1:3')
    } finally {
      restaurar()
    }
  })

  it('HU-07: el filtro por docente muestra su historial de todos los meses', async () => {
    simularApi(
      base([
        ejemplo.acompanamiento(),
        ejemplo.acompanamiento({ id: 'a0', periodo: '2026-10', objetivo: 'Gestión de aula' }),
        ejemplo.acompanamiento({ id: 'a2', objetivo: 'Otro docente', docentes: [ejemplo.docentes[1]] }),
      ]),
    )
    pintar(<Rutas />, { ruta: '/estrategias/7/acompanamientos' })
    await screen.findByText(/mejorar la planeación de clase/i)
    expect(screen.queryByText(/gestión de aula/i)).not.toBeInTheDocument()

    await userEvent.selectOptions(screen.getByLabelText(/docente/i), '1')

    expect(screen.getByText(/gestión de aula/i)).toBeInTheDocument()
    expect(screen.queryByText(/otro docente/i)).not.toBeInTheDocument()
  })
})
