export type Rol = 'Tutor' | 'Funcionario' | 'EnteControl' | 'LiderPilotaje' | 'Administrador'
export type EstadoEstrategia = 'Editable' | 'SoloConsulta'
export type TipoAcompanamiento = 'Individual' | 'Grupal'

export interface Usuario {
  id: number
  username: string
  nombre: string
  rol: Rol
  region: string
}

export interface Sesion {
  token: string
  expira: string
  usuario: Usuario
}

export interface Entidad {
  id: number
  nombre: string
  region: string
  municipio: string
}

export interface Docente {
  id: number
  documento: string
  nombre: string
}

export interface Estudiante {
  id: number
  tipoDocumento: string
  numeroDocumento: string
  nombre: string
  vinculadoEn: string | null
}

export interface EstrategiaResumen {
  id: number
  nombre: string
  periodo: string
  entidad: string
  region: string
  estado: EstadoEstrategia
  totalEstudiantes: number
  totalDocentes: number
  totalAcompanamientos: number
  actualizadaEn: string
}

export interface Estrategia {
  id: number
  nombre: string
  descripcion: string
  periodo: string
  entidad: Entidad
  estado: EstadoEstrategia
  esEditable: boolean
  docentes: Docente[]
  estudiantes: Estudiante[]
}

export interface Seguimiento {
  id: string
  semana: number
  actividades: string
  avance: number
  observaciones: string
  actualizadoEn: string
  pendiente?: boolean
}

export interface Acompanamiento {
  id: string
  estrategiaId: number
  tipo: TipoAcompanamiento
  periodo: string
  objetivo: string
  compromisos: string
  docentes: Docente[]
  avanceActual: number
  seguimientos: Seguimiento[]
  registradoEn: string
  existente: boolean
  pendiente?: boolean
}

export interface RegistroAuditoria {
  id: number
  usuario: string
  operacion: string
  recurso: string
  recursoId: string | null
  detalle: string | null
  fecha: string
}

export const nombreRol: Record<Rol, string> = {
  Tutor: 'Tutor',
  Funcionario: 'Funcionario del Ministerio',
  EnteControl: 'Ente de control',
  LiderPilotaje: 'Líder de pilotaje',
  Administrador: 'Administrador',
}

export interface TutorReporte {
  id: number
  nombre: string
  region: string
}

export interface FiltrosReporte {
  regiones: string[]
  entidades: Entidad[]
  tutores: TutorReporte[]
  periodos: string[]
  meses: string[]
}

export interface TipoReporte {
  id: string
  nombre: string
  descripcion: string
  grupo: string
  filtros: string[]
  requeridos: string[]
}

export interface Cifra {
  etiqueta: string
  valor: string
  destacada: boolean
}

export interface TablaDocumento {
  titulo: string
  encabezado: string[]
  numericas: number[]
  filas: string[][]
}

/** Documento genérico del API: reportes (CU-04, CU-05) y consolidado de sesión (CU-07). */
export interface Documento {
  tipo: string
  titulo: string
  descripcion: string
  generadoEn: string
  filtros: string
  cifras: Cifra[]
  tablas: TablaDocumento[]
  nota: string
  sinDatos: boolean
}

export type EstadoSesion = 'Programada' | 'EnCurso' | 'Terminada'

export interface SesionResumen {
  id: number
  nombre: string
  region: string
  inicio: string
  fin: string
  estado: EstadoSesion
  convocados: number
  asistentes: number
  encuestas: number
}

export interface SesionDetalle {
  id: number
  nombre: string
  region: string
  inicio: string
  fin: string
  estado: EstadoSesion
  convocados: { id: number; nombre: string; ingresoEn: string | null; encuestaRespondida: boolean }[]
  incidentes: { id: number; descripcion: string; registradoEn: string }[]
}

export interface MiSesion {
  id: number
  nombre: string
  region: string
  inicio: string
  fin: string
  estado: EstadoSesion
  puedeIngresar: boolean
  ingresoEn: string | null
  encuestaRespondida: boolean
}

export interface Pregunta {
  clave: string
  texto: string
}
