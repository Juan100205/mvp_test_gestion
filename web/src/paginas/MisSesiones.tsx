import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Aviso, Cargando, ErrorCampo, ErrorCarga } from '../componentes/Estado'
import { Hoja } from '../componentes/Hoja'
import { api, ApiError, mensajeDeError } from '../lib/api'
import { claves, useMisSesiones, usePreguntas } from '../lib/consultas'
import { hora } from '../lib/periodo'
import type { MiSesion } from '../lib/tipos'
import { fechaSesion, SelloSesion } from './Sesiones'

/** CU-07 pasos 3 y 5: el tutor ve sus convocatorias, ingresa a la sesión (asistencia) y responde la encuesta. */
export function MisSesiones() {
  const sesiones = useMisSesiones()
  const cliente = useQueryClient()
  const [encuesta, setEncuesta] = useState<MiSesion | null>(null)
  const [aviso, setAviso] = useState<{ tono: 'error' | 'info'; texto: string } | null>(null)

  const ingresar = useMutation({
    mutationFn: (id: number) => api<MiSesion>(`/api/sesiones/${id}/asistencia`, { metodo: 'POST' }),
    onSuccess: (s) => {
      setAviso({ tono: 'info', texto: `Quedó registrada tu asistencia a «${s.nombre}» a las ${hora(s.ingresoEn!)}.` })
      void cliente.invalidateQueries({ queryKey: claves.misSesiones })
    },
    onError: (e) => setAviso({ tono: 'error', texto: mensajeDeError(e) }),
  })

  return (
    <>
      <header>
        <p className="text-sm text-niebla">Pilotaje</p>
        <h1 className="mt-1 text-2xl font-semibold sm:text-3xl">Mis sesiones</h1>
        <p className="mt-1.5 max-w-prose text-sm text-niebla">
          Sesiones a las que te convocó el líder de pilotaje. Ingresa desde 30 minutos antes para que quede tu asistencia y, al terminar,
          responde la encuesta.
        </p>
      </header>

      {aviso && (
        <div className="mt-6">
          <Aviso tono={aviso.tono}>{aviso.texto}</Aviso>
        </div>
      )}

      <section className="mt-6">
        {sesiones.isPending ? (
          <Cargando />
        ) : sesiones.error ? (
          <ErrorCarga error={sesiones.error} reintentar={() => void sesiones.refetch()} />
        ) : sesiones.data.length === 0 ? (
          <div className="border-y border-duna py-12 text-center">
            <p className="font-display text-lg font-semibold">No tienes sesiones asignadas</p>
            <p className="mt-2 text-sm text-niebla">Cuando el líder de pilotaje te convoque, la sesión aparecerá aquí.</p>
          </div>
        ) : (
          <ul className="divide-y divide-duna border-y border-duna">
            {sesiones.data.map((s) => (
              <li key={s.id} className="flex flex-col gap-3 py-4 sm:flex-row sm:items-center sm:justify-between">
                <div className="min-w-0">
                  <div className="flex items-center gap-2">
                    <p className="font-display text-lg font-semibold">{s.nombre}</p>
                    <SelloSesion estado={s.estado} />
                  </div>
                  <p className="text-sm text-niebla">
                    {s.region} · {fechaSesion(s.inicio, s.fin)}
                  </p>
                  {s.ingresoEn && <p className="mt-1 text-xs font-medium text-tinta">Asististe: ingresaste a las {hora(s.ingresoEn)}.</p>}
                </div>
                <div className="grid gap-2 sm:flex">
                  {s.puedeIngresar && (
                    <button type="button" className="boton-acento" disabled={ingresar.isPending} onClick={() => ingresar.mutate(s.id)}>
                      {ingresar.isPending ? 'Registrando…' : 'Ingresar a la sesión'}
                    </button>
                  )}
                  {s.ingresoEn && !s.encuestaRespondida && (
                    <button type="button" className="boton-primario" onClick={() => setEncuesta(s)}>
                      Responder encuesta
                    </button>
                  )}
                  {s.encuestaRespondida && <p className="text-sm font-medium text-niebla">Encuesta respondida. Gracias.</p>}
                  {!s.puedeIngresar && !s.ingresoEn && (
                    <p className="text-sm text-niebla">{s.estado === 'Terminada' ? 'No registraste asistencia.' : 'Podrás ingresar 30 minutos antes.'}</p>
                  )}
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      <Hoja abierta={encuesta !== null} titulo="Encuesta de percepción" subtitulo={encuesta?.nombre} onCerrar={() => setEncuesta(null)}>
        {encuesta && (
          <FormEncuesta
            sesion={encuesta}
            alTerminar={() => {
              setEncuesta(null)
              setAviso({ tono: 'info', texto: 'Gracias. Tus respuestas quedaron guardadas.' })
              void cliente.invalidateQueries({ queryKey: claves.misSesiones })
            }}
          />
        )}
      </Hoja>
    </>
  )
}

function FormEncuesta({ sesion, alTerminar }: { sesion: MiSesion; alTerminar: () => void }) {
  const preguntas = usePreguntas()
  const [respuestas, setRespuestas] = useState<(number | null)[]>([null, null, null, null, null])
  const [comentario, setComentario] = useState('')
  const enviar = useMutation({
    mutationFn: () => api(`/api/sesiones/${sesion.id}/encuesta`, { metodo: 'POST', cuerpo: { respuestas, comentario } }),
    onSuccess: alTerminar,
  })
  const pendientes = enviar.error instanceof ApiError ? enviar.error.campos : []

  if (preguntas.isPending) return <Cargando />
  if (preguntas.error) return <ErrorCarga error={preguntas.error} />

  return (
    <form
      noValidate
      onSubmit={(e: FormEvent) => {
        e.preventDefault()
        enviar.mutate()
      }}
      className="grid gap-5"
    >
      <p className="text-sm text-niebla">De 1 (nada) a 5 (totalmente). Todas las preguntas son obligatorias, menos el comentario.</p>
      {preguntas.data.map((p, i) => {
        const pendiente = pendientes.includes(p.clave)
        return (
          <fieldset key={p.clave} aria-describedby={pendiente ? `${p.clave}-error` : undefined}>
            <legend className="mb-2 text-sm font-medium">
              {i + 1}. {p.texto}
            </legend>
            <div className="grid grid-cols-5 gap-1.5">
              {[1, 2, 3, 4, 5].map((v) => (
                <label
                  key={v}
                  className={`flex min-h-11 cursor-pointer items-center justify-center rounded-control border text-sm font-semibold ${
                    respuestas[i] === v ? 'border-tinta bg-tinta text-papel' : pendiente ? 'border-arcilla bg-papel' : 'border-duna bg-papel'
                  }`}
                >
                  <input
                    type="radio"
                    name={p.clave}
                    className="sr-only"
                    checked={respuestas[i] === v}
                    onChange={() => setRespuestas(respuestas.map((r, j) => (j === i ? v : r)))}
                  />
                  {v}
                </label>
              ))}
            </div>
            {pendiente && <ErrorCampo id={`${p.clave}-error`}>Responde esta pregunta.</ErrorCampo>}
          </fieldset>
        )
      })}
      <div>
        <label htmlFor="comentario" className="mb-1.5 block text-sm font-medium">
          Comentario <span className="font-normal text-niebla">(opcional)</span>
        </label>
        <textarea id="comentario" rows={3} maxLength={1000} className="campo resize-y" value={comentario} onChange={(e) => setComentario(e.target.value)} />
      </div>
      {enviar.error && <Aviso>{mensajeDeError(enviar.error)}</Aviso>}
      <button type="submit" className="boton-primario" disabled={enviar.isPending}>
        {enviar.isPending ? 'Guardando…' : 'Enviar encuesta'}
      </button>
    </form>
  )
}
