import { useState } from 'react'
import { mensajeDeError } from '../lib/api'
import { descargar } from '../lib/formato'
import { descargarArchivo, documentoACsv, documentoAPdf, fechaHora, nombreArchivo, type Formato } from '../lib/reportes'
import type { Documento, TablaDocumento, Usuario } from '../lib/tipos'
import { nombreRol } from '../lib/tipos'
import { Aviso } from './Estado'
import { Hoja } from './Hoja'
import { Icono } from './Icono'

/** Cifras y tablas del documento en la pantalla (mobile-first: las tablas se desplazan dentro de su caja). */
export function ContenidoDocumento({ documento }: { documento: Documento }) {
  return (
    <>
      <dl className="grid grid-cols-2 gap-px overflow-hidden rounded-control border border-duna bg-duna sm:grid-cols-3 lg:grid-cols-6">
        {documento.cifras.map((c) => (
          <div key={c.etiqueta} className="bg-papel px-4 py-3">
            <dt className="text-xs text-niebla">{c.etiqueta}</dt>
            <dd className={`cifra mt-0.5 text-2xl font-semibold ${c.destacada ? 'text-terracota' : ''}`}>{c.valor}</dd>
          </div>
        ))}
      </dl>
      {documento.tablas.map((t) => (
        <section key={t.titulo} className="mt-8">
          <h2 className="text-lg font-semibold">{t.titulo}</h2>
          <Tabla tabla={t} />
        </section>
      ))}
      <p className="mt-6 text-xs text-niebla">{documento.nota}</p>
    </>
  )
}

