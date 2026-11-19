import { useQuery } from '@tanstack/react-query'
import { api, esErrorDeRed } from './api'
import { encolar, type Operacion } from './colaOffline'
import type {
  Acompanamiento,
  Docente,
  Documento,
  Entidad,
  Estrategia,
  EstrategiaResumen,
  FiltrosReporte,
  MiSesion,
  Pregunta,
  RegistroAuditoria,
  SesionDetalle,
  SesionResumen,
  TipoReporte,
  TutorReporte,
} from './tipos'

export const claves = {
  estrategias: ['estrategias'] as const,
  estrategia: (id: number) => ['estrategias', id] as const,
  acompanamientos: (estrategiaId: number) => ['acompanamientos', estrategiaId] as const,
  entidades: ['entidades'] as const,
  docentes: (entidadId: number) => ['entidades', entidadId, 'docentes'] as const,
  auditoria: (filtros: Record<string, string>) => ['auditoria', filtros] as const,
  filtrosReporte: ['reportes', 'filtros'] as const,
  tiposReporte: ['reportes', 'tipos'] as const,
  reporte: (tipo: string, filtros: Record<string, string>) => ['reportes', tipo, filtros] as const,
  sesiones: ['sesiones'] as const,
  sesion: (id: number) => ['sesiones', id] as const,
  consolidado: (id: number) => ['sesiones', id, 'consolidado'] as const,
  tutoresRegion: (region: string) => ['sesiones', 'tutores', region] as const,
  misSesiones: ['mis-sesiones'] as const,
  preguntas: ['preguntas'] as const,
}

export const useEstrategias = () =>
  useQuery({ queryKey: claves.estrategias, queryFn: () => api<EstrategiaResumen[]>('/api/estrategias') })

export const useEstrategia = (id: number) =>
  useQuery({
    queryKey: claves.estrategia(id),
    queryFn: () => api<Estrategia>(`/api/estrategias/${id}`),
    enabled: Number.isInteger(id) && id > 0,
  })

export const useAcompanamientos = (estrategiaId: number) =>
  useQuery({
    queryKey: claves.acompanamientos(estrategiaId),
    queryFn: () => api<Acompanamiento[]>(`/api/estrategias/${estrategiaId}/acompanamientos`),
  })

export const useEntidades = () =>
  useQuery({ queryKey: claves.entidades, queryFn: () => api<Entidad[]>('/api/entidades'), staleTime: Infinity })

export const useDocentesDeEntidad = (entidadId: number | null) =>
  useQuery({
    queryKey: claves.docentes(entidadId ?? 0),
    queryFn: () => api<Docente[]>(`/api/entidades/${entidadId}/docentes`),
    enabled: entidadId !== null,
    staleTime: Infinity,
  })

export const useAuditoria = (filtros: Record<string, string>) =>
  useQuery({
    queryKey: claves.auditoria(filtros),
    queryFn: () => {
      const parametros = new URLSearchParams(Object.entries(filtros).filter(([, v]) => v))
      return api<RegistroAuditoria[]>(`/api/auditoria?${parametros}`)
    },
  })

const conParametros = (ruta: string, filtros: Record<string, string>) => {
  const parametros = new URLSearchParams(Object.entries(filtros).filter(([, v]) => v))
  return parametros.size > 0 ? `${ruta}?${parametros}` : ruta
}

export const useFiltrosReporte = () =>
  useQuery({ queryKey: claves.filtrosReporte, queryFn: () => api<FiltrosReporte>('/api/reportes/filtros'), staleTime: 60_000 })

export const useTiposReporte = () =>
  useQuery({ queryKey: claves.tiposReporte, queryFn: () => api<TipoReporte[]>('/api/reportes/tipos'), staleTime: Infinity })

/** CU-04 y CU-05: documento del reporte con los filtros aplicados. */
export const useReporte = (tipo: string | undefined, filtros: Record<string, string>) =>
  useQuery({
    queryKey: claves.reporte(tipo ?? '', filtros),
    queryFn: () => api<Documento>(conParametros(`/api/reportes/${tipo}`, filtros)),
    enabled: Boolean(tipo),
  })

// CU-07: sesiones de pilotaje.
export const useSesiones = () => useQuery({ queryKey: claves.sesiones, queryFn: () => api<SesionResumen[]>('/api/sesiones') })

export const useSesionPilotaje = (id: number) =>
  useQuery({ queryKey: claves.sesion(id), queryFn: () => api<SesionDetalle>(`/api/sesiones/${id}`), enabled: id > 0 })

export const useConsolidado = (id: number) =>
  useQuery({ queryKey: claves.consolidado(id), queryFn: () => api<Documento>(`/api/sesiones/${id}/consolidado`), enabled: id > 0 })

/** Tutores que el líder puede convocar (todas las regiones). */
export const useTutoresElegibles = () =>
  useQuery({ queryKey: claves.tutoresRegion(''), queryFn: () => api<TutorReporte[]>('/api/sesiones/tutores') })

export const useMisSesiones = () => useQuery({ queryKey: claves.misSesiones, queryFn: () => api<MiSesion[]>('/api/mis-sesiones') })

export const usePreguntas = () =>
  useQuery({ queryKey: claves.preguntas, queryFn: () => api<Pregunta[]>('/api/sesiones/preguntas'), staleTime: Infinity })

export type ResultadoEnvio<T> = { encolada: true } | { encolada: false; datos: T }

/** Envía si hay señal; si no, guarda en el dispositivo para sincronizar después (RQ_21). */
export async function enviarOEncolar<T>(op: Omit<Operacion, 'creadaEn'>): Promise<ResultadoEnvio<T>> {
  if (!navigator.onLine) {
    await encolar(op)
    return { encolada: true }
  }
  try {
    return { encolada: false, datos: await api<T>(op.ruta, { metodo: op.metodo, cuerpo: op.cuerpo }) }
  } catch (error) {
    if (!esErrorDeRed(error)) throw error
    await encolar(op)
    return { encolada: true }
  }
}
