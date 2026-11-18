import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { Rutas } from '../App'
import { ejemplo, iniciarSesionComo, pintar, simularApi } from '../test/utilidades'

// CU-01: registrar y editar estrategias estudiantiles.

const resumen = {
  id: 7,
  nombre: 'Club de lectura en voz alta',
  periodo: '2026-2',
  entidad: 'IE Técnica Simón Bolívar',
  region: 'Caribe',
  estado: 'SoloConsulta',
  totalEstudiantes: 12,
  totalDocentes: 2,
  totalAcompanamientos: 3,
  actualizadaEn: '2026-11-10T10:00:00Z',
}

async function elegirEntidad() {
  await screen.findByRole('option', { name: /simón bolívar/i })
  await userEvent.selectOptions(screen.getByLabelText(/entidad/i), '1')
}

const catalogos = {
  'GET /api/entidades': { cuerpo: ejemplo.entidades },
  'GET /api/entidades/1/docentes': { cuerpo: ejemplo.docentes },
}

describe('Listado de estrategias', () => {
  beforeEach(() => iniciarSesionComo('Tutor'))
  afterEach(() => vi.restoreAllMocks())

  it('sin estrategias invita a crear la primera', async () => {
    simularApi({ 'GET /api/estrategias': { cuerpo: [] } })
    pintar(<Rutas />, { ruta: '/estrategias' })
    expect(await screen.findByText(/no tienes estrategias registradas/i)).toBeInTheDocument()
  })

  it('muestra cada estrategia con sus contadores y su estado', async () => {
    simularApi({ 'GET /api/estrategias': { cuerpo: [resumen] } })
    pintar(<Rutas />, { ruta: '/estrategias' })

    const fila = (await screen.findByRole('link', { name: /club de lectura/i })).closest('li')!
    expect(within(fila).getByText(/12 estudiantes/i)).toBeInTheDocument()
    expect(within(fila).getByText('Solo consulta')).toBeInTheDocument()
  })
})

describe('Formulario de estrategia', () => {
  beforeEach(() => iniciarSesionComo('Tutor'))
  afterEach(() => vi.restoreAllMocks())

  it('agrupa las entidades por región', async () => {
    simularApi(catalogos)
    pintar(<Rutas />, { ruta: '/estrategias/nueva' })
    const selector = await screen.findByLabelText(/entidad/i)
    expect(await within(selector).findByRole('group', { name: 'Pacífico' })).toBeInTheDocument()
  })

  it('flujo alterno 2A: no envía si faltan datos y señala cada campo', async () => {
    const llamadas = simularApi(catalogos)
    pintar(<Rutas />, { ruta: '/estrategias/nueva' })

    await userEvent.click(await screen.findByRole('button', { name: /guardar estrategia/i }))

    expect(screen.getByLabelText(/nombre/i)).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByText(/elige al menos un docente/i)).toBeInTheDocument()
    expect(llamadas.some((l) => l.metodo === 'POST')).toBe(false)
  })

  it('crea la estrategia con los docentes de la entidad y abre su detalle', async () => {
    const llamadas = simularApi({
      ...catalogos,
      'POST /api/estrategias': { status: 201, cuerpo: ejemplo.estrategia() },
      'GET /api/estrategias/7': { cuerpo: ejemplo.estrategia() },
    })
    pintar(<Rutas />, { ruta: '/estrategias/nueva' })

    await elegirEntidad()
    await userEvent.type(screen.getByLabelText(/nombre/i), 'Club de lectura en voz alta')
    await userEvent.click(await screen.findByRole('checkbox', { name: /marta lucía ruiz/i }))
    await userEvent.click(screen.getByRole('button', { name: /guardar estrategia/i }))

    expect(await screen.findByRole('heading', { name: /club de lectura/i, level: 1 })).toBeInTheDocument()
    const post = llamadas.find((l) => l.metodo === 'POST')!
    expect(post.cuerpo).toMatchObject({ entidadId: 1, nombre: 'Club de lectura en voz alta', docenteIds: [1] })
  })

  it('muestra el error que devuelve el API en el campo correspondiente', async () => {
    simularApi({
      ...catalogos,
      'POST /api/estrategias': {
        status: 400,
        cuerpo: { codigo: 'docente_de_otra_entidad', detail: 'Solo puedes asociar docentes de la entidad.', campos: ['docentes'] },
      },
    })
    pintar(<Rutas />, { ruta: '/estrategias/nueva' })

    await elegirEntidad()
    await userEvent.type(screen.getByLabelText(/nombre/i), 'Club')
    await userEvent.click(await screen.findByRole('checkbox', { name: /marta lucía ruiz/i }))
    await userEvent.click(screen.getByRole('button', { name: /guardar estrategia/i }))

    expect(await screen.findByText(/solo puedes asociar docentes de la entidad/i)).toBeInTheDocument()
  })

  it('RQ_03: una estrategia con estudiantes no se puede editar', async () => {
    simularApi({
      ...catalogos,
      'GET /api/estrategias/7': {
        cuerpo: ejemplo.estrategia({ estado: 'SoloConsulta', esEditable: false }),
      },
    })
    pintar(<Rutas />, { ruta: '/estrategias/7/editar' })

    expect(await screen.findByText(/solo está disponible para consulta/i)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /guardar/i })).not.toBeInTheDocument()
  })
})
