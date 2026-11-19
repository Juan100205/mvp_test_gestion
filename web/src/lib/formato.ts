import type { RegistroAuditoria } from './tipos'

/** Ley 1581 de 2012: los documentos de menores se muestran enmascarados. */
export const enmascararDocumento = (tipo: string, numero: string) => `${tipo} ••••${numero.slice(-4)}`

export const plural = (n: number, singular: string, pluralTexto = `${singular}s`) => `${n} ${n === 1 ? singular : pluralTexto}`

export const operaciones: Record<string, string> = {
  'auth.ingreso': 'Ingresó al sistema',
  'auth.fallido': 'Intento de ingreso fallido',
  'auth.bloqueo': 'Cuenta bloqueada por intentos',
  'estrategia.crear': 'Creó una estrategia',
  'estrategia.editar': 'Editó una estrategia',
  'estrategia.vincular_estudiante': 'Vinculó un estudiante',
  'acompanamiento.registrar': 'Registró un acompañamiento',
  'seguimiento.registrar': 'Registró un seguimiento semanal',
  'seguimiento.actualizar': 'Actualizó un seguimiento semanal',
  'reporte.descargar': 'Descargó un reporte',
}

export const describirOperacion = (codigo: string) => operaciones[codigo] ?? codigo

export function celda(valor: string | number | null) {
  let texto = valor === null ? '' : String(valor)
  // Evita que Excel interprete el texto como fórmula (inyección CSV).
  if (/^[=+\-@]/.test(texto)) texto = `'${texto}`
  return /[";\r\n]/.test(texto) ? `"${texto.replaceAll('"', '""')}"` : texto
}

/** CSV separado por punto y coma, como lo abre Excel con configuración regional de Colombia. */
export function aCsv(registros: RegistroAuditoria[]) {
  const encabezado = ['Fecha', 'Usuario', 'Operación', 'Recurso', 'Id del recurso', 'Detalle']
  const filas = registros.map((r) =>
    [r.fecha, r.usuario, describirOperacion(r.operacion), r.recurso, r.recursoId, r.detalle].map(celda).join(';'),
  )
  return [encabezado.join(';'), ...filas].join('\r\n')
}

export function descargar(nombre: string, contenido: string, tipo = 'text/csv;charset=utf-8') {
  const url = URL.createObjectURL(new Blob(['﻿', contenido], { type: tipo }))
  const enlace = Object.assign(document.createElement('a'), { href: url, download: nombre })
  enlace.click()
  URL.revokeObjectURL(url)
}
