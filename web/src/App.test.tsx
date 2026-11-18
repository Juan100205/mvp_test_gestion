import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { Rutas } from './App'
import { iniciarSesionComo, pintar, simularApi } from './test/utilidades'

// CU-06: cada perfil ve solo sus opciones.

describe('Rutas por perfil', () => {
  afterEach(() => vi.restoreAllMocks())

  it('sin sesión lleva al ingreso', async () => {
    simularApi({})
    pintar(<Rutas />, { ruta: '/estrategias' })
    expect(await screen.findByRole('heading', { name: /^ingresar$/i })).toBeInTheDocument()
  })

  it('el tutor entra a sus estrategias', async () => {
    iniciarSesionComo('Tutor')
    simularApi({ 'GET /api/estrategias': { cuerpo: [] } })
    pintar(<Rutas />, { ruta: '/' })
    expect(await screen.findByRole('heading', { name: /tus estrategias/i })).toBeInTheDocument()
  })

  it('el administrador no puede abrir las estrategias: va a la trazabilidad', async () => {
    iniciarSesionComo('Administrador')
    simularApi({ 'GET /api/auditoria': { cuerpo: [] } })
    pintar(<Rutas />, { ruta: '/estrategias' })
    expect(await screen.findByRole('heading', { name: /trazabilidad/i })).toBeInTheDocument()
  })

  it('el ente de control ve el aviso del módulo del Sprint 4', async () => {
    iniciarSesionComo('EnteControl')
    simularApi({})
    pintar(<Rutas />, { ruta: '/estrategias' })
    expect(await screen.findByText(/sprint 4/i)).toBeInTheDocument()
  })
})
