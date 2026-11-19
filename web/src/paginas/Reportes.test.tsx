import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { Rutas } from '../App'
import { documentoACsv, documentoAPdf, nombreArchivo } from '../lib/reportes'
import type { Documento, FiltrosReporte, TipoReporte } from '../lib/tipos'
import { ejemplo, iniciarSesionComo, pintar, sesionDe, simularApi } from '../test/utilidades'

// CU-04 (funcionario) y CU-05 (ente de control): reportes autorizados, filtros, vista previa y exportación.

const filtros: FiltrosReporte = {
  regiones: ['Caribe', 'Pacífico'],
  entidades: ejemplo.entidades,
  tutores: [
    { id: 1, nombre: 'Laura Gómez Palacios', region: 'Caribe' },
    { id: 2, nombre: 'Andrés Mosquera Rentería', region: 'Pacífico' },
  ],
  periodos: ['2026-2'],
  meses: ['2026-11', '2026-10'],
}

const consolidado: TipoReporte = {
  id: 'consolidado',
  nombre: 'Estrategias y acompañamiento',
  descripcion: 'Estrategias por entidad.',
  grupo: 'Control',
  filtros: ['region', 'entidadId', 'periodo', 'mes'],
  requeridos: [],
}
const desempeno: TipoReporte = { ...consolidado, id: 'desempeno', nombre: 'Desempeño de tutores', grupo: 'Desempeño', filtros: ['region', 'tutorId', 'mes'] }
const cobertura: TipoReporte = {
  ...consolidado,
  id: 'ley-cobertura',
  nombre: 'Cobertura del programa',
  grupo: 'Reportes de ley',
  filtros: ['region', 'periodo'],
  requeridos: ['periodo'],
}

const documento = (cambios: Partial<Documento> = {}): Documento => ({
  tipo: 'consolidado',
  titulo: 'Estrategias y acompañamiento',
  descripcion: 'Estrategias por entidad.',
  generadoEn: '2026-11-19T15:00:00Z',
  filtros: 'Sin filtros',
  cifras: [
    { etiqueta: 'estrategias', valor: '2', destacada: false },
    { etiqueta: 'avance promedio', valor: '55 %', destacada: true },
  ],
  tablas: [
    {
      titulo: 'Estrategias',
      encabezado: ['Región', 'Entidad', 'Estrategia', 'Estudiantes'],
      numericas: [3],
      filas: [
        ['Caribe', 'IE Técnica Simón Bolívar', 'Club de lectura en voz alta', '5'],
        ['Pacífico', 'IE Normal Superior de Quibdó', '=Semillero "río"', '0'],
      ],
    },
  ],
  nota: 'Cifras agregadas, sin datos personales de estudiantes (Ley 1581 de 2012).',
  sinDatos: false,
  ...cambios,
})

function simular(tipos: TipoReporte[], doc = documento(), extra: Parameters<typeof simularApi>[0] = {}) {
  return simularApi({
    'GET /api/reportes/tipos': { cuerpo: tipos },
    'GET /api/reportes/filtros': { cuerpo: filtros },
    'GET /api/reportes/consolidado': { cuerpo: doc },
    'GET /api/reportes/desempeno': { cuerpo: { ...doc, tipo: 'desempeno', titulo: 'Desempeño de tutores' } },
    'GET /api/reportes/ley-cobertura': { cuerpo: { ...doc, tipo: 'ley-cobertura', titulo: 'Cobertura del programa' } },
    'POST /api/reportes/descargas': { status: 204 },
    ...extra,
  })
}

const espiarDescargas = () => {
  vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:prueba')
  vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => {})
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
}

describe('exportación de documentos', () => {
  it('CSV con encabezado, cifras y tablas, separado por ; y protegido contra fórmulas', () => {
    const csv = documentoACsv(documento())
    expect(csv).toContain('Estrategias y acompañamiento')
    expect(csv).toContain('avance promedio;55 %')
    expect(csv).toContain('Región;Entidad;Estrategia;Estudiantes')
    expect(csv).toContain(`"'=Semillero ""río"""`)
  })

  it('genera un PDF con nombre de archivo legible', async () => {
    const pdf = await documentoAPdf(documento(), sesionDe('EnteControl', 'ente.control').usuario)
    expect(pdf.type).toBe('application/pdf')
    expect(pdf.size).toBeGreaterThan(1000)
    expect(nombreArchivo(documento(), 'pdf', new Date('2026-11-19T10:00:00Z'))).toBe('sses-estrategias-y-acompanamiento-2026-11-19.pdf')
  })
})

