import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import { Aviso, Cargando, ErrorCampo, ErrorCarga, Volver } from '../componentes/Estado'
import { Hoja } from '../componentes/Hoja'
import { Icono } from '../componentes/Icono'
import { ContenidoDocumento, ExportarDocumento } from '../componentes/VistaPreviaReporte'
import { api, ApiError, mensajeDeError } from '../lib/api'
import { claves, useConsolidado, useSesionPilotaje, useSesiones, useTutoresElegibles } from '../lib/consultas'
import { useSesion } from '../lib/sesion'
import type { EstadoSesion, SesionDetalle } from '../lib/tipos'

export const textoEstado: Record<EstadoSesion, string> = { Programada: 'Programada', EnCurso: 'En curso', Terminada: 'Terminada' }

export const fechaSesion = (inicio: string, fin: string) => {
  const a = new Date(inicio)
  const b = new Date(fin)
  const dia = a.toLocaleDateString('es-CO', { weekday: 'long', day: 'numeric', month: 'long' })
  const hora = (d: Date) => d.toLocaleTimeString('es-CO', { hour: 'numeric', minute: '2-digit' })
  return `${dia}, ${hora(a)} a ${hora(b)}`
}

export function SelloSesion({ estado }: { estado: EstadoSesion }) {
  const estilos: Record<EstadoSesion, string> = {
    Programada: 'border border-duna text-niebla',
    EnCurso: 'bg-terracota text-tinta',
    Terminada: 'bg-tinta text-papel',
  }
  return <span className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-semibold ${estilos[estado]}`}>{textoEstado[estado]}</span>
}

/** CU-07 pasos 2 y 6: el líder programa las sesiones por región y consulta su consolidado. */
export function Sesiones() {
  const sesiones = useSesiones()
  const [programando, setProgramando] = useState(false)

  return (
    <>
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-sm text-niebla">Líder de pilotaje</p>
          <h1 className="mt-1 text-2xl font-semibold sm:text-3xl">Sesiones de pilotaje</h1>
          <p className="mt-1.5 max-w-prose text-sm text-niebla">
            Programa las sesiones por región, convoca a los tutores y revisa la asistencia, el uso del sistema, los incidentes y la encuesta.
          </p>
        </div>
        <button type="button" className="boton-acento w-full sm:w-auto" onClick={() => setProgramando(true)}>
          <Icono nombre="mas" />
          Programar sesión
        </button>
      </header>

      <section className="mt-8">
        {sesiones.isPending ? (
          <Cargando />
        ) : sesiones.error ? (
          <ErrorCarga error={sesiones.error} reintentar={() => void sesiones.refetch()} />
        ) : sesiones.data.length === 0 ? (
          <div className="border-y border-duna py-12 text-center">
            <p className="font-display text-lg font-semibold">Aún no hay sesiones programadas</p>
            <p className="mt-2 text-sm text-niebla">Usa «Programar sesión» para crear la primera.</p>
          </div>
        ) : (
          <ul className="divide-y divide-duna border-y border-duna">
            {sesiones.data.map((s) => (
              <li key={s.id}>
                <Link to={`/sesiones/${s.id}`} className="flex flex-col gap-2 py-4 hover:bg-duna-suave/40 sm:flex-row sm:items-center sm:justify-between sm:px-2">
                  <div className="min-w-0">
                    <p className="font-display text-lg font-semibold">{s.nombre}</p>
                    <p className="text-sm text-niebla">
                      {s.region} · {fechaSesion(s.inicio, s.fin)}
                    </p>
                  </div>
                  <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-niebla">
                    <span>
                      {s.asistentes} de {s.convocados} asistieron
                    </span>
                    <span>{s.encuestas} encuestas</span>
                    <SelloSesion estado={s.estado} />
                  </div>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </section>

      <Hoja abierta={programando} titulo="Programar sesión" subtitulo="Los tutores convocados la verán en «Mis sesiones»." onCerrar={() => setProgramando(false)}>
        {programando && <FormSesion alTerminar={() => setProgramando(false)} />}
      </Hoja>
    </>
  )
}

const ahoraLocal = () => {
  const d = new Date(Date.now() + 60 * 60 * 1000)
  d.setMinutes(0, 0, 0)
  return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16)
}

function FormSesion({ alTerminar }: { alTerminar: () => void }) {
  const cliente = useQueryClient()
  const elegibles = useTutoresElegibles()
  const [nombre, setNombre] = useState('')
  const [region, setRegion] = useState('')
  const [inicio, setInicio] = useState(ahoraLocal)
  const [duracion, setDuracion] = useState(120)
  const [elegidos, setElegidos] = useState<number[]>([])
  const regiones = [...new Set(elegibles.data?.map((t) => t.region) ?? [])].sort()
  const tutores = (elegibles.data ?? []).filter((t) => t.region === region)

  const programar = useMutation({
    mutationFn: () =>
      api<SesionDetalle>('/api/sesiones', {
        metodo: 'POST',
        cuerpo: { nombre, region, inicio: new Date(inicio).toISOString(), duracionMinutos: duracion, tutorIds: elegidos },
      }),
    onSuccess: () => {
      void cliente.invalidateQueries({ queryKey: claves.sesiones })
      alTerminar()
    },
  })
  const campos = programar.error instanceof ApiError ? programar.error.campos : []

  const enviar = (e: FormEvent) => {
    e.preventDefault()
    programar.mutate()
  }

  return (
    <form noValidate onSubmit={enviar} className="grid gap-4">
      <div>
        <label htmlFor="s-nombre" className="mb-1.5 block text-sm font-medium">
          Nombre de la sesión
        </label>
        <input id="s-nombre" className="campo" value={nombre} onChange={(e) => setNombre(e.target.value)} placeholder="Ej. Sesión 3 · Caribe" />
        {campos.includes('nombre') && <ErrorCampo id="s-nombre-error">Escribe el nombre de la sesión.</ErrorCampo>}
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label htmlFor="s-region" className="mb-1.5 block text-sm font-medium">
            Región
          </label>
          <select
            id="s-region"
            className="campo"
            value={region}
            onChange={(e) => {
              setRegion(e.target.value)
              setElegidos([])
            }}
          >
            <option value="">Elige la región</option>
            {regiones.map((r) => (
              <option key={r} value={r}>
                {r}
              </option>
            ))}
          </select>
          {campos.includes('region') && <ErrorCampo id="s-region-error">Elige la región.</ErrorCampo>}
        </div>
        <div>
          <label htmlFor="s-duracion" className="mb-1.5 block text-sm font-medium">
            Duración
          </label>
          <select id="s-duracion" className="campo" value={duracion} onChange={(e) => setDuracion(Number(e.target.value))}>
            {[60, 90, 120, 180, 240].map((m) => (
              <option key={m} value={m}>
                {m < 120 ? `${m} minutos` : `${m / 60} horas`}
              </option>
            ))}
          </select>
        </div>
      </div>
      <div>
        <label htmlFor="s-inicio" className="mb-1.5 block text-sm font-medium">
          Fecha y hora de inicio
        </label>
        <input id="s-inicio" type="datetime-local" className="campo" value={inicio} onChange={(e) => setInicio(e.target.value)} />
        {campos.includes('inicio') && <ErrorCampo id="s-inicio-error">La sesión debe empezar en una fecha y hora futuras.</ErrorCampo>}
      </div>
      <fieldset>
        <legend className="mb-1.5 text-sm font-medium">Tutores convocados</legend>
        {!region ? (
          <p className="text-sm text-niebla">Primero elige la región para ver sus tutores.</p>
        ) : elegibles.isPending ? (
          <p className="text-sm text-niebla">Cargando tutores…</p>
        ) : (
          <ul className="divide-y divide-duna rounded-control border border-duna">
            {tutores.map((t) => (
              <li key={t.id}>
                <label className="flex min-h-11 cursor-pointer items-center gap-3 px-3 py-2.5">
                  <input
                    type="checkbox"
                    className="size-4 accent-tinta"
                    checked={elegidos.includes(t.id)}
                    onChange={(e) => setElegidos(e.target.checked ? [...elegidos, t.id] : elegidos.filter((x) => x !== t.id))}
                  />
                  <span className="text-sm font-medium">{t.nombre}</span>
                </label>
              </li>
            ))}
          </ul>
        )}
        {campos.includes('tutores') && <ErrorCampo id="s-tutores-error">Convoca al menos un tutor de la región.</ErrorCampo>}
      </fieldset>
      {programar.error && !(programar.error instanceof ApiError && programar.error.campos.length > 0) && <Aviso>{mensajeDeError(programar.error)}</Aviso>}
      {programar.error instanceof ApiError && programar.error.campos.length > 0 && <Aviso>{programar.error.message}</Aviso>}
      <button type="submit" className="boton-primario" disabled={programar.isPending}>
        {programar.isPending ? 'Programando…' : 'Programar y convocar'}
      </button>
    </form>
  )
}

/** CU-07 pasos 6 y 7: consolidado de la sesión (asistencia, uso, incidentes y encuesta) y exportación del registro. */
export function SesionDetalleVista() {
  const { id } = useParams()
  const sesionId = Number(id)
  const { sesion: usuario } = useSesion()
  const sesion = useSesionPilotaje(sesionId)
  const consolidado = useConsolidado(sesionId)
  const cliente = useQueryClient()
  const [incidente, setIncidente] = useState('')
  const registrar = useMutation({
    mutationFn: () => api(`/api/sesiones/${sesionId}/incidentes`, { metodo: 'POST', cuerpo: { descripcion: incidente } }),
    onSuccess: () => {
      setIncidente('')
      void cliente.invalidateQueries({ queryKey: claves.sesion(sesionId) })
      void cliente.invalidateQueries({ queryKey: claves.consolidado(sesionId) })
    },
  })

  if (!usuario) return null
  if (sesion.isPending) return <Cargando />
  if (sesion.error) return <ErrorCarga error={sesion.error} reintentar={() => void sesion.refetch()} />
  const s = sesion.data

  return (
    <>
      <Volver a="/sesiones" texto="Sesiones de pilotaje" />
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <SelloSesion estado={s.estado} />
          <h1 className="mt-2 text-2xl font-semibold sm:text-3xl">{s.nombre}</h1>
          <p className="mt-1 text-sm text-niebla">
            {s.region} · {fechaSesion(s.inicio, s.fin)}
          </p>
        </div>
        <ExportarDocumento
          documento={consolidado.data}
          usuario={usuario.usuario}
          registrar={(formato) => api(`/api/sesiones/${sesionId}/descargas`, { metodo: 'POST', cuerpo: { formato } })}
        />
      </header>

      <form
        className="mt-6 flex flex-col gap-2 rounded-control border border-duna bg-papel p-4 sm:flex-row sm:items-end"
        onSubmit={(e) => {
          e.preventDefault()
          registrar.mutate()
        }}
      >
        <div className="flex-1">
          <label htmlFor="incidente" className="mb-1.5 block text-sm font-medium">
            Registrar un incidente
          </label>
          <input id="incidente" className="campo" value={incidente} onChange={(e) => setIncidente(e.target.value)} placeholder="Ej. La sede no tiene internet" />
        </div>
        <button type="submit" className="boton-secundario" disabled={registrar.isPending || incidente.trim().length === 0}>
          {registrar.isPending ? 'Guardando…' : 'Registrar incidente'}
        </button>
      </form>
      {registrar.error && (
        <div className="mt-2">
          <Aviso>{mensajeDeError(registrar.error)}</Aviso>
        </div>
      )}

      <section className="mt-8">
        {consolidado.isPending ? (
          <Cargando texto="Consolidando la sesión…" />
        ) : consolidado.error ? (
          <ErrorCarga error={consolidado.error} reintentar={() => void consolidado.refetch()} />
        ) : (
          <ContenidoDocumento documento={consolidado.data} />
        )}
      </section>
    </>
  )
}
