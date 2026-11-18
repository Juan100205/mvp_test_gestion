import { useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { useParams } from 'react-router'
import { CicloSemanas } from '../componentes/CicloSemanas'
import { Aviso, Cargando, ErrorCampo, ErrorCarga, Volver } from '../componentes/Estado'
import { Hoja } from '../componentes/Hoja'
import { Icono } from '../componentes/Icono'
import { mensajeDeError } from '../lib/api'
import { claves, enviarOEncolar, useAcompanamientos, useEstrategia } from '../lib/consultas'
import { mesActual, mesesRecientes, nombreMes, semanaDelMes } from '../lib/periodo'
import type { Acompanamiento, Docente, Seguimiento, TipoAcompanamiento } from '../lib/tipos'

type Elegido = { acompanamiento: Acompanamiento; semana: number }

/** CU-03 y HU-07: acompañamiento mensual, seguimiento semanal e historial por docente. */
export function Acompanamientos() {
  const estrategiaId = Number(useParams().id)
  const estrategia = useEstrategia(estrategiaId)
  const lista = useAcompanamientos(estrategiaId)
  const cliente = useQueryClient()
  const [mes, setMes] = useState(mesActual)
  const [docenteId, setDocenteId] = useState<number | null>(null)
  const [registrando, setRegistrando] = useState(false)
  const [elegido, setElegido] = useState<Elegido | null>(null)
  const [aviso, setAviso] = useState<{ tono: 'error' | 'info'; texto: string } | null>(null)
  const [destacado, setDestacado] = useState<string | null>(null)

  if (estrategia.isPending || lista.isPending) return <Cargando />
  if (estrategia.error) return <ErrorCarga error={estrategia.error} reintentar={() => void estrategia.refetch()} />
  if (lista.error) return <ErrorCarga error={lista.error} reintentar={() => void lista.refetch()} />

  const visibles = lista.data.filter((a) =>
    docenteId === null ? a.periodo === mes : a.docentes.some((d) => d.id === docenteId),
  )
  const actualizarCache = (fn: (actual: Acompanamiento[]) => Acompanamiento[]) =>
    cliente.setQueryData<Acompanamiento[]>(claves.acompanamientos(estrategiaId), (actual = []) => fn(actual))

  function alRegistrar(resultado: Acompanamiento, encolado: boolean) {
    setRegistrando(false)
    setDocenteId(null)
    setMes(resultado.periodo)
    setDestacado(resultado.id)
    actualizarCache((actual) => [resultado, ...actual.filter((a) => a.id !== resultado.id)])
    if (!encolado) void cliente.invalidateQueries({ queryKey: claves.estrategias, exact: true })
    setAviso(
      encolado
        ? { tono: 'info', texto: 'Sin señal. El acompañamiento quedó guardado en este dispositivo y se enviará cuando vuelva la conexión.' }
        : resultado.existente
          ? { tono: 'info', texto: `${resultado.tipo === 'Grupal' ? 'Ese grupo' : resultado.docentes[0]?.nombre ?? 'Ese docente'} ya tiene un acompañamiento en ${nombreMes(resultado.periodo)}. No se creó otro: registra el seguimiento en el que aparece resaltado.` }
          : null,
    )
  }

  function alGuardarSeguimiento(resultado: Acompanamiento, encolado: boolean) {
    setElegido(null)
    actualizarCache((actual) => actual.map((a) => (a.id === resultado.id ? resultado : a)))
    setAviso(encolado ? { tono: 'info', texto: 'Sin señal. El seguimiento quedó guardado en este dispositivo y se enviará cuando vuelva la conexión.' } : null)
  }

  return (
    <>
      <Volver a={`/estrategias/${estrategiaId}`} texto={estrategia.data.nombre} />
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold sm:text-3xl">Acompañamiento pedagógico</h1>
          <p className="mt-1.5 text-sm text-niebla">Un acompañamiento por docente o grupo cada mes, con seguimiento semanal.</p>
        </div>
        <button type="button" className="boton-acento" onClick={() => setRegistrando(true)}>
          <Icono nombre="mas" />
          Registrar acompañamiento
        </button>
      </header>

      <div className="mt-6 grid gap-3 sm:grid-cols-2 sm:gap-4 lg:max-w-2xl">
        <div>
          <label htmlFor="mes" className="mb-1.5 block text-sm font-medium">
            Mes
          </label>
          <select id="mes" className="campo" value={mes} disabled={docenteId !== null} onChange={(e) => setMes(e.target.value)}>
            {[...new Set([mes, ...mesesRecientes(6)])].map((m) => (
              <option key={m} value={m}>
                {nombreMes(m)}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label htmlFor="filtro-docente" className="mb-1.5 block text-sm font-medium">
            Historial del docente
          </label>
          <select
            id="filtro-docente"
            className="campo"
            value={docenteId ?? ''}
            onChange={(e) => setDocenteId(e.target.value ? Number(e.target.value) : null)}
          >
            <option value="">Todos, solo el mes elegido</option>
            {estrategia.data.docentes.map((d) => (
              <option key={d.id} value={d.id}>
                {d.nombre}
              </option>
            ))}
          </select>
        </div>
      </div>

      {aviso && (
        <div className="mt-6">
          <Aviso tono={aviso.tono}>{aviso.texto}</Aviso>
        </div>
      )}

      {visibles.length === 0 ? (
        <div className="mt-8 border-y border-duna py-12 text-center">
          <p className="font-display text-lg font-semibold">
            {docenteId === null ? `Sin acompañamientos en ${nombreMes(mes)}` : 'Este docente aún no tiene acompañamientos'}
          </p>
          <p className="mx-auto mt-2 max-w-sm text-sm text-niebla">
            Para crear el de este mes, usa «Registrar acompañamiento».
          </p>
        </div>
      ) : (
        <ul className="mt-8 divide-y divide-duna border-y border-duna">
          {visibles.map((a) => (
            <li
              key={a.id}
              className={`py-5 transition-colors ${destacado === a.id ? '-mx-3 rounded-control bg-duna-suave/60 px-3' : ''}`}
            >
              <div className="flex flex-wrap items-start justify-between gap-x-6 gap-y-2">
                <div className="min-w-0">
                  <p className="text-xs text-niebla">
                    {a.tipo === 'Grupal' ? 'Grupal' : 'Individual'}
                    {docenteId !== null && `, ${nombreMes(a.periodo)}`}
                    {a.pendiente && <span className="ml-2 font-medium text-cielo">Pendiente de enviar</span>}
                  </p>
                  <p className="mt-0.5 font-display text-lg font-semibold">{a.docentes.map((d) => d.nombre).join(', ')}</p>
                  <p className="mt-1 max-w-prose text-sm">{a.objetivo}</p>
                  {a.compromisos && <p className="mt-1 max-w-prose text-sm text-niebla">Compromisos: {a.compromisos}</p>}
                </div>
                <p className="text-right">
                  <span className="cifra text-3xl font-semibold">{a.avanceActual}</span>
                  <span className="text-sm text-niebla"> % de avance</span>
                </p>
              </div>
              <div className="mt-4">
                <CicloSemanas
                  seguimientos={a.seguimientos}
                  semanaActual={a.periodo === mesActual() ? semanaDelMes() : undefined}
                  onElegir={(semana) => setElegido({ acompanamiento: a, semana })}
                />
              </div>
            </li>
          ))}
        </ul>
      )}

      <Hoja
        abierta={registrando}
        titulo="Registrar acompañamiento"
        subtitulo={`Mes de ${nombreMes(mesActual())}`}
        onCerrar={() => setRegistrando(false)}
      >
        {registrando && <FormAcompanamiento estrategiaId={estrategiaId} docentes={estrategia.data.docentes} alGuardar={alRegistrar} />}
      </Hoja>

      <Hoja
        abierta={elegido !== null}
        titulo={elegido ? `Seguimiento de la semana ${elegido.semana}` : ''}
        subtitulo={elegido ? elegido.acompanamiento.docentes.map((d) => d.nombre).join(', ') : undefined}
        onCerrar={() => setElegido(null)}
      >
        {elegido && <FormSeguimiento {...elegido} alGuardar={alGuardarSeguimiento} />}
      </Hoja>
    </>
  )
}

function FormAcompanamiento({
  estrategiaId,
  docentes,
  alGuardar,
}: {
  estrategiaId: number
  docentes: Docente[]
  alGuardar: (a: Acompanamiento, encolado: boolean) => void
}) {
  const [tipo, setTipo] = useState<TipoAcompanamiento>('Individual')
  const [elegidos, setElegidos] = useState<number[]>([])
  const [objetivo, setObjetivo] = useState('')
  const [compromisos, setCompromisos] = useState('')
  const [errores, setErrores] = useState<{ docentes?: string; objetivo?: string }>({})
  const [error, setError] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function alEnviar(evento: FormEvent) {
    evento.preventDefault()
    const faltan = {
      docentes:
        tipo === 'Individual'
          ? elegidos.length !== 1 ? 'Elige el docente que acompañas.' : undefined
          : elegidos.length < 2 ? 'Un acompañamiento grupal necesita al menos dos docentes.' : undefined,
      objetivo: objetivo.trim() ? undefined : 'Escribe el objetivo del acompañamiento.',
    }
    setErrores(faltan)
    if (faltan.docentes || faltan.objetivo) return

    const id = crypto.randomUUID()
    const cuerpo = { id, estrategiaId, tipo, periodo: mesActual(), objetivo: objetivo.trim(), compromisos: compromisos.trim(), docenteIds: [...elegidos].sort((a, b) => a - b) }
    setEnviando(true)
    setError(null)
    try {
      const resultado = await enviarOEncolar<Acompanamiento>({
        clave: `acomp:${id}`,
        metodo: 'POST',
        ruta: '/api/acompanamientos',
        cuerpo,
        descripcion: `Acompañamiento ${tipo.toLowerCase()} de ${nombreMes(cuerpo.periodo)}`,
      })
      if (resultado.encolada)
        alGuardar(
          {
            ...cuerpo,
            docentes: docentes.filter((d) => elegidos.includes(d.id)),
            avanceActual: 0,
            seguimientos: [],
            registradoEn: new Date().toISOString(),
            existente: false,
            pendiente: true,
          },
          true,
        )
      else alGuardar(resultado.datos, false)
    } catch (e) {
      setError(mensajeDeError(e))
      setEnviando(false)
    }
  }

  return (
    <form noValidate onSubmit={alEnviar} className="grid gap-5">
      <fieldset>
        <legend className="mb-2 text-sm font-medium">Tipo</legend>
        <div className="grid grid-cols-2 gap-2">
          {(['Individual', 'Grupal'] as const).map((t) => (
            <label
              key={t}
              className={`flex min-h-11 cursor-pointer items-center justify-center gap-2 rounded-control border px-3 text-sm font-medium ${
                tipo === t ? 'border-tinta bg-tinta text-papel' : 'border-duna bg-papel'
              }`}
            >
              <input
                type="radio"
                name="tipo"
                className="sr-only"
                checked={tipo === t}
                onChange={() => {
                  setTipo(t)
                  setElegidos([])
                }}
              />
              {t}
            </label>
          ))}
        </div>
      </fieldset>

      <fieldset aria-describedby={errores.docentes ? 'acomp-docentes-error' : undefined}>
        <legend className="mb-2 text-sm font-medium">{tipo === 'Individual' ? 'Docente' : 'Docentes del grupo'}</legend>
        <ul className={`divide-y divide-duna rounded-control border ${errores.docentes ? 'border-arcilla' : 'border-duna'}`}>
          {docentes.map((d) => (
            <li key={d.id}>
              <label className="flex min-h-11 cursor-pointer items-center gap-3 px-3 py-2.5">
                <input
                  type={tipo === 'Individual' ? 'radio' : 'checkbox'}
                  name="docentes"
                  className="size-4 accent-tinta"
                  checked={elegidos.includes(d.id)}
                  onChange={(e) =>
                    setElegidos(
                      tipo === 'Individual' ? [d.id] : e.target.checked ? [...elegidos, d.id] : elegidos.filter((x) => x !== d.id),
                    )
                  }
                />
                <span className="text-sm font-medium">{d.nombre}</span>
              </label>
            </li>
          ))}
        </ul>
        {errores.docentes && <ErrorCampo id="acomp-docentes-error">{errores.docentes}</ErrorCampo>}
      </fieldset>

      <div>
        <label htmlFor="objetivo" className="mb-1.5 block text-sm font-medium">
          Objetivo
        </label>
        <textarea
          id="objetivo"
          rows={2}
          maxLength={500}
          className="campo resize-y"
          value={objetivo}
          onChange={(e) => setObjetivo(e.target.value)}
          aria-invalid={errores.objetivo ? true : undefined}
          aria-describedby={errores.objetivo ? 'objetivo-error' : undefined}
        />
        {errores.objetivo && <ErrorCampo id="objetivo-error">{errores.objetivo}</ErrorCampo>}
      </div>

      <div>
        <label htmlFor="compromisos" className="mb-1.5 block text-sm font-medium">
          Compromisos <span className="font-normal text-niebla">(opcional)</span>
        </label>
        <textarea id="compromisos" rows={2} maxLength={1000} className="campo resize-y" value={compromisos} onChange={(e) => setCompromisos(e.target.value)} />
      </div>

      {error && <Aviso>{error}</Aviso>}
      <button type="submit" className="boton-primario" disabled={enviando}>
        {enviando ? 'Guardando…' : 'Guardar acompañamiento'}
      </button>
    </form>
  )
}

function FormSeguimiento({
  acompanamiento,
  semana,
  alGuardar,
}: Elegido & { alGuardar: (a: Acompanamiento, encolado: boolean) => void }) {
  const previo = acompanamiento.seguimientos.find((s) => s.semana === semana)
  const anterior = [...acompanamiento.seguimientos].filter((s) => s.semana < semana).sort((a, b) => b.semana - a.semana)[0]
  const [actividades, setActividades] = useState(previo?.actividades ?? '')
  const [avance, setAvance] = useState(previo?.avance ?? anterior?.avance ?? 0)
  const [observaciones, setObservaciones] = useState(previo?.observaciones ?? '')
  const [faltaActividades, setFaltaActividades] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  async function alEnviar(evento: FormEvent) {
    evento.preventDefault()
    setFaltaActividades(!actividades.trim())
    if (!actividades.trim()) return

    const cuerpo = { id: previo?.id ?? crypto.randomUUID(), actividades: actividades.trim(), avance, observaciones: observaciones.trim() }
    setEnviando(true)
    setError(null)
    try {
      const resultado = await enviarOEncolar<Acompanamiento>({
        clave: `seg:${acompanamiento.id}:${semana}`,
        metodo: 'PUT',
        ruta: `/api/acompanamientos/${acompanamiento.id}/seguimientos/${semana}`,
        cuerpo,
        descripcion: `Seguimiento de la semana ${semana} (${acompanamiento.docentes.map((d) => d.nombre).join(', ')})`,
      })
      if (resultado.encolada) {
        const seguimiento: Seguimiento = { ...cuerpo, semana, actualizadoEn: new Date().toISOString(), pendiente: true }
        const seguimientos = [...acompanamiento.seguimientos.filter((s) => s.semana !== semana), seguimiento].sort((a, b) => a.semana - b.semana)
        alGuardar({ ...acompanamiento, seguimientos, avanceActual: seguimientos.at(-1)!.avance }, true)
      } else alGuardar(resultado.datos, false)
    } catch (e) {
      setError(mensajeDeError(e))
      setEnviando(false)
    }
  }

  return (
    <form noValidate onSubmit={alEnviar} className="grid gap-5">
      <div>
        <label htmlFor="actividades" className="mb-1.5 block text-sm font-medium">
          Actividades de la semana
        </label>
        <textarea
          id="actividades"
          rows={3}
          maxLength={1000}
          className="campo resize-y"
          value={actividades}
          onChange={(e) => setActividades(e.target.value)}
          aria-invalid={faltaActividades || undefined}
          aria-describedby={faltaActividades ? 'actividades-error' : undefined}
        />
        {faltaActividades && <ErrorCampo id="actividades-error">Describe las actividades realizadas.</ErrorCampo>}
      </div>

      <div>
        <div className="mb-1.5 flex items-baseline justify-between">
          <label htmlFor="avance" className="text-sm font-medium">
            Avance acumulado
          </label>
          <output htmlFor="avance" className="cifra text-2xl font-semibold">
            {avance} %
          </output>
        </div>
        <input
          id="avance"
          type="range"
          min={0}
          max={100}
          step={5}
          value={avance}
          onChange={(e) => setAvance(Number(e.target.value))}
          className="w-full accent-terracota"
        />
      </div>

      <div>
        <label htmlFor="observaciones" className="mb-1.5 block text-sm font-medium">
          Observaciones <span className="font-normal text-niebla">(opcional)</span>
        </label>
        <textarea id="observaciones" rows={2} maxLength={1000} className="campo resize-y" value={observaciones} onChange={(e) => setObservaciones(e.target.value)} />
      </div>

      {previo && <p className="text-xs text-niebla">Esta semana ya tenía registro: al guardar se actualiza.</p>}
      {error && <Aviso>{error}</Aviso>}
      <button type="submit" className="boton-primario" disabled={enviando}>
        {enviando ? 'Guardando…' : 'Guardar seguimiento'}
      </button>
    </form>
  )
}
