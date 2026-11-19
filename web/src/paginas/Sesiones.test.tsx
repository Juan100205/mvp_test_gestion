import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { Rutas } from '../App'
import type { Documento, MiSesion, SesionDetalle, SesionResumen } from '../lib/tipos'
import { iniciarSesionComo, pintar, simularApi } from '../test/utilidades'

// CU-07: el líder programa sesiones y consulta el consolidado; el tutor ingresa y responde la encuesta.

const resumen: SesionResumen = {
  id: 3,
  nombre: 'Sesión 2 · Caribe',
  region: 'Caribe',
  inicio: '2026-11-19T14:00:00Z',
  fin: '2026-11-19T16:00:00Z',
  estado: 'EnCurso',
  convocados: 2,
  asistentes: 1,
  encuestas: 1,
}

const detalle: SesionDetalle = {
  ...resumen,
  convocados: [{ id: 1, nombre: 'Laura Gómez Palacios', ingresoEn: '2026-11-19T14:05:00Z', encuestaRespondida: true }],
  incidentes: [],
}

const consolidado: Documento = {
  tipo: 'sesion',
  titulo: 'Sesión 2 · Caribe',
  descripcion: 'Sesión de pilotaje',
  generadoEn: '2026-11-19T15:00:00Z',
  filtros: 'Región: Caribe · Estado: en curso',
  cifras: [{ etiqueta: 'asistencia', valor: '50 %', destacada: true }],
  tablas: [
    { titulo: 'Asistencia y uso del sistema', encabezado: ['Tutor', 'Asistió'], numericas: [], filas: [['Laura Gómez Palacios', 'Sí']] },
    { titulo: 'Incidentes', encabezado: ['Hora', 'Origen', 'Descripción'], numericas: [], filas: [['9:15 a. m.', 'Manual', 'Sin internet']] },
  ],
  nota: 'Uso medido por el servidor.',
  sinDatos: false,
}

const mia = (cambios: Partial<MiSesion> = {}): MiSesion => ({
  id: 3,
  nombre: 'Sesión 2 · Caribe',
  region: 'Caribe',
  inicio: '2026-11-19T14:00:00Z',
  fin: '2026-11-19T16:00:00Z',
  estado: 'EnCurso',
  puedeIngresar: true,
  ingresoEn: null,
  encuestaRespondida: false,
  ...cambios,
})

const preguntas = [1, 2, 3, 4, 5].map((n) => ({ clave: `p${n}`, texto: `Pregunta ${n}` }))

describe('Sesiones · líder de pilotaje', () => {
  afterEach(() => vi.restoreAllMocks())

  it('entra a sus sesiones con asistencia y estado', async () => {
    iniciarSesionComo('LiderPilotaje')
    simularApi({ 'GET /api/sesiones': { cuerpo: [resumen] } })
    pintar(<Rutas />, { ruta: '/' })

    expect(await screen.findByRole('heading', { name: /sesiones de pilotaje/i })).toBeInTheDocument()
    expect(await screen.findByText('1 de 2 asistieron')).toBeInTheDocument()
    expect(screen.getByText('En curso')).toBeInTheDocument()
  })

  it('programa una sesión convocando tutores de la región', async () => {
    iniciarSesionComo('LiderPilotaje')
    const llamadas = simularApi({
      'GET /api/sesiones': { cuerpo: [] },
      'GET /api/sesiones/tutores': {
        cuerpo: [
          { id: 1, nombre: 'Laura Gómez Palacios', region: 'Caribe' },
          { id: 2, nombre: 'Andrés Mosquera Rentería', region: 'Pacífico' },
        ],
      },
      'POST /api/sesiones': { status: 201, cuerpo: detalle },
    })
    pintar(<Rutas />, { ruta: '/sesiones' })
    await screen.findByText(/aún no hay sesiones/i)

    await userEvent.click(screen.getByRole('button', { name: /programar sesión/i }))
    await userEvent.type(screen.getByLabelText(/nombre de la sesión/i), 'Sesión 3 · Caribe')
    await userEvent.selectOptions(screen.getByLabelText(/región/i), 'Caribe')
    expect(screen.queryByLabelText('Andrés Mosquera Rentería')).not.toBeInTheDocument()
    await userEvent.click(await screen.findByLabelText('Laura Gómez Palacios'))
    await userEvent.click(screen.getByRole('button', { name: /programar y convocar/i }))

    await waitFor(() => {
      const cuerpo = llamadas.find((l) => l.metodo === 'POST' && l.ruta === '/api/sesiones')?.cuerpo as Record<string, unknown>
      expect(cuerpo).toMatchObject({ nombre: 'Sesión 3 · Caribe', region: 'Caribe', tutorIds: [1], duracionMinutos: 120 })
    })
  })

  it('muestra los errores de cada campo al programar', async () => {
    iniciarSesionComo('LiderPilotaje')
    simularApi({
      'GET /api/sesiones': { cuerpo: [] },
      'GET /api/sesiones/tutores': { cuerpo: [{ id: 1, nombre: 'Laura Gómez Palacios', region: 'Caribe' }] },
      'POST /api/sesiones': { status: 400, cuerpo: { codigo: 'datos_incompletos', detail: 'Completa estos datos', campos: ['nombre', 'region', 'tutores'] } },
    })
    pintar(<Rutas />, { ruta: '/sesiones' })
    await screen.findByText(/aún no hay sesiones/i)

    await userEvent.click(screen.getByRole('button', { name: /programar sesión/i }))
    await userEvent.click(screen.getByRole('button', { name: /programar y convocar/i }))

    expect(await screen.findByText('Escribe el nombre de la sesión.')).toBeInTheDocument()
    expect(screen.getByText('Convoca al menos un tutor de la región.')).toBeInTheDocument()
  })

  it('consolidado con asistencia e incidentes, y registro de incidentes manuales', async () => {
    iniciarSesionComo('LiderPilotaje')
    const llamadas = simularApi({
      'GET /api/sesiones/3': { cuerpo: detalle },
      'GET /api/sesiones/3/consolidado': { cuerpo: consolidado },
      'POST /api/sesiones/3/incidentes': { status: 201, cuerpo: { id: 1, descripcion: 'Wifi caído', registradoEn: '2026-11-19T14:30:00Z' } },
    })
    pintar(<Rutas />, { ruta: '/sesiones/3' })

    expect(await screen.findByRole('heading', { name: 'Asistencia y uso del sistema' })).toBeInTheDocument()
    expect(screen.getByText('50 %')).toBeInTheDocument()

    await userEvent.type(screen.getByLabelText(/registrar un incidente/i), 'Wifi caído')
    await userEvent.click(screen.getByRole('button', { name: /registrar incidente/i }))

    await waitFor(() => expect(llamadas.find((l) => l.ruta === '/api/sesiones/3/incidentes')?.cuerpo).toEqual({ descripcion: 'Wifi caído' }))
  })
})

