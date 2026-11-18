import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { Rutas } from '../App'
import { aCsv } from '../lib/formato'
import { iniciarSesionComo, pintar, simularApi } from '../test/utilidades'

// CU-06 pasos 4-5: el administrador consulta la trazabilidad.

const registros = [
  { id: 2, usuario: 'tutor.caribe', operacion: 'estrategia.crear', recurso: 'estrategia', recursoId: '7', detalle: 'Club; "lectura"', fecha: '2026-11-18T15:00:00Z' },
  { id: 1, usuario: 'tutor.caribe', operacion: 'auth.ingreso', recurso: 'usuario', recursoId: '1', detalle: null, fecha: '2026-11-18T14:58:00Z' },
]

describe('aCsv', () => {
  it('escapa comillas y separadores', () => {
    const csv = aCsv([registros[0]!])
    expect(csv.split('\r\n')[1]).toContain('"Club; ""lectura"""')
  })
})

describe('Trazabilidad', () => {
  beforeEach(() => iniciarSesionComo('Administrador'))
  afterEach(() => vi.restoreAllMocks())

  it('lista las operaciones con usuario y fecha', async () => {
    simularApi({ 'GET /api/auditoria': { cuerpo: registros } })
    pintar(<Rutas />, { ruta: '/trazabilidad' })
    expect(await screen.findAllByText('tutor.caribe')).toHaveLength(2)
    expect(screen.getByRole('cell', { name: /creó una estrategia/i })).toBeInTheDocument()
  })

  it('envía los filtros al API', async () => {
    const llamadas = simularApi({ 'GET /api/auditoria': { cuerpo: registros } })
    pintar(<Rutas />, { ruta: '/trazabilidad' })
    await screen.findAllByText('tutor.caribe')

    await userEvent.type(screen.getByLabelText(/usuario/i), 'tutor.caribe')
    await userEvent.selectOptions(screen.getByLabelText(/operación/i), 'auth.bloqueo')
    await userEvent.click(screen.getByRole('button', { name: /aplicar filtros/i }))

    await waitFor(() => {
      const ultima = llamadas.at(-1)!.url.searchParams
      expect(ultima.get('usuario')).toBe('tutor.caribe')
      expect(ultima.get('operacion')).toBe('auth.bloqueo')
    })
  })

  it('flujo alterno 5A: sin resultados lo dice claramente', async () => {
    simularApi({ 'GET /api/auditoria': { cuerpo: [] } })
    pintar(<Rutas />, { ruta: '/trazabilidad' })
    expect(await screen.findByText(/no hay registros con esos filtros/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /descargar csv/i })).toBeDisabled()
  })
})
