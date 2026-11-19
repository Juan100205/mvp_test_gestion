import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { CurvasDeNivel } from '../componentes/CurvasDeNivel'
import { ApiError, mensajeDeError } from '../lib/api'
import { hora } from '../lib/periodo'
import { useSesion } from '../lib/sesion'
import { nombreRol, type Rol } from '../lib/tipos'

export const rutaInicial = (rol: Rol) =>
  rol === 'Tutor' ? '/estrategias' : rol === 'Administrador' ? '/trazabilidad' : rol === 'LiderPilotaje' ? '/sesiones' : '/reportes'

const cuentasDemo: { usuario: string; rol: Rol; quien: string }[] = [
  { usuario: 'tutor.caribe', rol: 'Tutor', quien: 'Laura Gómez, Caribe' },
  { usuario: 'tutor.pacifico', rol: 'Tutor', quien: 'Andrés Mosquera, Pacífico' },
  { usuario: 'admin.mineducacion', rol: 'Administrador', quien: 'Oficina de Tecnología' },
  { usuario: 'ente.control', rol: 'EnteControl', quien: 'Contraloría Delegada' },
]

function mensajeIngreso(error: unknown) {
  if (error instanceof ApiError && error.codigo === 'cuenta_bloqueada' && error.datos.bloqueadoHasta)
    return `Cuenta bloqueada por intentos fallidos. Podrás ingresar de nuevo a las ${hora(error.datos.bloqueadoHasta)}.`
  if (error instanceof ApiError && error.codigo === 'credenciales_invalidas') {
    const quedan = error.datos.intentosRestantes
    return quedan === undefined
      ? 'El usuario o la contraseña no son correctos.'
      : `El usuario o la contraseña no son correctos. Te quedan ${quedan} ${quedan === 1 ? 'intento' : 'intentos'} antes del bloqueo temporal.`
  }
  return mensajeDeError(error)
}

export function Login() {
  const { iniciar } = useSesion()
  const navegar = useNavigate()
  const [usuario, setUsuario] = useState('')
  const [clave, setClave] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [faltantes, setFaltantes] = useState<{ usuario?: boolean; clave?: boolean }>({})
  const [enviando, setEnviando] = useState(false)

  async function alEnviar(evento: FormEvent) {
    evento.preventDefault()
    const vacios = { usuario: !usuario.trim(), clave: !clave }
    setFaltantes(vacios)
    if (vacios.usuario || vacios.clave) return

    setEnviando(true)
    setError(null)
    try {
      const sesion = await iniciar(usuario.trim(), clave)
      navegar(rutaInicial(sesion.usuario.rol), { replace: true })
    } catch (e) {
      setError(mensajeIngreso(e))
    } finally {
      setEnviando(false)
    }
  }

  return (
    <main className="grid min-h-dvh lg:grid-cols-[1.1fr_1fr]">
      <section className="relative isolate flex flex-col justify-between overflow-hidden bg-tinta px-5 pb-8 pt-6 text-papel sm:px-10 lg:min-h-dvh lg:px-14 lg:py-12">
        <CurvasDeNivel className="absolute inset-0 -z-10 size-full text-duna" />
        <p className="flex items-center gap-2 font-display text-lg font-semibold tracking-tight">
          <img src="/favicon.svg" alt="" className="size-7" />
          SSES
        </p>
        <div className="mt-16 max-w-xl lg:mt-0">
          <h1 className="text-3xl font-semibold leading-[1.05] sm:text-4xl lg:text-[3.6rem]">
            Seguimiento a estrategias estudiantiles
          </h1>
          <p className="mt-5 max-w-md text-base text-ceniza">
            Para los tutores del piloto: registro de estrategias, vinculación de estudiantes y acompañamiento semanal a
            docentes. Sin señal también funciona: los registros se envían cuando vuelve la conexión.
          </p>
        </div>
        <p className="mt-10 hidden text-sm text-ceniza lg:block">Piloto del Ministerio de Educación Nacional, 2026</p>
      </section>

      <section className="flex items-start justify-center px-5 py-10 sm:px-10 lg:items-center">
        <div className="w-full max-w-sm">
          <h2 className="text-2xl font-semibold">Ingresar</h2>
          <p className="mt-1.5 text-sm text-niebla">Usa el usuario y la contraseña que te asignó el Ministerio.</p>

          <form noValidate onSubmit={alEnviar} className="mt-8 space-y-5">
            <div>
              <label htmlFor="usuario" className="mb-1.5 block text-sm font-medium">
                Usuario
              </label>
              <input
                id="usuario"
                className="campo"
                autoComplete="username"
                autoCapitalize="none"
                spellCheck={false}
                value={usuario}
                onChange={(e) => setUsuario(e.target.value)}
                aria-invalid={faltantes.usuario || undefined}
                aria-describedby={faltantes.usuario ? 'usuario-error' : undefined}
              />
              {faltantes.usuario && (
                <p id="usuario-error" className="mt-1.5 text-sm text-arcilla">
                  Escribe tu usuario.
                </p>
              )}
            </div>
            <div>
              <label htmlFor="clave" className="mb-1.5 block text-sm font-medium">
                Contraseña
              </label>
              <input
                id="clave"
                type="password"
                className="campo"
                autoComplete="current-password"
                value={clave}
                onChange={(e) => setClave(e.target.value)}
                aria-invalid={faltantes.clave || undefined}
                aria-describedby={faltantes.clave ? 'clave-error' : undefined}
              />
              {faltantes.clave && (
                <p id="clave-error" className="mt-1.5 text-sm text-arcilla">
                  Escribe tu contraseña.
                </p>
              )}
            </div>

            {error && (
              <p role="alert" className="rounded-control border-l-[3px] border-arcilla bg-[#fbeee6] px-3 py-2.5 text-sm">
                {error}
              </p>
            )}

            <button type="submit" disabled={enviando} className="boton-primario w-full">
              {enviando ? 'Ingresando…' : 'Ingresar'}
            </button>
          </form>

          <details className="mt-10 border-t border-duna pt-5 text-sm">
            <summary className="cursor-pointer font-medium text-niebla hover:text-tinta">
              Cuentas de demostración del piloto
            </summary>
            <p className="mt-3 text-niebla">
              Todas usan la contraseña <span className="font-semibold text-tinta">Piloto2026*</span>. Toca una para
              llenar el usuario.
            </p>
            <ul className="mt-3 divide-y divide-duna">
              {cuentasDemo.map((c) => (
                <li key={c.usuario}>
                  <button
                    type="button"
                    onClick={() => setUsuario(c.usuario)}
                    className="flex w-full items-baseline justify-between gap-3 py-2.5 text-left hover:text-arcilla"
                  >
                    <span className="font-medium">{c.usuario}</span>
                    <span className="text-xs text-niebla">
                      {nombreRol[c.rol]}, {c.quien}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          </details>
        </div>
      </section>
    </main>
  )
}
