import { celda } from './formato'
import type { Documento, Usuario } from './tipos'
import { nombreRol } from './tipos'

export interface FiltrosAplicados {
  region: string
  entidadId: string
  tutorId: string
  periodo: string
  mes: string
}

export const sinFiltros: FiltrosAplicados = { region: '', entidadId: '', tutorId: '', periodo: '', mes: '' }

export const etiquetaFiltro: Record<keyof FiltrosAplicados, string> = {
  region: 'Región',
  entidadId: 'Entidad',
  tutorId: 'Tutor',
  periodo: 'Periodo académico',
  mes: 'Mes del acompañamiento',
}

export type Formato = 'csv' | 'pdf'

const slug = (texto: string) =>
  texto.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '')

export const nombreArchivo = (documento: Pick<Documento, 'titulo'>, extension: Formato, fecha = new Date()) =>
  `sses-${slug(documento.titulo)}-${fecha.toISOString().slice(0, 10)}.${extension}`

export const fechaHora = (iso: string) =>
  new Date(iso).toLocaleString('es-CO', { day: 'numeric', month: 'long', year: 'numeric', hour: 'numeric', minute: '2-digit' })

/** CSV para Excel: encabezado del documento y cada tabla con su título, separadas por una línea en blanco. */
export function documentoACsv(d: Documento) {
  const lineas: string[] = [
    [d.titulo].map(celda).join(';'),
    ['Fecha de corte', fechaHora(d.generadoEn)].map(celda).join(';'),
    ['Filtros', d.filtros].map(celda).join(';'),
    '',
    ['Indicador', 'Valor'].join(';'),
    ...d.cifras.map((c) => [c.etiqueta, c.valor].map(celda).join(';')),
  ]
  for (const tabla of d.tablas) {
    lineas.push('', celda(tabla.titulo), tabla.encabezado.map(celda).join(';'), ...tabla.filas.map((f) => f.map(celda).join(';')))
  }
  lineas.push('', celda(d.nota))
  return lineas.join('\r\n')
}

// Paleta de la app (index.css) en RGB para jsPDF.
type Rgb = [number, number, number]
const color = {
  tinta: [20, 24, 31] as Rgb,
  niebla: [91, 98, 112] as Rgb,
  ceniza: [174, 181, 192] as Rgb,
  duna: [221, 212, 196] as Rgb,
  arena: [245, 241, 234] as Rgb,
  papel: [252, 250, 246] as Rgb,
  terracota: [224, 102, 46] as Rgb,
}

