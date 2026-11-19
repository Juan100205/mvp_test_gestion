import { Navigate, Outlet, Route, Routes, useLocation } from 'react-router'
import { Shell } from './componentes/Shell'
import { useSesion } from './lib/sesion'
import type { Rol } from './lib/tipos'
import { Acompanamientos } from './paginas/Acompanamientos'
import { EstrategiaDetalle } from './paginas/EstrategiaDetalle'
import { EstrategiaForm } from './paginas/EstrategiaForm'
import { Estrategias } from './paginas/Estrategias'
import { Login, rutaInicial } from './paginas/Login'
import { MisSesiones } from './paginas/MisSesiones'
import { Reportes } from './paginas/Reportes'
import { SesionDetalleVista, Sesiones } from './paginas/Sesiones'
import { Trazabilidad } from './paginas/Trazabilidad'

/** CU-06: sin sesión va al ingreso; con un perfil distinto va a su página de inicio. */
function Protegida({ roles }: { roles?: Rol[] }) {
  const { sesion } = useSesion()
  const ubicacion = useLocation()
  if (!sesion) return <Navigate to="/login" replace state={{ desde: ubicacion.pathname }} />
  if (roles && !roles.includes(sesion.usuario.rol)) return <Navigate to={rutaInicial(sesion.usuario.rol)} replace />
  return <Outlet />
}

function Raiz() {
  const { sesion } = useSesion()
  return <Navigate to={sesion ? rutaInicial(sesion.usuario.rol) : '/login'} replace />
}

function SoloSinSesion() {
  const { sesion } = useSesion()
  return sesion ? <Navigate to={rutaInicial(sesion.usuario.rol)} replace /> : <Login />
}

export function Rutas() {
  return (
    <Routes>
      <Route path="/login" element={<SoloSinSesion />} />
      <Route element={<Protegida />}>
        <Route element={<Shell />}>
          <Route element={<Protegida roles={['Tutor']} />}>
            <Route path="/estrategias" element={<Estrategias />} />
            <Route path="/estrategias/nueva" element={<EstrategiaForm />} />
            <Route path="/estrategias/:id" element={<EstrategiaDetalle />} />
            <Route path="/estrategias/:id/editar" element={<EstrategiaForm />} />
            <Route path="/estrategias/:id/acompanamientos" element={<Acompanamientos />} />
            <Route path="/mis-sesiones" element={<MisSesiones />} />
          </Route>
          <Route element={<Protegida roles={['Administrador']} />}>
            <Route path="/trazabilidad" element={<Trazabilidad />} />
          </Route>
          <Route element={<Protegida roles={['Funcionario', 'EnteControl', 'Administrador']} />}>
            <Route path="/reportes" element={<Reportes />} />
          </Route>
          <Route element={<Protegida roles={['LiderPilotaje']} />}>
            <Route path="/sesiones" element={<Sesiones />} />
            <Route path="/sesiones/:id" element={<SesionDetalleVista />} />
          </Route>
        </Route>
      </Route>
      <Route path="*" element={<Raiz />} />
    </Routes>
  )
}
