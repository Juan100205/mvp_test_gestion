import { useQuery } from '@tanstack/react-query'
import { api, esErrorDeRed } from './api'
import { encolar, type Operacion } from './colaOffline'
import type { Acompanamiento, Docente, Entidad, Estrategia, EstrategiaResumen, RegistroAuditoria } from './tipos'

export const claves = {
  estrategias: ['estrategias'] as const,
  estrategia: (id: number) => ['estrategias', id] as const,
  acompanamientos: (estrategiaId: number) => ['acompanamientos', estrategiaId] as const,
  entidades: ['entidades'] as const,
  docentes: (entidadId: number) => ['entidades', entidadId, 'docentes'] as const,
  auditoria: (filtros: Record<string, string>) => ['auditoria', filtros] as const,
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
