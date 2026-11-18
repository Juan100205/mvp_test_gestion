import { useSesion } from '../lib/sesion'
import { nombreRol, type Rol } from '../lib/tipos'

const proximo: Partial<Record<Rol, string[]>> = {
  Funcionario: ['Tablero nacional de estrategias por región', 'Avance consolidado del acompañamiento pedagógico', 'Exportación de reportes'],
  EnteControl: ['Consulta de solo lectura de estrategias y acompañamientos', 'Trazabilidad de cambios por entidad', 'Reportes para auditoría'],
  LiderPilotaje: ['Seguimiento de la adopción por tutor', 'Alertas de semanas sin seguimiento', 'Indicadores del piloto'],
}

/** Perfiles cuyo módulo se entrega en el Sprint 4: ya ingresan y ven solo su espacio (CU-06). */
export function Inicio() {
  const { sesion } = useSesion()
  if (!sesion) return null
  const { usuario } = sesion

  return (
    <>
      <p className="text-sm text-niebla">{nombreRol[usuario.rol]}</p>
      <h1 className="mt-1 text-2xl font-semibold sm:text-3xl">Las consultas de este perfil llegan en el Sprint 4</h1>
      <p className="mt-4 max-w-prose">
        {usuario.nombre}, ya puedes ingresar, pero todavía no hay pantallas de consulta para tu perfil. Por ahora los
        tutores están registrando la información.
      </p>
      <h2 className="mt-10 text-lg font-semibold">Qué incluirá</h2>
      <ul className="mt-3 max-w-xl divide-y divide-duna border-y border-duna">
        {(proximo[usuario.rol] ?? []).map((item) => (
          <li key={item} className="py-3 text-sm">
            {item}
          </li>
        ))}
      </ul>
    </>
  )
}
