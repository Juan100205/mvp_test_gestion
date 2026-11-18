import { useState, type FormEvent } from 'react'
import { Cargando, ErrorCarga } from '../componentes/Estado'
import { useAuditoria } from '../lib/consultas'
import { aCsv, descargar, describirOperacion, operaciones } from '../lib/formato'
import { fechaCorta } from '../lib/periodo'

interface Filtros {
  usuario: string
  operacion: string
  desde: string
  hasta: string
}

const vacios: Filtros = { usuario: '', operacion: '', desde: '', hasta: '' }

/** Fecha del selector (día local) a ISO con zona horaria, para que el API filtre el día completo. */
const inicioDelDia = (dia: string) => (dia ? new Date(`${dia}T00:00:00`).toISOString() : '')
const finDelDia = (dia: string) => (dia ? new Date(`${dia}T23:59:59.999`).toISOString() : '')

/** CU-06 pasos 4-5 y flujo alterno 5A: consulta de la trazabilidad por el administrador. */
export function Trazabilidad() {
  const [borrador, setBorrador] = useState<Filtros>(vacios)
  const [filtros, setFiltros] = useState<Filtros>(vacios)
  const { data, error, isPending, isFetching, refetch } = useAuditoria({
    usuario: filtros.usuario.trim().toLowerCase(),
    operacion: filtros.operacion,
    desde: inicioDelDia(filtros.desde),
    hasta: finDelDia(filtros.hasta),
  })

  const aplicar = (evento: FormEvent) => {
    evento.preventDefault()
    setFiltros(borrador)
  }
  const cambiar = (campo: keyof Filtros) => (e: { target: { value: string } }) => setBorrador((b) => ({ ...b, [campo]: e.target.value }))

  return (
    <>
      <header>
        <h1 className="text-2xl font-semibold sm:text-3xl">Trazabilidad</h1>
        <p className="mt-1.5 max-w-prose text-sm text-niebla">
          Cada ingreso, creación y modificación queda registrado con su usuario y su fecha. Se muestran los 200 más recientes.
        </p>
      </header>

      <form onSubmit={aplicar} className="mt-6 grid gap-3 rounded-control border border-duna bg-papel p-4 sm:grid-cols-2 lg:grid-cols-[1fr_1.3fr_auto_auto_auto] lg:items-end">
        <div>
          <label htmlFor="f-usuario" className="mb-1.5 block text-sm font-medium">
            Usuario
          </label>
          <input id="f-usuario" className="campo" autoCapitalize="none" spellCheck={false} value={borrador.usuario} onChange={cambiar('usuario')} />
        </div>
        <div>
          <label htmlFor="f-operacion" className="mb-1.5 block text-sm font-medium">
            Operación
          </label>
          <select id="f-operacion" className="campo" value={borrador.operacion} onChange={cambiar('operacion')}>
            <option value="">Todas</option>
            {Object.entries(operaciones).map(([codigo, texto]) => (
              <option key={codigo} value={codigo}>
                {texto}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label htmlFor="f-desde" className="mb-1.5 block text-sm font-medium">
            Desde
          </label>
          <input id="f-desde" type="date" className="campo" value={borrador.desde} onChange={cambiar('desde')} />
        </div>
        <div>
          <label htmlFor="f-hasta" className="mb-1.5 block text-sm font-medium">
            Hasta
          </label>
          <input id="f-hasta" type="date" className="campo" value={borrador.hasta} min={borrador.desde || undefined} onChange={cambiar('hasta')} />
        </div>
        <button type="submit" className="boton-primario sm:col-span-2 lg:col-span-1">
          Aplicar filtros
        </button>
      </form>

      <div className="mt-6 flex items-center justify-between gap-3">
        <p className="text-sm text-niebla" aria-live="polite">
          {isPending ? '' : isFetching ? 'Actualizando…' : data ? `${data.length} ${data.length === 1 ? 'registro' : 'registros'}` : ''}
        </p>
        <button
          type="button"
          className="boton-secundario"
          disabled={!data || data.length === 0}
          onClick={() => data && descargar(`trazabilidad-sses-${new Date().toISOString().slice(0, 10)}.csv`, aCsv(data))}
        >
          Descargar CSV
        </button>
      </div>

      <section className="mt-3">
        {isPending ? (
          <Cargando />
        ) : error ? (
          <ErrorCarga error={error} reintentar={() => void refetch()} />
        ) : data.length === 0 ? (
          <div className="border-y border-duna py-12 text-center">
            <p className="font-display text-lg font-semibold">No hay registros con esos filtros</p>
            <p className="mt-2 text-sm text-niebla">Amplía el rango de fechas o quita algún filtro.</p>
          </div>
        ) : (
          <table className="w-full border-y border-duna text-sm">
            <thead className="sr-only sm:not-sr-only">
              <tr className="border-b border-duna text-left text-niebla">
                <th scope="col" className="py-2.5 pr-4 font-medium">Fecha</th>
                <th scope="col" className="py-2.5 pr-4 font-medium">Usuario</th>
                <th scope="col" className="py-2.5 pr-4 font-medium">Operación</th>
                <th scope="col" className="py-2.5 font-medium">Detalle</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-duna">
              {data.map((r) => (
                <tr key={r.id} className="grid grid-cols-[1fr_auto] gap-x-4 py-3 sm:table-row sm:py-0">
                  <td className="text-xs text-niebla sm:py-3 sm:pr-4 sm:text-sm sm:whitespace-nowrap">{fechaCorta(r.fecha)}</td>
                  <td className="text-right text-xs font-medium sm:py-3 sm:pr-4 sm:text-left sm:text-sm">{r.usuario}</td>
                  <td className={`col-span-2 mt-1 font-medium sm:mt-0 sm:py-3 sm:pr-4 ${r.operacion.startsWith('auth.f') || r.operacion === 'auth.bloqueo' ? 'text-arcilla' : ''}`}>
                    {describirOperacion(r.operacion)}
                  </td>
                  <td className="col-span-2 text-niebla sm:py-3">
                    {r.detalle ?? ''}
                    {r.recursoId && <span className="text-ceniza"> ({r.recurso} {r.recursoId})</span>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </>
  )
}
