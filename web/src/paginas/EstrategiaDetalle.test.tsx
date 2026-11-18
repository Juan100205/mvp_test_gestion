import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { Rutas } from '../App'
import { enmascararDocumento } from '../lib/formato'
import { ejemplo, iniciarSesionComo, pintar, simularApi } from '../test/utilidades'

// CU-02: asociar estudiantes precargados a la estrategia.

const estudiante = { id: 1, tipoDocumento: 'TI', numeroDocumento: '1100000001', nombre: 'Valentina Rodríguez Ospina', vinculadoEn: null }

describe('enmascararDocumento (Ley 1581)', () => {
  it('muestra solo los últimos cuatro dígitos', () => {
    expect(enmascararDocumento('TI', '1100000001')).toBe('TI ••••0001')
  })
})

describe('Detalle de la estrategia', () => {
  beforeEach(() => iniciarSesionComo('Tutor'))
  afterEach(() => vi.restoreAllMocks())

  it('busca un estudiante por documento y lo vincula; la estrategia queda solo para consulta', async () => {
    const llamadas = simularApi({
      'GET /api/estrategias/7': { cuerpo: ejemplo.estrategia() },
      'GET /api/estudiantes': { cuerpo: estudiante },
      'POST /api/estrategias/7/estudiantes': {
        status: 201,
        cuerpo: ejemplo.estrategia({
          estado: 'SoloConsulta',
          esEditable: false,
          estudiantes: [{ ...estudiante, vinculadoEn: '2026-11-18T15:00:00Z' }],
        }),
      },
    })
    pintar(<Rutas />, { ruta: '/estrategias/7' })

    await userEvent.type(await screen.findByLabelText(/documento del estudiante/i), '1100000001')
    await userEvent.click(screen.getByRole('button', { name: /buscar/i }))
    await userEvent.click(await screen.findByRole('button', { name: /vincular a la estrategia/i }))

    expect(await screen.findByText('Solo consulta')).toBeInTheDocument()
    expect(screen.getAllByText('TI ••••0001').length).toBeGreaterThan(0)
    expect(screen.queryByText('1100000001')).not.toBeInTheDocument()
    expect(llamadas.find((l) => l.metodo === 'POST')?.cuerpo).toEqual({ estudianteId: 1 })
  })

  it('flujo alterno: el documento no está en la base precargada', async () => {
    simularApi({
      'GET /api/estrategias/7': { cuerpo: ejemplo.estrategia() },
      'GET /api/estudiantes': {
        status: 404,
        cuerpo: { codigo: 'estudiante_no_encontrado', detail: 'No hay un estudiante precargado con ese documento.' },
      },
    })
    pintar(<Rutas />, { ruta: '/estrategias/7' })

    await userEvent.type(await screen.findByLabelText(/documento del estudiante/i), '999')
    await userEvent.click(screen.getByRole('button', { name: /buscar/i }))

    expect(await screen.findByText(/no hay un estudiante precargado/i)).toBeInTheDocument()
  })

  it('flujo alterno: el estudiante ya estaba vinculado', async () => {
    simularApi({
      'GET /api/estrategias/7': { cuerpo: ejemplo.estrategia() },
      'GET /api/estudiantes': { cuerpo: estudiante },
      'POST /api/estrategias/7/estudiantes': {
        status: 409,
        cuerpo: { codigo: 'estudiante_ya_vinculado', detail: 'Valentina ya está vinculado a esta estrategia.' },
      },
    })
    pintar(<Rutas />, { ruta: '/estrategias/7' })

    await userEvent.type(await screen.findByLabelText(/documento del estudiante/i), '1100000001')
    await userEvent.click(screen.getByRole('button', { name: /buscar/i }))
    await userEvent.click(await screen.findByRole('button', { name: /vincular a la estrategia/i }))

    expect(await screen.findByText(/ya está vinculado/i)).toBeInTheDocument()
  })
})
