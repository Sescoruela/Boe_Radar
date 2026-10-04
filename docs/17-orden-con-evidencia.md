# Relevancia con evidencia: orden y conservación

Implementado y validado en local el 4 de octubre de 2026. No publicado todavía
en GitHub ni Render. Amplía las fichas por tipo y el contraste de perfil.

## Comportamiento

Abrir una ficha guarda su revisión general: citas, tipo de publicación, huella
del contenido y fecha de revisión. Las búsquedas personalizadas reutilizan
esas revisiones sin descargar más documentos ni llamar a Gemini.

El orden conserva las pistas de título/epígrafe y añade puntos internos por
dimensión con mención literal: actividad 80, tipo de negocio 40 y territorio 20.
No son probabilidades ni decisiones de elegibilidad. Una cita que contiene
restricciones reconocidas, como «No podrán», «excepto» o «excluidas», no añade
puntos en esa dimensión. Este filtro es léxico: no comprende todas las formas
de exclusión, condiciones o negaciones del lenguaje jurídico.

Las señales sin revisión o sin coincidencias siguen visibles. La interfaz
explica los motivos y muestra cuántas señales examinadas tienen revisión.
Abrir una ficha no reordena la pantalla actual: se incorpora al volver a explorar.

## Persistencia y privacidad

La migración `20261004114953_AddStoredSourceReviews` añade únicamente la tabla
`source_reviews` y su índice. Se aplicó en PostgreSQL local sin eliminar datos
del catálogo. Antes de publicar, debe aplicarse también en el entorno destino
mediante el mecanismo existente de migraciones al iniciar la aplicación.

Clave: identificador BOE + huella + versión del extractor (`source-review-v1`).
Se conserva el primer registro de cada versión/huella y las huellas anteriores.
El servidor guarda exclusivamente la revisión general: `profileContrast` se
elimina antes de serializar. No se persisten perfiles ni resultados personalizados;
sus respuestas siguen marcadas como `no-store`.

Las versiones de reglas de extracción deben incrementar la versión del almacén
para no mezclar resultados de reglas distintas bajo una misma huella.

## Paginación

La primera búsqueda devuelve `evidenceAsOf`. Angular lo reenvía al avanzar o
retroceder de página; el servidor usa solo revisiones registradas hasta ese
instante. Una nueva búsqueda toma una referencia nueva. Se normaliza a UTC,
incluidas fechas recibidas con otro desplazamiento horario.

Esto limita cambios debidos a revisiones posteriores; no es una instantánea
transaccional del catálogo. Ingesta concurrente o cambios de clasificación
pueden todavía modificar las publicaciones disponibles entre páginas.

## Validación realizada

- 78 pruebas .NET aprobadas: siete casos nuevos de ranking y uno de búsqueda
  con evidencia/paginación, incluyendo equivalencia de zona horaria.
- Compilación de API sin errores ni advertencias y compilación Angular de producción.
- PostgreSQL real: una revisión de Auto+ conserva su prioridad después de
  reiniciar la API, sin recurrir a la caché de memoria.
- Revisión nueva de Filipinas: la página siguiente conserva una revisión
  disponible, mientras una búsqueda nueva incorpora dos; siguen nueve señales.
- Comprobación directa de los registros: `profileContrast` es nulo.
- Perfil inválido: HTTP 400. GET general posterior a POST personalizado no
  devuelve datos de perfil.
- Navegador: cuatro de nueve señales revisadas; tabaco, Auto+ y Filipinas
  muestran motivos de texto, sin esconder las otras señales.

Muestra dirigida, no evaluación representativa de calidad:

| Fuente / perfil de prueba | Actividad | Tipo de negocio | Territorio |
| --- | --- | --- | --- |
| Auto+ / autónomo, otra actividad, varios territorios | Sin especificar | Mención | Sin especificar |
| Auto+ / pyme, tecnología, Canarias | Pendiente | Mención | Pendiente |
| Auto+ / autónomo, transporte, varios territorios | Pendiente | Mención | Sin especificar |
| Auto+ / pyme, comercio, Baleares | Pendiente | Mención | Pendiente |
| Erasmus+ / pyme, formación y deporte, varios territorios | Mención | Pendiente | Sin especificar |
| Tabaco / autónomo, comercio, Baleares | Mención | Pendiente | Mención |

Auto+ demuestra un límite importante: el título aporta una pista de movilidad,
pero los pasajes seleccionados no necesariamente contienen términos de esa
actividad. El sistema conserva ambas explicaciones sin inventar cobertura.

## Pendiente para cerrar el hito

Evaluar una muestra etiquetada por personas y perfiles, incluyendo casos reales
fiscales/laborales y referencias incidentales. Mejorar la selección de pasajes
y contrastar el ranking con relevancia real, no solo coincidencias léxicas.
Preparar revisión automática acotada y renovación de evidencia: hoy se genera
al abrir fichas, no para todo el catálogo, y una revisión guardada no garantiza
vigencia actual. La consulta personalizada examina como máximo 1.000 señales e
informa si la cobertura es parcial.

Quedan la publicación de estas entregas y su validación en Render. No se da por
cerrado el hito completo ni la validación con usuarios.
