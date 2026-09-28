# Fichas accionables: primera iteración

La ficha de cada publicación permite consultar el XML oficial bajo demanda y
localiza pasajes literales sobre destinatarios, ámbito territorial, requisitos,
cuantía, plazos y presentación de solicitudes. Si no se localiza un pasaje, la
interfaz muestra «No identificado automáticamente». Los pasajes son pistas de
lectura, **no** hechos jurídicos verificados ni una comprobación de elegibilidad.

La petición `GET /api/v1/source-review/{externalId}` acepta únicamente un
identificador BOE con formato estricto y construye la URL XML oficial. El
cliente de fuentes restringe el host a `www.boe.es`. El servidor guarda el
resultado seis horas en memoria para evitar consultas repetidas y limita las
peticiones por IP. Se devuelve el hash
del texto normalizado y la hora de consulta para facilitar la trazabilidad.

La ficha sigue ofreciendo HTML, XML y PDF oficiales. Si el XML falta o el BOE
no responde, se indica que hay que consultar el documento directamente.

## Límites y siguiente validación

- La búsqueda por expresiones puede seleccionar pasajes que no sean aplicables
  al usuario y omitir otros importantes. No calcula fechas implícitas ni marca
  convocatorias como abiertas.
- Las citas se extraen del texto completo normalizado, no solo del título del
  sumario. La actualización de la fuente puede cambiar los pasajes tras expirar
  la caché.
- Antes de convertir estos pasajes en campos estructurados se necesita una
  muestra revisada manualmente de 20–30 publicaciones, incluidos falsos
  positivos, y criterios por categoría para validar cada requisito y plazo.
- El contrato de análisis `radar-v1` existente puede aportar resúmenes, pero
  en Render sigue sin ejecutarse el worker ni estar configurado Gemini. Esta
  iteración no presenta ese análisis como una verificación profesional.

## Comprobación inicial con publicaciones reales

El 26 de septiembre de 2026 se contrastó la respuesta local con tres XML del
BOE:

| Publicación | Resultado de la comprobación |
| --- | --- |
| [Programa Auto+](https://www.boe.es/diario_boe/txt.php?id=BOE-B-2026-30841) | El pasaje de cuantía conserva `50.000.000 euros` como presupuesto total; el pasaje de plazo conserva apertura, cierre y horas. No se presenta esa cuantía como importe individual. |
| [Ayudas Ramón y Cajal](https://www.boe.es/diario_boe/txt.php?id=BOE-B-2026-31264) | El pasaje conserva `240.000.000,00 euros` y nombra a las entidades beneficiarias. No demuestra aplicabilidad a una pyme. |
| [Convalidación de medidas para La Palma](https://www.boe.es/diario_boe/txt.php?id=BOE-A-2026-19845) | Se detectó y corrigió el falso positivo que interpretaba «comunidades autónomas» como autónomos destinatarios. |

Esta revisión de tres casos detectó errores reales de extracción y no sustituye
la muestra de 20–30 publicaciones prevista para cerrar el hito.

## Verificación ampliada y correcciones

En una muestra dirigida de 20 XML oficiales, las 20 consultas de revisión
respondieron correctamente. No es una estimación estadística de precisión:
se eligieron señales del radar y ejemplos conocidos de falsos positivos.

- La búsqueda específica de «autónom…» ya no considera por sí sola nombres
  como «Organismo Autónomo» o «Universidad Autónoma» como coincidencias de
  autoempleo. El catálogo completo conserva la búsqueda textual general para
  otros términos.
- Los pasajes de cuantía se cortan antes de la siguiente sección numerada
  cuando el XML aplana una tabla y un encabezado en el mismo párrafo. Auto+
  conserva por separado el presupuesto total y las partidas para empresas y
  profesionales autónomos.
- «Cómo presentar la solicitud» exige una instrucción relacionada con la
  solicitud. La inscripción de un convenio en un registro administrativo ya
  no aparece como instrucción para solicitar una ayuda.

Persisten límites: una coincidencia por título no confirma aplicabilidad a
una pyme, y muchos extractos remiten las instrucciones completas a la
convocatoria o a la BDNS. La ficha debe seguir mostrando «No identificado
automáticamente» cuando no hay indicaciones suficientes en el XML consultado.

## Validación local con PostgreSQL

La interfaz descarta respuestas antiguas cuando se cambia de búsqueda y no
mezcla el contador de una vista con la selección de otra. También valida el
rango de fechas antes de consultar la API y explica qué buscar en el HTML
oficial cuando el XML no identifica el canal de solicitud.

La preselección de negocios se ha acotado para excluir, en la muestra del 26
de septiembre, las convocatorias Ramón y Cajal, Juan de la Cierva y
profesorado universitario, además de la mera convalidación de una norma.
La exclusión inicial de Erasmus+ Deporte se retiró después de revisar
la guía de elegibilidad, que admite organizaciones deportivas privadas. Son
reglas de títulos, no una garantía de relevancia ni
un sustituto de revisar los destinatarios. La búsqueda «autónomos» exige
contexto de trabajo por cuenta propia y contiene una equivalencia explícita
para Auto+, cuya fuente oficial menciona a profesionales autónomos aunque el
título no los nombre.

El 26 de septiembre se inició el PostgreSQL local existente, se importaron
las ediciones del 24 al 26 de septiembre y se repitió la última importación:

| Comprobación | Resultado local |
| --- | --- |
| Catálogo | 979 documentos: 216 previos y 763 importados en tres días. |
| Reimportación del 26 | 375 sin cambios, 0 nuevos, 0 actualizados. |
| Señales sin fecha explícita | 3 de los últimos 30 días: Auto+, Doctorados Industriales y Torres Quevedo. |
| Histórico con fecha explícita del 29 de mayo de 2024 | 5 señales recuperables. |
| Búsqueda «autónom», «autónomos» y «autonomos» | Auto+; no aparecen «Comunidad Autónoma» ni «respiración autónoma». |
| Ficha Auto+ | Tres pasajes de presupuesto, un pasaje de plazo y enlaces oficiales. El canal de solicitud permanece sin identificar. |

La API local respondió sana en `/health/live` y `/health/ready`. La web de
pruebas en `127.0.0.1:4200` se conectó a esta API y mostró el mismo catálogo.
Esta validación cubre el conjunto local, no acredita precisión y exhaustividad
en todo el BOE ni implica que los cambios estén desplegados en Render.
