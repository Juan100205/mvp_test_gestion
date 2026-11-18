import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { mensajeDeError } from '../lib/api'
import { Icono } from './Icono'

export function Cargando({ texto = 'Cargando…' }: { texto?: string }) {
  return (
    <p role="status" className="py-16 text-center text-sm text-niebla">
      {texto}
    </p>
  )
}

export function ErrorCarga({ error, reintentar }: { error: unknown; reintentar?: () => void }) {
  return (
    <div role="alert" className="rounded-control border-l-[3px] border-arcilla bg-[#fbeee6] px-4 py-3 text-sm">
      <p>{mensajeDeError(error)}</p>
      {reintentar && (
        <button type="button" onClick={reintentar} className="boton-texto -ml-2 mt-1">
          Intentar de nuevo
        </button>
      )}
    </div>
  )
}

export function Aviso({ tono = 'error', children }: { tono?: 'error' | 'info'; children: ReactNode }) {
  return (
    <p
      role={tono === 'error' ? 'alert' : 'status'}
      className={`rounded-control border-l-[3px] px-3 py-2.5 text-sm ${
        tono === 'error' ? 'border-arcilla bg-[#fbeee6]' : 'border-cielo bg-cielo-suave'
      }`}
    >
      {children}
    </p>
  )
}

export function Volver({ a, texto }: { a: string; texto: string }) {
  return (
    <Link to={a} className="-ml-1 mb-4 inline-flex items-center gap-1 text-sm font-medium text-niebla hover:text-tinta">
      <Icono nombre="atras" className="size-4" />
      {texto}
    </Link>
  )
}

export function ErrorCampo({ id, children }: { id: string; children: ReactNode }) {
  return (
    <p id={id} className="mt-1.5 text-sm text-arcilla">
      {children}
    </p>
  )
}