/** PDF con el diseño de la app: cabecera oscura, cifras en tarjetas y tablas con la paleta del SSES. */
export async function documentoAPdf(d: Documento, usuario: Usuario): Promise<Blob> {
  const [{ jsPDF }, { autoTable }] = await Promise.all([import('jspdf'), import('jspdf-autotable')])
  const doc = new jsPDF({ orientation: 'landscape', unit: 'pt', format: 'a4' })
  const ancho = doc.internal.pageSize.getWidth()
  const alto = doc.internal.pageSize.getHeight()
  const m = 36
  const fondo = () => doc.setFillColor(...color.arena).rect(0, 0, ancho, alto, 'F')

  fondo()
  doc.setFillColor(...color.tinta).rect(0, 0, ancho, 104, 'F')
  doc.setDrawColor(...color.terracota).setLineWidth(2.4)
  doc.lines([[6, -6], [6, 6]], m, 34, [1, 1], 'S')
  doc.setFillColor(...color.terracota).circle(m + 12, 22, 2.6, 'F')
  doc.setFont('helvetica', 'bold').setFontSize(13).setTextColor(...color.papel)
  doc.text('SSES', m + 22, 34)
  doc.setFont('helvetica', 'normal').setFontSize(9).setTextColor(...color.ceniza)
  doc.text('Piloto del Ministerio de Educación Nacional', m + 58, 34)
  doc.setFont('helvetica', 'bold').setFontSize(19).setTextColor(...color.papel)
  doc.text(d.titulo, m, 70)
  doc.setFont('helvetica', 'normal').setFontSize(9.5).setTextColor(...color.ceniza)
  doc.text(`Fecha de corte: ${fechaHora(d.generadoEn)}   ·   Generado por: ${usuario.nombre} (${nombreRol[usuario.rol]})`, m, 89)

  let y = 126
  doc.setFont('helvetica', 'bold').setFontSize(9).setTextColor(...color.niebla)
  doc.text('FILTROS', m, y)
  doc.setFont('helvetica', 'normal').setFontSize(10).setTextColor(...color.tinta)
  const lineasFiltro = doc.splitTextToSize(d.filtros, ancho - m * 2 - 50) as string[]
  doc.text(lineasFiltro, m + 50, y)
  y += 12 * lineasFiltro.length + 8

  const hueco = 8
  const anchoTarjeta = (ancho - m * 2 - hueco * (d.cifras.length - 1)) / Math.max(1, d.cifras.length)
  d.cifras.forEach(({ valor, etiqueta, destacada }, i) => {
    const x = m + i * (anchoTarjeta + hueco)
    doc.setFillColor(...color.papel).setDrawColor(...color.duna).setLineWidth(0.6)
    doc.roundedRect(x, y, anchoTarjeta, 54, 5, 5, 'FD')
    doc.setFont('helvetica', 'normal').setFontSize(8).setTextColor(...color.niebla)
    doc.text(doc.splitTextToSize(etiqueta, anchoTarjeta - 16) as string[], x + 8, y + 15)
    doc.setFont('helvetica', 'bold').setFontSize(18).setTextColor(...(destacada ? color.terracota : color.tinta))
    doc.text(valor, x + 8, y + 44)
  })
  y += 54 + 28

  for (const tabla of d.tablas) {
    if (y > alto - 90) {
      doc.addPage()
      fondo()
      y = m + 10
    }
    doc.setFont('helvetica', 'bold').setFontSize(12.5).setTextColor(...color.tinta)
    doc.text(tabla.titulo, m, y)
    autoTable(doc, {
      startY: y + 8,
      head: [tabla.encabezado],
      body: tabla.filas.length > 0 ? tabla.filas : [[{ content: 'Sin registros', colSpan: tabla.encabezado.length }]],
      theme: 'plain',
      margin: { left: m, right: m, top: m, bottom: 40 },
      styles: { font: 'helvetica', fontSize: 8.5, cellPadding: { top: 5, bottom: 5, left: 6, right: 6 }, textColor: color.tinta },
      headStyles: { fillColor: color.tinta, textColor: color.papel, fontStyle: 'bold' },
      bodyStyles: { fillColor: color.papel, lineColor: color.duna, lineWidth: { bottom: 0.5 } },
      alternateRowStyles: { fillColor: color.arena },
      columnStyles: Object.fromEntries(tabla.numericas.map((i) => [i, { halign: 'right' as const }])),
      willDrawPage: (data) => {
        if (data.pageNumber > 1) fondo()
      },
    })
    y = (doc as unknown as { lastAutoTable: { finalY: number } }).lastAutoTable.finalY + 30
  }

  const paginas = doc.getNumberOfPages()
  for (let i = 1; i <= paginas; i++) {
    doc.setPage(i)
    doc.setDrawColor(...color.duna).setLineWidth(0.6).line(m, alto - 30, ancho - m, alto - 30)
    doc.setFont('helvetica', 'normal').setFontSize(8).setTextColor(...color.niebla)
    doc.text(doc.splitTextToSize(d.nota, ancho - m * 2 - 80)[0] as string, m, alto - 18)
    doc.text(`Página ${i} de ${paginas}`, ancho - m, alto - 18, { align: 'right' })
  }
  return doc.output('blob')
}

export function descargarArchivo(nombre: string, archivo: Blob) {
  const url = URL.createObjectURL(archivo)
  const enlace = Object.assign(document.createElement('a'), { href: url, download: nombre })
  enlace.click()
  URL.revokeObjectURL(url)
}
