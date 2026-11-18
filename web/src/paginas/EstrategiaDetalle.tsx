import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import { Aviso, Cargando, ErrorCarga, Volver } from '../componentes/Estado'
import { Icono } from '../componentes/Icono'
import { SelloEstado } from '../componentes/SelloEstado'
import { api, mensajeDeError } from '../lib/api'
import { claves, useEstrategia } from '../lib/consultas'
import { enmascararDocumento, plural } from '../lib/formato'
import { fechaCorta } from '../lib/periodo'
import type { Estrategia, Estudiante } from '../lib/tipos'

export function EstrategiaDetalle() {
  const id = Number(useParams().id)
  const { data: estrategia, error, isPending, refetch } = useEstrategia(id)
  const [recienSellada, setRecienSellada] = useState(false)

  if (isPending) return <Cargando />
  if (error) return <ErrorCarga error={error} reintentar={() => void refetch()} />

  return (
    <>
      <Volver a="/estrategias" texto="Tus estrategias" />
      <header className="flex flex-wrap items-start justify-between gap-x-6 gap-y-4">
        <div className="min-w-0">
          <SelloEstado estado={estrategia.estado} animar={recienSellada} />
          <h1 className="mt-3 text-2xl font-semibold sm:text-3xl">{estrategia.nombre}</h1>
          <p className="mt-1.5 text-sm text-niebla">
            {estrategia.entidad.nombre}, {estrategia.entidad.municipio} ({estrategia.entidad.region}). Periodo{' '}
            {estrategia.periodo}.
          </p>
          {estrategia.descripcion && <p className="mt-4 max-w-prose">{estrategia.descripcion}</p>}
        </div>
        <div className="flex w-full flex-col gap-3 sm:w-auto sm:flex-row">
          {estrategia.esEditable && (
            <Link to={`/estrategias/${id}/editar`} className="boton-secundario">
              Editar
            </Link>
          )}
          <Link to={`/estrategias/${id}/acompanamientos`} className="boton-primario">
            Acompañamiento pedagógico
          </Link>
        </div>
      </header>

      <div className="mt-10 grid gap-10 lg:grid-cols-[1.6fr_1fr]">
        <Estudiantes estrategia={estrategia} alSellar={() => setRecienSellada(true)} />

        <section aria-labelledby="titulo-docentes">
          <h2 id="titulo-docentes" className="text-lg font-semibold">
            Docentes
          </h2>
          <ul className="mt-3 divide-y divide-duna border-y border-duna">
            {estrategia.docentes.map((d) => (
              <li key={d.id} className="py-3 text-sm font-medium">
                {d.nombre}
              </li>
            ))}
          </ul>
        </section>
      </div>
    </>
  )
}

/** CU-02: busca en la base precargada y vincula. El primer vínculo sella la estrategia (RQ_03). */
function Estudiantes({ estrategia, alSellar }: { estrategia: Estrategia; alSellar: () => void }) {
  const cliente = useQueryClient()
  const [documento, setDocumento] = useState('')
  const [encontrado, setEncontrado] = useState<Estudiante | null>(null)
  const [buscando, setBuscando] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [aviso, setAviso] = useState<string | null>(null)

  const vincular = useMutation({
    mutationFn: (estudiante: Estudiante) =>
      api<Estrategia>(`/api/estrategias/${estrategia.id}/estudiantes`, { metodo: 'POST', cuerpo: { estudianteId: estudiante.id } }),
    onSuccess: (actualizada, estudiante) => {
      if (estrategia.esEditable && !actualizada.esEditable) alSellar()
      cliente.setQueryData(claves.estrategia(estrategia.id), actualizada)
      void cliente.invalidateQueries({ queryKey: claves.estrategias, exact: true })
      setEncontrado(null)
      setDocumento('')
      setAviso(`Se vinculó a ${estudiante.nombre}.`)
    },
    onError: (e) => setError(mensajeDeError(e)),
  })

  async function buscar(evento: FormEvent) {
    evento.preventDefault()
    const numero = documento.trim()
    setAviso(null)
    setEncontrado(null)
    if (!numero) {
      setError('Escribe el número de documento.')
      return
    }
    setError(null)
    setBuscando(true)
    try {
      setEncontrado(await api<Estudiante>(`/api/estudiantes?documento=${encodeURIComponent(numero)}`))
    } catch (e) {
      setError(mensajeDeError(e))
    } finally {
      setBuscando(false)
    }
  }

  const yaVinculado = encontrado && estrategia.estudiantes.some((s) => s.id === encontrado.id)

  return (
    <section aria-labelledby="titulo-estudiantes">
      <div className="flex items-baseline justify-between gap-3">
        <h2 id="titulo-estudiantes" className="text-lg font-semibold">
          Estudiantes
        </h2>
        <p className="text-sm text-niebla">{plural(estrategia.estudiantes.length, 'vinculado')}</p>
      </div>

      <form onSubmit={buscar} noValidate className="mt-3 rounded-control border border-duna bg-papel p-4">
        <label htmlFor="documento" className="mb-1.5 block text-sm font-medium">
          Documento del estudiante
        </label>
        <div className="flex gap-2">
          <input
            id="documento"
            className="campo"
            inputMode="numeric"
            autoComplete="off"
            placeholder="Ej. 1100000004"
            value={documento}
            onChange={(e) => setDocumento(e.target.value)}
          />
          <button type="submit" className="boton-secundario shrink-0" disabled={buscando}>
            <Icono nombre="buscar" />
            <span>Buscar</span>
          </button>
        </div>
        {estrategia.esEditable && (
          <p className="mt-2 text-xs text-niebla">
            Al vincular el primer estudiante, la estrategia queda solo para consulta.
          </p>
        )}

        {encontrado && (
          <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-duna pt-4">
            <div>
              <p className="font-medium">{encontrado.nombre}</p>
              <p className="text-sm text-niebla">{enmascararDocumento(encontrado.tipoDocumento, encontrado.numeroDocumento)}</p>
            </div>
            {yaVinculado ? (
              <p className="text-sm text-niebla">Ya está en esta estrategia.</p>
            ) : (
              <button
                type="button"
                className="boton-acento w-full sm:w-auto"
                disabled={vincular.isPending}
                onClick={() => {
                  setError(null)
                  vincular.mutate(encontrado)
                }}
              >
                {vincular.isPending ? 'Vinculando…' : 'Vincular a la estrategia'}
              </button>
            )}
          </div>
        )}
        {error && (
          <div className="mt-4">
            <Aviso>{error}</Aviso>
          </div>
        )}
        {aviso && (
          <div className="mt-4">
            <Aviso tono="info">{aviso}</Aviso>
          </div>
        )}
      </form>

      {estrategia.estudiantes.length > 0 && (
        <ul className="mt-4 divide-y divide-duna border-y border-duna">
          {estrategia.estudiantes.map((s) => (
            <li key={s.id} className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-0.5 py-3">
              <span className="text-sm font-medium">{s.nombre}</span>
              <span className="text-xs text-niebla">
                <span>{enmascararDocumento(s.tipoDocumento, s.numeroDocumento)}</span>
                {s.vinculadoEn && <span>, desde el {fechaCorta(s.vinculadoEn)}</span>}
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
