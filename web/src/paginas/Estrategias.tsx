import { Link } from 'react-router'
import { Cargando, ErrorCarga } from '../componentes/Estado'
import { Icono } from '../componentes/Icono'
import { SelloEstado } from '../componentes/SelloEstado'
import { useEstrategias } from '../lib/consultas'
import { plural } from '../lib/formato'
import { useSesion } from '../lib/sesion'

/** CU-01: las estrategias del tutor, la más reciente primero. */
export function Estrategias() {
  const { sesion } = useSesion()
  const { data, error, isPending, refetch } = useEstrategias()

  return (
    <>
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-sm text-niebla">Región {sesion?.usuario.region}</p>
          <h1 className="mt-1 text-2xl font-semibold sm:text-3xl">Tus estrategias</h1>
        </div>
        <Link to="/estrategias/nueva" className="boton-acento">
          <Icono nombre="mas" />
          Nueva estrategia
        </Link>
      </header>

      <section className="mt-8">
        {isPending ? (
          <Cargando />
        ) : error ? (
          <ErrorCarga error={error} reintentar={() => void refetch()} />
        ) : data.length === 0 ? (
          <div className="border-y border-duna py-14 text-center">
            <p className="font-display text-lg font-semibold">No tienes estrategias registradas</p>
            <p className="mx-auto mt-2 max-w-sm text-sm text-niebla">
              Crea la primera con «Nueva estrategia». Necesitas la entidad educativa, el periodo y al menos un docente.
            </p>
          </div>
        ) : (
          <ul className="divide-y divide-duna border-y border-duna">
            {data.map((e) => (
              <li key={e.id}>
                <Link
                  to={`/estrategias/${e.id}`}
                  className="group grid gap-x-6 gap-y-2 py-4 sm:grid-cols-[1fr_auto] sm:items-center"
                >
                  <div className="min-w-0">
                    <p className="truncate font-display text-lg font-semibold group-hover:text-arcilla">{e.nombre}</p>
                    <p className="mt-0.5 truncate text-sm text-niebla">
                      {e.entidad}, periodo {e.periodo}
                    </p>
                  </div>
                  <div className="flex flex-wrap items-center gap-x-4 gap-y-2 text-sm text-niebla">
                    <span>{plural(e.totalEstudiantes, 'estudiante')}</span>
                    <span>{plural(e.totalDocentes, 'docente')}</span>
                    <span>{plural(e.totalAcompanamientos, 'acompañamiento')}</span>
                    <SelloEstado estado={e.estado} />
                  </div>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </section>
    </>
  )
}