describe('Reportes · CU-05 ente de control', () => {
  beforeEach(() => iniciarSesionComo('EnteControl'))
  afterEach(() => vi.restoreAllMocks())

  it('entra directo al reporte autorizado y ve cifras y tablas', async () => {
    simular([consolidado])
    pintar(<Rutas />, { ruta: '/' })

    expect(await screen.findByRole('heading', { name: /^reportes$/i })).toBeInTheDocument()
    expect(await screen.findByText('Club de lectura en voz alta')).toBeInTheDocument()
    expect(screen.getByText('55 %')).toBeInTheDocument()
    expect(screen.getByText(/perfil de solo consulta/i)).toBeInTheDocument()
    expect(screen.queryByLabelText(/^tutor$/i)).not.toBeInTheDocument()
  })

  it('envía solo los filtros autorizados del tipo', async () => {
    const llamadas = simular([consolidado])
    pintar(<Rutas />, { ruta: '/reportes' })
    await screen.findByText('Club de lectura en voz alta')

    await userEvent.selectOptions(screen.getByLabelText(/región/i), 'Pacífico')
    await userEvent.selectOptions(screen.getByLabelText(/mes del acompañamiento/i), '2026-11')
    await userEvent.click(screen.getByRole('button', { name: /generar reporte/i }))

    await waitFor(() => {
      const ultima = llamadas.filter((l) => l.ruta === '/api/reportes/consolidado').at(-1)!.url.searchParams
      expect(ultima.get('region')).toBe('Pacífico')
      expect(ultima.get('mes')).toBe('2026-11')
    })
  })

  it('flujo 3A: criterios fuera de sus permisos muestran lo autorizado', async () => {
    simular([consolidado], documento(), {
      'GET /api/reportes/consolidado': {
        status: 403,
        cuerpo: { codigo: 'criterio_no_autorizado', detail: 'Con «Estrategias y acompañamiento» puedes filtrar por región, entidad.', campos: ['tutorId'] },
      },
    })
    pintar(<Rutas />, { ruta: '/reportes' })

    expect(await screen.findByText(/puedes filtrar por región, entidad/i)).toBeInTheDocument()
  })

  it('flujo 4A: sin información lo dice y no permite descargar', async () => {
    simular([consolidado], documento({ sinDatos: true, tablas: [{ ...documento().tablas[0]!, filas: [] }] }))
    pintar(<Rutas />, { ruta: '/reportes' })

    expect(await screen.findByText(/no se encontraron registros/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /descargar csv/i })).toBeDisabled()
    expect(screen.getByRole('button', { name: /vista previa/i })).toBeDisabled()
  })

  it('vista previa con el diseño del documento y descarga del PDF registrada', async () => {
    espiarDescargas()
    const llamadas = simular([consolidado])
    pintar(<Rutas />, { ruta: '/reportes' })
    await screen.findByText('Club de lectura en voz alta')

    await userEvent.click(screen.getByRole('button', { name: /vista previa/i }))
    const pdf = await screen.findByRole('article', { name: /vista previa del pdf/i })
    expect(within(pdf).getByRole('heading', { name: /estrategias y acompañamiento/i })).toBeInTheDocument()
    expect(within(pdf).getByText(/generado por: laura gómez palacios \(ente de control\)/i)).toBeInTheDocument()

    await userEvent.click(screen.getByRole('tab', { name: /csv/i }))
    expect(screen.getByRole('group', { name: /vista previa del csv/i })).toHaveTextContent('=Semillero "río"')

    await userEvent.click(screen.getByRole('tab', { name: /pdf/i }))
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: /descargar pdf/i }))

    await waitFor(() =>
      expect(llamadas.find((l) => l.ruta === '/api/reportes/descargas')?.cuerpo).toEqual({ tipo: 'consolidado', formato: 'pdf', filtros: 'Sin filtros' }),
    )
  })
})

describe('Reportes · CU-04 funcionario', () => {
  beforeEach(() => iniciarSesionComo('Funcionario'))
  afterEach(() => vi.restoreAllMocks())

  it('elige entre desempeño, actividades y reportes de ley', async () => {
    const llamadas = simular([consolidado, desempeno, cobertura])
    pintar(<Rutas />, { ruta: '/' })
    await screen.findByText('Club de lectura en voz alta')

    await userEvent.click(screen.getByRole('radio', { name: /desempeño de tutores/i }))

    await waitFor(() => expect(llamadas.some((l) => l.ruta === '/api/reportes/desempeno')).toBe(true))
    expect(screen.getByLabelText(/^tutor$/i)).toBeInTheDocument()
  })

  it('flujo 3A: el reporte de ley exige el periodo antes de generarse', async () => {
    const llamadas = simular([cobertura])
    pintar(<Rutas />, { ruta: '/reportes' })

    expect(await screen.findByText(/elige el periodo académico y genera el reporte/i)).toBeInTheDocument()
    expect(llamadas.some((l) => l.ruta === '/api/reportes/ley-cobertura')).toBe(false)

    await userEvent.selectOptions(screen.getByLabelText(/periodo académico/i), '2026-2')
    await userEvent.click(screen.getByRole('button', { name: /generar reporte/i }))

    await waitFor(() => expect(llamadas.find((l) => l.ruta === '/api/reportes/ley-cobertura')?.url.searchParams.get('periodo')).toBe('2026-2'))
  })

  it('un tutor no puede abrir los reportes', async () => {
    iniciarSesionComo('Tutor')
    simularApi({ 'GET /api/estrategias': { cuerpo: [] } })
    pintar(<Rutas />, { ruta: '/reportes' })

    expect(await screen.findByRole('heading', { name: /tus estrategias/i })).toBeInTheDocument()
  })
})
