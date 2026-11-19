# Matriz de trazabilidad: requerimientos, flujos y pruebas

Estado verificado el 19/11/2026 con TDD: dominio 49/49, API 61/61, web 69/69 y E2E con Playwright contra Docker Compose (Sprints 1 a 4).
Cada fila indica dónde está la regla y qué prueba la demuestra. Los flujos y requerimientos son los de la propuesta (Parcial Final, Propuesta Piloto SSES).

## Casos de uso por sprint

| Sprint | Caso de uso | Actor | Estado |
|---|---|---|---|
| 1 | CU-06 Gestionar acceso y trazabilidad | Todos / Administrador | Cumple |
| 2 | CU-01 Gestionar estrategias estudiantiles | Tutor | Cumple |
| 2 | CU-02 Asociar estudiantes a una estrategia | Tutor | Cumple |
| 3 | CU-03 Registrar acompañamiento pedagógico y su seguimiento | Tutor | Cumple |
| 4 | CU-04 Generar reportes de desempeño y reportes de ley | Funcionario del Ministerio (y Administrador) | Cumple |
| 4 | CU-05 Generar reportes para el ente de control | Ente de control | Cumple |
| 4 | CU-07 Gestionar sesiones de pilotaje y percepción de los tutores | Líder de pilotaje / Tutor | Cumple |

## Flujos del Sprint 4

### CU-04 · Reportes de desempeño y de ley (`funcionario.men`)

| Paso / flujo | Comportamiento | Implementación | Pruebas |
|---|---|---|---|
| 1 | Ingreso y permisos del perfil | Política `Reportes` (Funcionario, EnteControl, Administrador) | `App.test`: el funcionario entra a los reportes |
| 2 | Tipos de reporte: desempeño, actividades y reportes de ley | `GET /api/reportes/tipos` | `ReportesTests.El_funcionario_ve_desempeno_actividades_y_reportes_de_ley` |
| 3 | Filtros: tipo, periodo, entidad, región, tutor y mes | `Validar` en `ReportesEndpoints` | `Reportes.test`: elige entre desempeño, actividades y reportes de ley |
| 4-5 | Consolida estrategias, estudiantes, acompañamientos y seguimientos | `GET /api/reportes/{tipo}` | `El_desempeno_consolida_indicadores_por_tutor`, `Actividades_lista_cada_acompanamiento...`, `La_cobertura_calcula...`, `El_reporte_de_datos_personales...` |
| 6 | Exporta con fecha de corte (CSV y PDF con vista previa) | `ExportarDocumento`, `documentoACsv`, `documentoAPdf` | `Reportes.test`: vista previa y descarga; exportación de documentos |
| 7 | Registra la operación | `reporte.generar` y `reporte.descargar` | `Generar_y_descargar_quedan_en_la_trazabilidad` |
| 3A | Filtros incompletos o periodo no válido: indica qué corregir | 400 `filtros_no_validos` con `campos` | `Filtros_no_validos_o_incompletos_indican_que_corregir` (5 casos); `Reportes.test` 3A |
| 4A | Sin información: lo informa | `sinDatos` en el documento | `Sin_informacion_el_reporte_lo_indica`; `Reportes.test` 4A |

Reportes de ley (RQ_10), definidos con el usuario:
- **Cobertura del programa:** estudiantes precargados y vinculados, cobertura, y docentes por entidad y región. Exige el periodo.
- **Tratamiento de datos personales (Ley 1581 de 2012):** datos tratados, titulares, medidas de protección, y accesos y operaciones registrados. Ninguna celda contiene documentos de estudiantes.

### CU-05 · Reportes para el ente de control (`ente.control`)

| Paso / flujo | Comportamiento | Implementación | Pruebas |
|---|---|---|---|
| 1 | Perfil de solo consulta | Sin endpoints de escritura para el rol | `AccesoTests.Funcion_que_no_corresponde_al_perfil_es_restringida` |
| 2 | Solo los reportes autorizados | `consolidado` con filtros de periodo, entidad, región y mes | `El_ente_de_control_solo_ve_el_consolidado...`; E2E: 1 tipo y sin filtro de tutor |
| 3-5 | Consolida con fecha de corte | `GET /api/reportes/consolidado` | `El_consolidado_muestra_cada_estrategia_con_fecha_de_corte` |
| 6 | Descarga y registro de quién y cuándo | `POST /api/reportes/descargas` | `Generar_y_descargar_quedan_en_la_trazabilidad` |
| 3A | Criterios fuera de sus permisos: se restringe e informa lo autorizado | 403 `criterio_no_autorizado` con `autorizados` | `Criterios_fuera_de_los_permisos_del_ente...`; `Descargas_no_validas_se_rechazan`; `Reportes.test` 3A |
| 4A | Sin información | `sinDatos` | `Sin_informacion_el_reporte_lo_indica` |

