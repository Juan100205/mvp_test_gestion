import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { Aviso, Cargando, ErrorCampo, ErrorCarga, Volver } from '../componentes/Estado'
import { api, ApiError, mensajeDeError } from '../lib/api'
import { claves, useDocentesDeEntidad, useEntidades, useEstrategia } from '../lib/consultas'
import { periodosAcademicos } from '../lib/periodo'
import type { Entidad, Estrategia } from '../lib/tipos'

type Campo = 'entidadId' | 'nombre' | 'periodo' | 'docentes'

const mensajesFaltantes: Record<Campo, string> = {
  entidadId: 'Elige la entidad educativa.',
  nombre: 'Escribe el nombre de la estrategia.',
  periodo: 'Elige el periodo académico.',
  docentes: 'Elige al menos un docente de la entidad.',
}

interface Valores {
  entidadId: number | null
  nombre: string
  descripcion: string
  periodo: string
  docenteIds: number[]
}

const periodoEnCurso = (hoy = new Date()) => `${hoy.getFullYear()}-${hoy.getMonth() < 6 ? 1 : 2}`

function porRegion(entidades: Entidad[]) {
  const grupos = new Map<string, Entidad[]>()
  for (const e of entidades) grupos.set(e.region, [...(grupos.get(e.region) ?? []), e])
  return [...grupos]
}

/** CU-01: crear (flujo principal) y editar mientras no tenga estudiantes (RQ_03). */
export function EstrategiaForm() {
  const { id } = useParams()
  const editando = id !== undefined
  const estrategia = useEstrategia(Number(id))

  if (!editando) return <Formulario />
  if (estrategia.isPending) return <Cargando />
  if (estrategia.error) return <ErrorCarga error={estrategia.error} reintentar={() => void estrategia.refetch()} />
  if (!estrategia.data.esEditable)
    return (
      <>
        <Volver a={`/estrategias/${id}`} texto="Volver a la estrategia" />
        <Aviso>
          Esta estrategia ya tiene estudiantes vinculados y solo está disponible para consulta.{' '}
          <Link to={`/estrategias/${id}`} className="font-semibold underline underline-offset-4">
            Ver el detalle
          </Link>
        </Aviso>
      </>
    )
  return <Formulario existente={estrategia.data} />
}