describe('Mis sesiones · tutor', () => {
  afterEach(() => vi.restoreAllMocks())

  it('ingresa a la sesión y queda su asistencia', async () => {
    iniciarSesionComo('Tutor')
    const llamadas = simularApi({
      'GET /api/mis-sesiones': { cuerpo: [mia()] },
      'POST /api/sesiones/3/asistencia': { cuerpo: mia({ puedeIngresar: false, ingresoEn: '2026-11-19T14:05:00Z' }) },
    })
    pintar(<Rutas />, { ruta: '/mis-sesiones' })

    await userEvent.click(await screen.findByRole('button', { name: /ingresar a la sesión/i }))

    expect(await screen.findByText(/quedó registrada tu asistencia/i)).toBeInTheDocument()
    expect(llamadas.some((l) => l.metodo === 'POST' && l.ruta === '/api/sesiones/3/asistencia')).toBe(true)
  })

  it('flujo 3A: si no está convocado se le informa su sesión asignada', async () => {
    iniciarSesionComo('Tutor')
    simularApi({
      'GET /api/mis-sesiones': { cuerpo: [mia()] },
      'POST /api/sesiones/3/asistencia': {
        status: 403,
        cuerpo: { codigo: 'no_convocado', detail: 'No estás convocado a esta sesión. Tu sesión asignada es «Sesión 2 · Pacífico».' },
      },
    })
    pintar(<Rutas />, { ruta: '/mis-sesiones' })

    await userEvent.click(await screen.findByRole('button', { name: /ingresar a la sesión/i }))

    expect(await screen.findByText(/tu sesión asignada es/i)).toBeInTheDocument()
  })

  it('flujo 5A: la encuesta incompleta marca las preguntas pendientes y completa se guarda', async () => {
    iniciarSesionComo('Tutor')
    let intento = 0
    const llamadas = simularApi({
      'GET /api/mis-sesiones': { cuerpo: [mia({ puedeIngresar: false, ingresoEn: '2026-11-19T14:05:00Z' })] },
      'GET /api/sesiones/preguntas': { cuerpo: preguntas },
      'POST /api/sesiones/3/encuesta': () =>
        ++intento === 1
          ? { status: 400, cuerpo: { codigo: 'datos_incompletos', detail: 'Responde estas preguntas antes de guardar.', campos: ['p2', 'p3', 'p4', 'p5'] } }
          : { status: 201, cuerpo: mia({ encuestaRespondida: true }) },
    })
    pintar(<Rutas />, { ruta: '/mis-sesiones' })

    await userEvent.click(await screen.findByRole('button', { name: /responder encuesta/i }))
    const p1 = await screen.findByRole('group', { name: /1\. pregunta 1/i })
    await userEvent.click(within5(p1))
    await userEvent.click(screen.getByRole('button', { name: /enviar encuesta/i }))

    expect(await screen.findAllByText('Responde esta pregunta.')).toHaveLength(4)

    for (const n of [2, 3, 4, 5]) await userEvent.click(within5(screen.getByRole('group', { name: new RegExp(`${n}\\. pregunta ${n}`, 'i') })))
    await userEvent.click(screen.getByRole('button', { name: /enviar encuesta/i }))

    expect(await screen.findByText(/tus respuestas quedaron guardadas/i)).toBeInTheDocument()
    expect(llamadas.filter((l) => l.ruta === '/api/sesiones/3/encuesta').at(-1)?.cuerpo).toEqual({ respuestas: [5, 5, 5, 5, 5], comentario: '' })
  })
})

/** Opción "5" dentro del grupo de una pregunta. */
function within5(grupo: HTMLElement) {
  return [...grupo.querySelectorAll('label')].find((l) => l.textContent === '5')!
}