function Tabla({ tabla }: { tabla: TablaDocumento }) {
  if (tabla.filas.length === 0) return <p className="mt-3 border-y border-duna py-4 text-sm text-niebla">Sin registros.</p>
  return (
    <div className="mt-3 overflow-x-auto border-y border-duna">
      <table className="w-full min-w-max text-sm">
        <thead>
          <tr className="border-b border-duna text-left text-niebla">
            {tabla.encabezado.map((h, i) => (
              <th key={h + i} scope="col" className={`py-2.5 pr-4 font-medium ${tabla.numericas.includes(i) ? 'text-right' : ''}`}>
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-duna">
          {tabla.filas.map((fila, f) => (
            <tr key={f}>
              {fila.map((valor, i) =>
                i === 0 ? (
                  <th key={i} scope="row" className="max-w-xs py-2.5 pr-4 text-left font-medium">
                    {valor}
                  </th>
                ) : (
                  <td key={i} className={`max-w-sm py-2.5 pr-4 ${tabla.numericas.includes(i) ? 'cifra text-right' : ''}`}>
                    {valor}
                  </td>
                ),
              )}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

/** Vista previa y descarga (CSV o PDF). Cada descarga se registra en la trazabilidad con `registrar`. */
export function ExportarDocumento({
  documento,
  usuario,
  registrar,
}: {
  documento: Documento | undefined
  usuario: Usuario
  registrar: (formato: Formato, documento: Documento) => Promise<unknown>
}) {
  const [previsualizando, setPrevisualizando] = useState(false)
  const [generando, setGenerando] = useState<Formato | null>(null)
  const [error, setError] = useState<string | null>(null)
  const deshabilitado = !documento || documento.sinDatos

  const descargarEn = async (formato: Formato) => {
    if (!documento) return
    setError(null)
    setGenerando(formato)
    try {
      if (formato === 'csv') descargar(nombreArchivo(documento, 'csv'), documentoACsv(documento))
      else descargarArchivo(nombreArchivo(documento, 'pdf'), await documentoAPdf(documento, usuario))
      // RQ_14: la descarga queda en la trazabilidad; si el registro falla, el archivo ya se entregó.
      await registrar(formato, documento).catch(() => undefined)
    } catch (e) {
      setError(mensajeDeError(e))
    } finally {
      setGenerando(null)
    }
  }

  return (
    <>
      <div className="grid grid-cols-2 gap-2 sm:flex">
        <button type="button" className="boton-primario col-span-2" disabled={deshabilitado} onClick={() => setPrevisualizando(true)}>
          <Icono nombre="ojo" className="size-4" />
          Vista previa
        </button>
        {(['csv', 'pdf'] as const).map((f) => (
          <button key={f} type="button" className="boton-secundario" disabled={deshabilitado || generando !== null} onClick={() => void descargarEn(f)}>
            <Icono nombre="descargar" className="size-4" />
            {generando === f ? 'Generando…' : `Descargar ${f.toUpperCase()}`}
          </button>
        ))}
      </div>
      {error && (
        <div className="mt-3">
          <Aviso>{error}</Aviso>
        </div>
      )}
      <Hoja
        abierta={previsualizando && documento !== undefined}
        titulo="Vista previa"
        subtitulo="Revisa el documento antes de descargarlo."
        ancho="amplio"
        onCerrar={() => setPrevisualizando(false)}
      >
        {previsualizando && documento && (
          <VistaPrevia documento={documento} usuario={usuario} generando={generando} onDescargar={(f) => void descargarEn(f)} />
        )}
      </Hoja>
    </>
  )
}

function VistaPrevia({
  documento,
  usuario,
  generando,
  onDescargar,
}: {
  documento: Documento
  usuario: Usuario
  generando: Formato | null
  onDescargar: (formato: Formato) => void
}) {
  const [formato, setFormato] = useState<Formato>('pdf')
  return (
    <div className="grid gap-4">
      <div role="tablist" aria-label="Formato del documento" className="grid grid-cols-2 gap-1 rounded-control bg-duna-suave p-1 sm:inline-grid sm:w-72">
        {(['pdf', 'csv'] as const).map((f) => (
          <button
            key={f}
            type="button"
            role="tab"
            id={`pestana-${f}`}
            aria-selected={formato === f}
            aria-controls={`panel-${f}`}
            onClick={() => setFormato(f)}
            className={`min-h-10 rounded-[4px] text-sm font-semibold transition-colors ${formato === f ? 'bg-tinta text-papel' : 'text-niebla hover:text-tinta'}`}
          >
            {f === 'pdf' ? 'PDF' : 'CSV (Excel)'}
          </button>
        ))}
      </div>
      <div
        role="tabpanel"
        id={`panel-${formato}`}
        aria-labelledby={`pestana-${formato}`}
        className="max-h-[58dvh] overflow-auto rounded-control border border-duna bg-duna-suave p-2 sm:p-4"
      >
        {formato === 'pdf' ? <HojaPdf documento={documento} usuario={usuario} /> : <TablaCsv documento={documento} />}
      </div>
      <div className="flex flex-col-reverse gap-2 sm:flex-row sm:items-center sm:justify-between">
        <p className="text-xs text-niebla">
          {formato === 'pdf' ? 'A4 horizontal. Así se verá el archivo al abrirlo o imprimirlo.' : 'Separado por punto y coma. Se abre directo en Excel.'}
        </p>
        <button type="button" className="boton-primario" disabled={generando !== null} onClick={() => onDescargar(formato)}>
          <Icono nombre="descargar" className="size-4" />
          {generando === formato ? 'Generando…' : `Descargar ${formato.toUpperCase()}`}
        </button>
      </div>
    </div>
  )
}

/** Réplica en HTML de la página del PDF (misma estructura y paleta). */
function HojaPdf({ documento: d, usuario }: { documento: Documento; usuario: Usuario }) {
  return (
    <article aria-label="Vista previa del PDF" className="mx-auto min-w-[44rem] max-w-[60rem] overflow-hidden bg-arena shadow-[0_2px_12px_rgb(20_24_31/0.12)]">
      <header className="bg-tinta px-7 pb-5 pt-5 text-papel">
        <p className="flex items-center gap-2 text-sm">
          <img src="/favicon.svg" alt="" className="size-5" />
          <span className="font-display font-semibold">SSES</span>
          <span className="text-xs text-ceniza">Piloto del Ministerio de Educación Nacional</span>
        </p>
        <h3 className="mt-3 font-display text-2xl font-semibold leading-tight">{d.titulo}</h3>
        <p className="mt-2 text-xs text-ceniza">
          Fecha de corte: {fechaHora(d.generadoEn)} · Generado por: {usuario.nombre} ({nombreRol[usuario.rol]})
        </p>
      </header>
      <div className="px-7 pb-6 pt-5">
        <p className="text-sm">
          <span className="mr-3 text-xs font-bold tracking-wide text-niebla">FILTROS</span>
          {d.filtros}
        </p>
        <dl className="mt-4 grid grid-cols-6 gap-2">
          {d.cifras.map((c) => (
            <div key={c.etiqueta} className="rounded-[5px] border border-duna bg-papel px-3 py-2.5">
              <dt className="text-[0.68rem] leading-tight text-niebla">{c.etiqueta}</dt>
              <dd className={`cifra mt-1 text-xl font-bold ${c.destacada ? 'text-terracota' : ''}`}>{c.valor}</dd>
            </div>
          ))}
        </dl>
        {d.tablas.map((t) => (
          <section key={t.titulo} className="mt-6">
            <h4 className="font-display text-base font-semibold">{t.titulo}</h4>
            <table className="mt-2 w-full text-[0.72rem]">
              <thead>
                <tr className="bg-tinta text-papel">
                  {t.encabezado.map((h, i) => (
                    <th key={h + i} scope="col" className={`px-2 py-1.5 font-semibold ${t.numericas.includes(i) ? 'text-right' : 'text-left'}`}>
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {t.filas.length === 0 ? (
                  <tr className="bg-papel">
                    <td colSpan={t.encabezado.length} className="px-2 py-2 text-niebla">
                      Sin registros
                    </td>
                  </tr>
                ) : (
                  t.filas.map((fila, f) => (
                    <tr key={f} className={`border-b border-duna ${f % 2 ? 'bg-arena' : 'bg-papel'}`}>
                      {fila.map((valor, i) => (
                        <td key={i} className={`px-2 py-1.5 ${t.numericas.includes(i) ? 'cifra text-right' : ''}`}>
                          {valor}
                        </td>
                      ))}
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </section>
        ))}
        <footer className="mt-8 flex justify-between gap-4 border-t border-duna pt-2 text-[0.65rem] text-niebla">
          <span>{d.nota}</span>
          <span className="shrink-0">Página 1</span>
        </footer>
      </div>
    </article>
  )
}

function TablaCsv({ documento }: { documento: Documento }) {
  return (
    <div aria-label="Vista previa del CSV" role="group" className="grid gap-4 font-mono text-[0.72rem]">
      {documento.tablas.map((t) => (
        <table key={t.titulo} className="w-full min-w-[40rem] bg-papel">
          <caption className="pb-1 text-left font-sans text-xs font-semibold">{t.titulo}</caption>
          <thead>
            <tr className="bg-duna-suave">
              {t.encabezado.map((h, i) => (
                <th key={h + i} scope="col" className="border border-duna px-2 py-1 text-left font-semibold">
                  {h}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {t.filas.map((fila, f) => (
              <tr key={f}>
                {fila.map((valor, i) => (
                  <td key={i} className={`border border-duna px-2 py-1 ${t.numericas.includes(i) ? 'text-right' : ''}`}>
                    {valor}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      ))}
    </div>
  )
}