function Formulario({ existente }: { existente?: Estrategia }) {
  const navegar = useNavigate()
  const cliente = useQueryClient()
  const entidades = useEntidades()
  const [valores, setValores] = useState<Valores>(() => ({
    entidadId: existente?.entidad.id ?? null,
    nombre: existente?.nombre ?? '',
    descripcion: existente?.descripcion ?? '',
    periodo: existente?.periodo ?? periodoEnCurso(),
    docenteIds: existente?.docentes.map((d) => d.id) ?? [],
  }))
  const docentes = useDocentesDeEntidad(valores.entidadId)
  const [errores, setErrores] = useState<Partial<Record<Campo, string>>>({})
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null)

  const guardar = useMutation({
    mutationFn: (v: Valores) => {
      const cuerpo = { entidadId: v.entidadId, nombre: v.nombre.trim(), descripcion: v.descripcion.trim(), periodo: v.periodo, docenteIds: v.docenteIds }
      return existente
        ? api<Estrategia>(`/api/estrategias/${existente.id}`, { metodo: 'PUT', cuerpo })
        : api<Estrategia>('/api/estrategias', { metodo: 'POST', cuerpo })
    },
    onSuccess: async (guardada) => {
      cliente.setQueryData(claves.estrategia(guardada.id), guardada)
      await cliente.invalidateQueries({ queryKey: claves.estrategias, exact: true })
      navegar(`/estrategias/${guardada.id}`, { replace: true })
    },
    onError: (error) => {
      if (error instanceof ApiError && error.campos.length > 0)
        setErrores(Object.fromEntries(error.campos.map((c) => [c, error.message])))
      else setErrorGeneral(mensajeDeError(error))
    },
  })

  const cambiar = <K extends keyof Valores>(campo: K, valor: Valores[K]) => {
    setValores((v) => ({ ...v, [campo]: valor, ...(campo === 'entidadId' ? { docenteIds: [] } : {}) }))
    setErrores((e) => ({ ...e, [campo === 'docenteIds' ? 'docentes' : campo]: undefined }))
  }

  function alEnviar(evento: FormEvent) {
    evento.preventDefault()
    setErrorGeneral(null)
    const faltantes: Partial<Record<Campo, string>> = {}
    if (valores.entidadId === null) faltantes.entidadId = mensajesFaltantes.entidadId
    if (!valores.nombre.trim()) faltantes.nombre = mensajesFaltantes.nombre
    if (!valores.periodo) faltantes.periodo = mensajesFaltantes.periodo
    if (valores.docenteIds.length === 0) faltantes.docentes = mensajesFaltantes.docentes
    setErrores(faltantes)
    if (Object.keys(faltantes).length === 0) guardar.mutate(valores)
  }

  const invalido = (campo: Campo) => (errores[campo] ? { 'aria-invalid': true, 'aria-describedby': `${campo}-error` } : {})
  const volverA = existente ? `/estrategias/${existente.id}` : '/estrategias'

  return (
    <>
      <Volver a={volverA} texto={existente ? 'Volver a la estrategia' : 'Tus estrategias'} />
      <h1 className="text-2xl font-semibold sm:text-3xl">{existente ? 'Editar estrategia' : 'Nueva estrategia'}</h1>
      <p className="mt-1.5 max-w-prose text-sm text-niebla">
        {existente
          ? 'Se puede editar hasta que tenga el primer estudiante vinculado.'
          : 'Las entidades y los docentes vienen precargados por el Ministerio.'}
      </p>

      <form noValidate onSubmit={alEnviar} className="mt-8 grid max-w-2xl gap-6">
        <div>
          <label htmlFor="entidad" className="mb-1.5 block text-sm font-medium">
            Entidad educativa
          </label>
          {entidades.error ? (
            <ErrorCarga error={entidades.error} reintentar={() => void entidades.refetch()} />
          ) : (
            <select
              id="entidad"
              className="campo"
              value={valores.entidadId ?? ''}
              disabled={!!existente || entidades.isPending}
              onChange={(e) => cambiar('entidadId', e.target.value ? Number(e.target.value) : null)}
              {...invalido('entidadId')}
            >
              <option value="">{entidades.isPending ? 'Cargando entidades…' : 'Elige una entidad'}</option>
              {porRegion(entidades.data ?? []).map(([region, lista]) => (
                <optgroup key={region} label={region}>
                  {lista.map((e) => (
                    <option key={e.id} value={e.id}>
                      {e.nombre}, {e.municipio}
                    </option>
                  ))}
                </optgroup>
              ))}
            </select>
          )}
          {existente && <p className="mt-1.5 text-xs text-niebla">La entidad no cambia después de crear la estrategia.</p>}
          {errores.entidadId && <ErrorCampo id="entidadId-error">{errores.entidadId}</ErrorCampo>}
        </div>

        <div>
          <label htmlFor="nombre" className="mb-1.5 block text-sm font-medium">
            Nombre
          </label>
          <input
            id="nombre"
            className="campo"
            maxLength={150}
            value={valores.nombre}
            onChange={(e) => cambiar('nombre', e.target.value)}
            {...invalido('nombre')}
          />
          {errores.nombre && <ErrorCampo id="nombre-error">{errores.nombre}</ErrorCampo>}
        </div>

        <div>
          <label htmlFor="descripcion" className="mb-1.5 block text-sm font-medium">
            Descripción <span className="font-normal text-niebla">(opcional)</span>
          </label>
          <textarea
            id="descripcion"
            rows={3}
            maxLength={1000}
            className="campo resize-y"
            value={valores.descripcion}
            onChange={(e) => cambiar('descripcion', e.target.value)}
          />
        </div>

        <div className="sm:max-w-xs">
          <label htmlFor="periodo" className="mb-1.5 block text-sm font-medium">
            Periodo académico
          </label>
          <select id="periodo" className="campo" value={valores.periodo} onChange={(e) => cambiar('periodo', e.target.value)} {...invalido('periodo')}>
            {[...new Set([valores.periodo, ...periodosAcademicos()])].filter(Boolean).map((p) => (
              <option key={p} value={p}>
                {p}
              </option>
            ))}
          </select>
          {errores.periodo && <ErrorCampo id="periodo-error">{errores.periodo}</ErrorCampo>}
        </div>

        <fieldset aria-describedby={errores.docentes ? 'docentes-error' : undefined}>
          <legend className="mb-1.5 text-sm font-medium">Docentes</legend>
          {valores.entidadId === null ? (
            <p className="text-sm text-niebla">Primero elige la entidad para ver sus docentes.</p>
          ) : docentes.isPending ? (
            <p className="text-sm text-niebla">Cargando docentes…</p>
          ) : docentes.error ? (
            <ErrorCarga error={docentes.error} reintentar={() => void docentes.refetch()} />
          ) : (
            <ul className={`divide-y divide-duna rounded-control border bg-papel ${errores.docentes ? 'border-arcilla' : 'border-duna'}`}>
              {docentes.data.map((d) => (
                <li key={d.id}>
                  <label className="flex min-h-11 cursor-pointer items-center gap-3 px-3 py-2.5">
                    <input
                      type="checkbox"
                      className="size-4 accent-tinta"
                      checked={valores.docenteIds.includes(d.id)}
                      onChange={(e) =>
                        cambiar(
                          'docenteIds',
                          e.target.checked ? [...valores.docenteIds, d.id] : valores.docenteIds.filter((x) => x !== d.id),
                        )
                      }
                    />
                    <span className="text-sm font-medium">{d.nombre}</span>
                  </label>
                </li>
              ))}
            </ul>
          )}
          {errores.docentes && <ErrorCampo id="docentes-error">{errores.docentes}</ErrorCampo>}
        </fieldset>

        {errorGeneral && <Aviso>{errorGeneral}</Aviso>}

        <div className="flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
          <Link to={volverA} className="boton-secundario">
            Cancelar
          </Link>
          <button type="submit" className="boton-primario" disabled={guardar.isPending}>
            {guardar.isPending ? 'Guardando…' : existente ? 'Guardar cambios' : 'Guardar estrategia'}
          </button>
        </div>
      </form>
    </>
  )
}
