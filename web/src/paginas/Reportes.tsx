import { useState, type FormEvent, type ReactNode } from 'react'
import { Aviso, Cargando, ErrorCarga } from '../componentes/Estado'
import { ContenidoDocumento, ExportarDocumento } from '../componentes/VistaPreviaReporte'
import { api, ApiError, mensajeDeError } from '../lib/api'
import { useFiltrosReporte, useReporte, useTiposReporte } from '../lib/consultas'
import { fechaCorta, nombreMes } from '../lib/periodo'
import { etiquetaFiltro, sinFiltros, type FiltrosAplicados } from '../lib/reportes'
import { useSesion as useSesionUsuario } from '../lib/sesion'
import { nombreRol } from '../lib/tipos'

/**
 * CU-04 (funcionario y administrador: desempeño, actividades y reportes de ley) y CU-05 (ente de control: solo los
 * reportes y criterios autorizados). Cada perfil ve los tipos que el API le autoriza.
 */
export function Reportes() {
  const { sesion } = useSesionUsuario()
  const tipos = useTiposReporte()
  const catalogos = useFiltrosReporte()
  const [tipoId, setTipoId] = useState<string>('')
  const [borrador, setBorrador] = useState<FiltrosAplicados>(sinFiltros)
  const [filtros, setFiltros] = useState<FiltrosAplicados>(sinFiltros)

  const tipo = tipos.data?.find((t) => t.id === tipoId) ?? tipos.data?.[0]
  const permitidos = (tipo?.filtros ?? []) as (keyof FiltrosAplicados)[]
  const aplicados = Object.fromEntries(Object.entries(filtros).filter(([k]) => permitidos.includes(k as keyof FiltrosAplicados)))
  const faltaRequerido = tipo?.requeridos.some((r) => !filtros[r as keyof FiltrosAplicados]) ?? false
  const reporte = useReporte(faltaRequerido ? undefined : tipo?.id, aplicados)

  if (!sesion) return null

  const entidades = (catalogos.data?.entidades ?? []).filter((e) => !borrador.region || e.region === borrador.region)
  const tutores = (catalogos.data?.tutores ?? []).filter((t) => !borrador.region || t.region === borrador.region)
  const opciones: Record<keyof FiltrosAplicados, ReactNode> = {
    region: catalogos.data?.regiones.map((r) => <option key={r} value={r}>{r}</option>),
    entidadId: entidades.map((e) => <option key={e.id} value={e.id}>{e.nombre}</option>),
    tutorId: tutores.map((t) => <option key={t.id} value={t.id}>{t.nombre}</option>),
    periodo: catalogos.data?.periodos.map((p) => <option key={p} value={p}>{p}</option>),
    mes: catalogos.data?.meses.map((m) => <option key={m} value={m}>{nombreMes(m)}</option>),
  }

  const cambiar = (campo: keyof FiltrosAplicados) => (e: { target: { value: string } }) =>
    setBorrador((b) => {
      const siguiente = { ...b, [campo]: e.target.value }
      if (campo === 'region' && siguiente.region) {
        const lista = catalogos.data
        if (lista?.entidades.find((x) => String(x.id) === siguiente.entidadId)?.region !== siguiente.region) siguiente.entidadId = ''
        if (lista?.tutores.find((x) => String(x.id) === siguiente.tutorId)?.region !== siguiente.region) siguiente.tutorId = ''
      }
      return siguiente
    })

  const elegirTipo = (id: string) => {
    setTipoId(id)
    setBorrador(sinFiltros)
    setFiltros(sinFiltros)
  }

  const aplicar = (evento: FormEvent) => {
    evento.preventDefault()
    setFiltros(borrador)
  }

  const grupos = [...new Set(tipos.data?.map((t) => t.grupo) ?? [])]
  const errorApi = reporte.error instanceof ApiError && [400, 403].includes(reporte.error.status) ? reporte.error : null

  return (
    <>
      <header>
        <p className="text-sm text-niebla">{nombreRol[sesion.usuario.rol]}</p>
        <h1 className="mt-1 text-2xl font-semibold sm:text-3xl">Reportes</h1>
        <p className="mt-1.5 max-w-prose text-sm text-niebla">
          {sesion.usuario.rol === 'EnteControl'
            ? 'Perfil de solo consulta: ves los reportes autorizados para el control, sin poder modificar la información.'
            : 'Desempeño de los tutores, actividades realizadas y reportes de ley, con fecha de corte y exportación.'}
        </p>
      </header>

      {tipos.isPending ? (
        <Cargando />
      ) : tipos.error ? (
        <ErrorCarga error={tipos.error} reintentar={() => void tipos.refetch()} />
      ) : (
        <>
          <fieldset className="mt-6">
            <legend className="mb-2 text-sm font-medium">Tipo de reporte</legend>
            <div className="grid gap-4">
              {grupos.map((grupo) => (
                <div key={grupo}>
                  <p className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-niebla">{grupo}</p>
                  <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
                    {tipos.data
                      .filter((t) => t.grupo === grupo)
                      .map((t) => (
                        <label
                          key={t.id}
                          className={`flex cursor-pointer flex-col gap-1 rounded-control border px-4 py-3 transition-colors ${
                            tipo?.id === t.id ? 'border-tinta bg-tinta text-papel' : 'border-duna bg-papel hover:border-niebla'
                          }`}
                        >
                          <input type="radio" name="tipo" className="sr-only" checked={tipo?.id === t.id} onChange={() => elegirTipo(t.id)} />
                          <span className="font-semibold">{t.nombre}</span>
                          <span className={`text-xs ${tipo?.id === t.id ? 'text-ceniza' : 'text-niebla'}`}>{t.descripcion}</span>
                        </label>
                      ))}
                  </div>
                </div>
              ))}
            </div>
          </fieldset>

          {permitidos.length > 0 && (
            <form onSubmit={aplicar} className="mt-6 rounded-control border border-duna bg-papel p-4" aria-label="Filtros del reporte">
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                {permitidos.map((campo) => (
                  <div key={campo}>
                    <label htmlFor={`r-${campo}`} className="mb-1.5 block text-sm font-medium">
                      {etiquetaFiltro[campo]}
                      {tipo?.requeridos.includes(campo) && <span className="font-normal text-arcilla"> (obligatorio)</span>}
                    </label>
                    <select id={`r-${campo}`} className="campo" value={borrador[campo]} onChange={cambiar(campo)}>
                      <option value="">{tipo?.requeridos.includes(campo) ? 'Elige una opción' : 'Todos'}</option>
                      {opciones[campo]}
                    </select>
                  </div>
                ))}
                <div className="flex flex-col-reverse gap-2 sm:flex-row sm:items-end lg:justify-end">
                  <button
                    type="button"
                    className="boton-secundario"
                    onClick={() => {
                      setBorrador(sinFiltros)
                      setFiltros(sinFiltros)
                    }}
                  >
                    Quitar filtros
                  </button>
                  <button type="submit" className="boton-primario sm:flex-1 lg:flex-none">
                    Generar reporte
                  </button>
                </div>
              </div>
            </form>
          )}

          <div className="mt-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <p className="text-sm text-niebla" aria-live="polite">
              {reporte.data && !reporte.isFetching ? `${reporte.data.filtros}. Corte: ${fechaCorta(reporte.data.generadoEn)}` : reporte.isFetching ? 'Generando…' : ''}
            </p>
            <ExportarDocumento
              documento={reporte.data}
              usuario={sesion.usuario}
              registrar={(formato, d) => api('/api/reportes/descargas', { metodo: 'POST', cuerpo: { tipo: d.tipo, formato, filtros: d.filtros } })}
            />
          </div>

          <section className="mt-4">
            {faltaRequerido ? (
              <Aviso tono="info">Elige el {tipo?.requeridos.map((r) => etiquetaFiltro[r as keyof FiltrosAplicados].toLowerCase()).join(', ')} y genera el reporte.</Aviso>
            ) : reporte.isPending ? (
              <Cargando texto="Generando el reporte…" />
            ) : errorApi ? (
              <Aviso>{errorApi.message}</Aviso>
            ) : reporte.error ? (
              <ErrorCarga error={reporte.error} reintentar={() => void reporte.refetch()} />
            ) : reporte.data.sinDatos ? (
              <div className="border-y border-duna py-12 text-center">
                <p className="font-display text-lg font-semibold">No se encontraron registros para estos filtros</p>
                <p className="mt-2 text-sm text-niebla">Quita algún filtro o elige otra región o periodo.</p>
              </div>
            ) : (
              <ContenidoDocumento documento={reporte.data} />
            )}
          </section>
          {catalogos.error && <p className="mt-3 text-sm text-arcilla">No se pudieron cargar las opciones de los filtros. {mensajeDeError(catalogos.error)}</p>}
        </>
      )}
    </>
  )
}
