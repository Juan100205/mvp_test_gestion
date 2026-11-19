import { useQueryClient } from '@tanstack/react-query'
import { NavLink, Outlet } from 'react-router'
import { useConexion } from '../lib/conexion'
import { descartarRechazadas } from '../lib/colaOffline'
import { useSesion } from '../lib/sesion'
import { nombreRol } from '../lib/tipos'
import { Icono, type NombreIcono } from './Icono'

function EstadoConexion() {
  const { enLinea, porEnviar, sincronizando } = useConexion()

  const texto = sincronizando
    ? `Enviando ${porEnviar}…`
    : !enLinea
      ? porEnviar > 0
        ? `Sin señal, ${porEnviar} por enviar`
        : 'Sin señal'
      : porEnviar > 0
        ? `${porEnviar} por enviar`
        : 'En línea'

  const sinSenal = !enLinea || porEnviar > 0
  return (
    <p
      role="status"
      className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium transition-colors ${
        sinSenal ? 'bg-cielo text-papel' : 'text-ceniza'
      }`}
    >
      <Icono nombre={enLinea ? 'senal' : 'sinSenal'} className="size-3.5" />
      {texto}
    </p>
  )
}

function AvisoRechazos() {
  const { rechazos } = useConexion()
  if (rechazos.length === 0) return null
  return (
    <div role="alert" className="border-b border-duna bg-[#fbeee6] px-5 py-3 text-sm sm:px-8">
      <p className="font-medium">
        {rechazos.length === 1 ? 'Un registro guardado sin señal no se pudo enviar' : `${rechazos.length} registros guardados sin señal no se pudieron enviar`}
      </p>
      <ul className="mt-1 list-disc pl-5 text-niebla">
        {rechazos.map((r) => (
          <li key={r.clave + r.rechazadaEn}>
            {r.descripcion}: {r.error}
          </li>
        ))}
      </ul>
      <button type="button" className="boton-texto -ml-2 mt-1" onClick={() => void descartarRechazadas()}>
        Ocultar aviso
      </button>
    </div>
  )
}

export function Shell() {
  const { sesion, cerrar } = useSesion()
  const cliente = useQueryClient()
  if (!sesion) return null
  const { usuario } = sesion

  const menu: Record<typeof usuario.rol, { a: string; texto: string; icono: NombreIcono }[]> = {
    Tutor: [
      { a: '/estrategias', texto: 'Estrategias', icono: 'estrategias' },
      { a: '/mis-sesiones', texto: 'Mis sesiones', icono: 'sesiones' },
    ],
    Administrador: [
      { a: '/trazabilidad', texto: 'Trazabilidad', icono: 'registro' },
      { a: '/reportes', texto: 'Reportes', icono: 'reporte' },
    ],
    Funcionario: [{ a: '/reportes', texto: 'Reportes', icono: 'reporte' }],
    EnteControl: [{ a: '/reportes', texto: 'Reportes', icono: 'reporte' }],
    LiderPilotaje: [{ a: '/sesiones', texto: 'Sesiones de pilotaje', icono: 'sesiones' }],
  }
  const enlaces = menu[usuario.rol]

  return (
    <div className="min-h-dvh lg:grid lg:grid-cols-[15rem_1fr]">
      <aside className="sticky top-0 z-20 bg-tinta text-papel lg:h-dvh">
        <div className="flex items-center justify-between gap-3 px-5 py-3 lg:h-full lg:flex-col lg:items-stretch lg:justify-start lg:px-5 lg:py-7">
          <p className="flex items-center gap-2 font-display text-lg font-semibold tracking-tight">
            <img src="/favicon.svg" alt="" className="size-7" />
            SSES
          </p>

          <nav aria-label="Principal" className="hidden lg:mt-10 lg:block">
            <ul className="space-y-1">
              {enlaces.map((e) => (
                <li key={e.a}>
                  <NavLink
                    to={e.a}
                    className={({ isActive }) =>
                      `flex items-center gap-3 rounded-control px-3 py-2.5 text-sm font-medium transition-colors ${
                        isActive ? 'bg-pizarra text-papel' : 'text-ceniza hover:text-papel'
                      }`
                    }
                  >
                    <Icono nombre={e.icono} />
                    {e.texto}
                  </NavLink>
                </li>
              ))}
            </ul>
          </nav>

          <div className="flex items-center gap-2 lg:mt-auto lg:flex-col lg:items-stretch lg:gap-4">
            <EstadoConexion />
            <div className="flex items-center gap-3 lg:border-t lg:border-pizarra lg:pt-4">
              <div className="hidden min-w-0 lg:block">
                <p className="truncate text-sm font-medium">{usuario.nombre}</p>
                <p className="truncate text-xs text-ceniza">
                  {nombreRol[usuario.rol]}, {usuario.region}
                </p>
              </div>
              <button
                type="button"
                onClick={() => {
                  cliente.clear()
                  cerrar()
                }}
                className="ml-auto rounded-control p-2 text-ceniza hover:bg-pizarra hover:text-papel"
              >
                <Icono nombre="salir" />
                <span className="sr-only">Cerrar sesión</span>
              </button>
            </div>
          </div>
        </div>
      </aside>

      <div className="min-w-0">
        {enlaces.length > 1 && (
          <nav aria-label="Secciones" className="sticky top-[3.25rem] z-10 border-b border-duna bg-arena lg:hidden">
            <ul className="flex gap-1 overflow-x-auto px-3 py-2">
              {enlaces.map((e) => (
                <li key={e.a}>
                  <NavLink
                    to={e.a}
                    className={({ isActive }) =>
                      `flex min-h-11 items-center gap-2 rounded-control px-3 text-sm font-medium ${
                        isActive ? 'bg-tinta text-papel' : 'text-niebla hover:text-tinta'
                      }`
                    }
                  >
                    <Icono nombre={e.icono} className="size-4" />
                    {e.texto}
                  </NavLink>
                </li>
              ))}
            </ul>
          </nav>
        )}
        <AvisoRechazos />
        <main className="mx-auto max-w-5xl px-5 pb-16 pt-6 sm:px-8 lg:pt-10">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