### CU-07 · Sesiones de pilotaje y percepción (`lider.piloto` y tutores)

| Paso / flujo | Comportamiento | Implementación | Pruebas |
|---|---|---|---|
| 1 | El líder ingresa con su perfil | Política `LiderPilotaje` | `Solo_el_lider_programa_y_consulta_sesiones`; `App.test` |
| 2 | Programa la sesión (fecha, región y tutores) y la notifica | `SesionPilotaje.Programar`; convocatoria visible en «Mis sesiones» | Dominio: `Programar_*`, `No_convoca_tutores_de_otra_region`, `Rechaza_fecha_pasada...`; API: `El_lider_programa_una_sesion_y_el_tutor_convocado_la_ve` |
| 3 | El tutor ingresa y queda su asistencia | `RegistrarAsistencia` (desde 30 min antes, una sola vez) | `El_tutor_convocado_registra_asistencia_al_ingresar`, `Ingresar_dos_veces_no_duplica...`, `Fuera_del_horario...` |
| 4 | Uso y tiempos de respuesta | Middleware `MedidorDeUso` → `metricas_peticiones` | `El_consolidado_presenta_asistencia_uso_incidentes_y_encuesta` |
| 5 | Encuesta de percepción (5 preguntas de 1 a 5 y comentario) | `ResponderEncuesta` | `El_tutor_que_asistio_responde_la_encuesta`; `Solo_responde_quien_asistio_y_una_sola_vez` |
| 6 | Consolidado: asistencia, incidentes (automáticos y manuales) y encuesta | `GET /api/sesiones/{id}/consolidado` | `El_consolidado_presenta...`; `Sesiones.test` |
| 7 | Exporta el registro de la sesión | `ExportarDocumento` y `POST /api/sesiones/{id}/descargas` | `Programar_asistir_responder_y_exportar_quedan_en_la_trazabilidad` |
| 3A | Tutor no convocado: se le informa su sesión asignada | 403 `no_convocado` con `sesionAsignada` | Dominio `Un_tutor_no_convocado_no_puede_ingresar`; API `Un_tutor_no_convocado_recibe_su_sesion_asignada`; `Sesiones.test` 3A |
| 5A | Encuesta incompleta: indica las preguntas pendientes | 400 `datos_incompletos` con `campos` p1 a p5 | Dominio `La_encuesta_incompleta_indica...`; API `La_encuesta_valida...`; `Sesiones.test` 5A; E2E |

## Requerimientos

| RQ | Descripción | CU | Evidencia |
|---|---|---|---|
| RQ_01 a RQ_03 | Estrategias por entidad, validación y edición sin estudiantes | CU-01 | `EstrategiaTests`, `EstrategiasTests`, `Estrategias.test`, `EstrategiaDetalle.test` |
| RQ_04, RQ_05 | Estudiantes precargados y sin duplicados | CU-02 | `EstrategiasTests`, `EstrategiaDetalle.test` |
| RQ_06 a RQ_08 | Acompañamiento mensual, seguimiento semanal e historial | CU-03 | `AcompanamientoTests`, `AcompanamientosTests`, `Acompanamientos.test` |
| RQ_09 | Desempeño de tutores y actividades | CU-04 | `ReportesTests` (desempeño y actividades) |
| RQ_10 | Reportes de ley | CU-04 | `ReportesTests` (cobertura y Ley 1581) |
| RQ_11 | Ente de control de solo consulta | CU-05 | `ReportesTests` (consolidado y 3A) |
| RQ_12 | Exportación con fecha de corte | CU-04, CU-05, CU-07 | `Reportes.test`, `Sesiones.test`, E2E (CSV y PDF) |
| RQ_13 | Ingreso por perfil y bloqueo al quinto intento | CU-06 | `AccesoTests`, `UsuarioTests`, `Login.test`, `App.test` |
| RQ_14 | Registro automático de operaciones | CU-06 y todos | `AccesoTests`, `ReportesTests`, `SesionesTests` (trazabilidad) |
| RQ_15 | Sesiones y control de asistencia | CU-07 | `SesionPilotajeTests`, `SesionesTests` |
| RQ_16 | Encuesta de percepción | CU-07 | `SesionPilotajeTests`, `SesionesTests`, `Sesiones.test` |
| RQ_17 a RQ_19 | Concurrencia, volumen y disponibilidad | — | Índices (GIN y compuestos) y pruebas de carga planeadas para las semanas 13 y 14 (fuera del MVP) |
| RQ_20 | Seguridad de la información | CU-06 | PBKDF2, JWT, políticas por perfil, CSP; reportes sin datos personales |
| RQ_21 | Operación sin conexión | CU-03 | `colaOffline.test`, `AcompanamientosTests`, E2E sin conexión |
| RQ_22 | Integridad y respaldo | — | Llaves únicas y restricciones CHECK; el respaldo depende del plan de la base en producción |
