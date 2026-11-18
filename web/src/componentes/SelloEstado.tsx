import type { EstadoEstrategia } from '../lib/tipos'
import { Icono } from './Icono'

/** RQ_03: la estrategia se edita hasta su primer estudiante; desde ahí queda sellada. */
export function SelloEstado({ estado, animar = false }: { estado: EstadoEstrategia; animar?: boolean }) {
  if (estado === 'Editable')
    return (
      <span className="inline-flex items-center gap-1.5 rounded-full border border-duna px-2.5 py-0.5 text-xs font-medium text-niebla">
        <span aria-hidden className="size-1.5 rounded-full bg-terracota" />
        Editable
      </span>
    )

  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full bg-tinta px-2.5 py-0.5 text-xs font-medium text-papel ${
        animar ? 'animate-sellar' : ''
      }`}
    >
      <Icono nombre="candado" className="size-3" />
      Solo consulta
    </span>
  )
}
