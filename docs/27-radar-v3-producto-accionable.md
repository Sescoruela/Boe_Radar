# Radar v3: del resumen a la revisión de decisiones

El segmento inicial son autónomos y pequeñas empresas. La promesa es identificar
qué cambia en el BOE, a quién puede afectar y qué conviene revisar, con acceso
a la fuente. No se confirma automáticamente la aplicabilidad a cada negocio.

## Contrato y compatibilidad

`RadarAnalysisOutput.Actionable` contiene un contrato obligatorio para nuevas
ejecuciones `radar-v3`:

| Campo | Significado |
| --- | --- |
| `topic` | Grants, Tax, Employment, Financing, Regulation u Other |
| `impactKind` | Obligation, Opportunity o Information, independiente del tema |
| `whatChanged` | Cambio explicado en español, con cita específica |
| `affectedGroups` | Destinatarios; cada elemento debe tener cita específica |
| `nextSteps` | Comprobaciones prudentes, sin inventar hechos, requisitos ni fechas |
| `uncertainties` | Datos y condiciones pendientes de verificar |

Las fechas permanecen en `deadlines` por compatibilidad. Cada elemento incluye
`kind`: `EffectiveDate`, `ApplicationDeadline` u `Other`. Una fecha relativa lleva
`date=null` e `isExplicit=false`; no se calcula. La publicación es un metadato del
documento, separado de estas fechas. Las fechas absolutas requieren una cita con
día, mes y año completos. `evidence.supports` identifica el campo que respalda
cada cita: `whatChanged`, `affectedGroups[i]`, `impactKind`, `requirements[i]` o
`deadlines[i]`. La aplicación comprueba que las citas son literales; esto no
demuestra por sí solo que la interpretación semántica sea correcta.

El prompt Gemini pasa a `radar-v3`, sin solicitar la categoría mezclada antigua.
`category` se deriva del tema únicamente para compatibilidad con suscripciones
anteriores. Los nuevos consumidores deben usar `actionable.topic` y
`actionable.impactKind`. La heurística no infiere obligaciones: devuelve
Information, sin destinatarios ni fechas inventados.

La migración `20261023162040_AddActionableAnalysisV3` añade `actionable` nullable
en JSONB. Conserva filas anteriores sin convertirlas en análisis v3. Los análisis
v2 mantienen sus requisitos y fechas validados; los anteriores a v2 no los
exponen como hechos validados. La clave de deduplicación ya incluye versión de
prompt, por lo que se puede volver a analizar con v3 sin borrar el historial.
La consulta y el digest eligen el análisis más reciente por fecha e identificador.

## Experiencia de producto

- La ficha muestra tema e impacto separados, cambio, destinatarios, próximos
  pasos e incertidumbres. Sin contrato v3 no se deduce impacto del tema.
- Si la revisión del XML tiene otra huella que el análisis, la ficha avisa y
  retira sus nuevos hechos accionables de la vista principal.
- El detalle técnico permite inspeccionar citas, modelo, versión, fecha de
  análisis y huella del contenido. La revisión tiene su propia fecha y huella.
- El digest existente acepta v2 y v3, añade los campos accionables y distingue
  efectos de plazos. Mantiene preferencias, verificación, baja, deduplicación,
  outbox y reintentos existentes.
- Los enlaces `#publication=UUID` abren la ficha y consultan los datos actuales.
  Al compartir se excluyen parámetros de consulta, perfil y tokens de gestión.
  Si falla el portapapeles se ofrece un enlace copiable.

## Base existente aprovechada

| Necesidad | Implementación y límite |
| --- | --- |
| Personalización mínima | Tipo de negocio, actividad y territorio; perfil local y preferencias de alertas explícitas |
| Ranking explicable | Menciones y restricciones del título y de citas oficiales; ventana de hasta 1.000 señales, con indicador de resultados parciales |
| Resumen periódico | Worker `digest`/`dispatch`, hora Europe/Madrid y suscripciones verificadas; requiere ejecución programada y SMTP configurados |
| Guardar y recuperar | Estados de seguimiento en el navegador, restauración de descartados y reapertura con datos actuales; sin sincronización entre dispositivos |
| P2 acotado | Enlaces a fichas, seguimiento y referencias oficiales relacionadas; sin añadir cuentas, CRM ni carteras multiusuario |
| Cobertura | Catálogo BOE estatal importado; no es cobertura exhaustiva de boletines autonómicos, locales o europeos |

No se añade una segunda lógica de ranking ni un segundo sistema de correos. Las
pruebas existentes de esas funciones se conservan junto con las regresiones v3.

## Verificación y puesta en uso

La primera causa de CI era `npm audit`: vulnerabilidades en Angular CLI/MCP,
Vitest y Vite. CLI/build pasan a 22.2.4, Vitest a 4.1.12 y Vite se fija en 8.3.3
mediante override porque Angular build fijaba una versión vulnerable. La auditoría
se mantiene activa. Revisar el override cuando Angular incorpore una versión
corregida de Vite directamente.

Las pruebas nuevas cubren parseo Gemini, campos requeridos y enums desconocidos,
citas específicas, fechas tipadas, heurística conservadora, cambio de huella,
presentación, enlaces y fallo del portapapeles. Una regresión PostgreSQL migra
una fila antigua y verifica persistencia, catálogo y digest v3. CI también prueba
PostgreSQL/Mailpit y construye la imagen de producción.

Antes de usar datos v3 hay que aplicar la migración en el entorno elegido y
ejecutar el análisis sobre las fechas deseadas. La salida v2 no se actualiza sola.
El worker admite `--mode analyze --date AAAA-MM-DD`, con continuación por cursor;
consultar el flujo y la configuración en los documentos de desarrollo y alertas.
No se han ejecutado análisis reales en Vertex AI ni envíos a destinatarios reales
como parte de este cambio. La calidad semántica requiere revisar ejemplos reales
y contrastar la utilidad con usuarios del segmento. No se despliega ni fusiona
el PR automáticamente.
