# Hito de producto: publicaciones relacionadas

Primera entrega implementada y validada en local el 4 de octubre de 2026.
Pendiente de publicación en GitHub y Render, junto con las entregas 15–17.

## Utilidad para el negocio

Desde una ficha se puede abrir la disposición que modifica, cita o utiliza como
fundamento y consultar referencias posteriores publicadas por el BOE. Esto
reduce la búsqueda manual del contexto de un cambio. No convierte esas
relaciones en una conclusión sobre obligaciones, elegibilidad o vigencia.

## Fuente y límites

Se extrae exclusivamente `documento/analisis/referencias` del XML oficial:
`anteriores/anterior` y `posteriores/posterior`, con su identificador `referencia`,
`palabra` y `texto`. No se buscan parecidos de títulos ni se usan relaciones
generadas por Gemini. El «análisis documental del BOE» se distingue expresamente
del análisis IA de la aplicación y de las citas del cuerpo.

Se conserva la etiqueta oficial y la dirección:

- `previous`: esta publicación respecto a la disposición referenciada.
- `subsequent`: una publicación posterior respecto a la que estás consultando.

Se permiten identificadores BOE-A, BOE-B y DOUE-L con formato estricto. Todas
las URLs se construyen con el destino fijo `https://www.boe.es/buscar/doc.php`;
no se aceptan URLs ni parámetros suministrados como identificador.
Incluir una referencia europea del BOE no amplía la ingesta del MVP al DOUE.

Las relaciones duplicadas se eliminan por identificador, dirección y etiqueta.
Se muestran como máximo 30; las etiquetas y descripciones tienen longitud
acotada. La interfaz avisa del límite y explica que una lista vacía no significa
ausencia de normas, bases o convocatorias relacionadas. No se recorre el grafo
ni se descargan automáticamente los documentos enlazados.

## Conservación y compatibilidad

`DocumentText` y `ActionableSourceReview` incorporan referencias opcionales,
sin cambiar las citas ni la huella canónica del cuerpo. No participan en el
matching de actividad, tipo de negocio o territorio: una norma citada no
determina los destinatarios de la publicación actual.

El almacén usa `source-review-v2`. Su identidad de almacenamiento combina la
huella del cuerpo y las referencias serializadas, porque los metadatos del BOE
pueden cambiar sin que cambie el texto. El campo físico `source_hash` contiene
esta huella de almacenamiento en v2; la huella original del cuerpo sigue en
`review_json.sourceHash`. La fecha de revisión no modifica la identidad.

Las revisiones v1 siguen disponibles para ordenar el radar. Una nueva consulta
de la fuente guarda v2; las revisiones previas no se borran. Esta entrega no
requiere otra migración además de la tabla añadida en la entrega 17. No se
persisten perfiles ni contrastes personalizados.

La información refleja el XML consultado en la fecha indicada en la ficha.
No existe todavía renovación automática de todas las relaciones almacenadas.

## Validación

- 86 pruebas .NET aprobadas, ocho nuevas para referencias: direcciones,
  etiqueta oficial, DOUE, identificadores inválidos, duplicados, límites,
  XML malformado y separación del contraste de perfil.
- Compilaciones de producción .NET y Angular correctas, sin advertencias.
- Orden de comercio BOE-A-2026-19943: siete relaciones obtenidas del XML real,
  incluida `MODIFICA` hacia BOE-A-2003-20151 y referencias europeas.
- Auto+ BOE-B-2026-30841: lista vacía, sin relaciones inventadas.
- XML real de BOE-A-2003-20151: ocho relaciones, incluida la posterior
  `SE MODIFICA` hacia BOE-A-2026-19943; la cadena se verifica en ambos sentidos.
- El enlace hacia BOE-A-2003-20151 responde HTTP 200 y presenta la orden esperada.
- PostgreSQL: siete referencias conservadas en la revisión de comercio y cero
  en Auto+, con `profileContrast` nulo. GET posterior a POST personalizado no
  devuelve el contraste. POST conserva `Cache-Control: no-store`.
- La búsqueda conserva nueve señales y reconoce cinco revisiones guardadas,
  incluyendo revisiones v1, después del cambio de versión.
- Navegador: relaciones y enlaces oficiales visibles en la ficha de comercio;
  la sección aparece antes de los bloques largos de fragmentos. Auto+ muestra
  el aviso de ausencia de referencias, sin confundirlo con ausencia de relaciones.

## Pendiente del hito completo

Verificar más ejemplos reales con referencias posteriores, correcciones y
derogaciones; renovar referencias de forma acotada; ofrecer navegación interna
cuando el documento ya está en el catálogo y agrupar cambios sin ocultar las
publicaciones individuales. Evaluar utilidad con usuarios y desplegar/verificar
en Render. No se da por cerrada la agrupación completa ni se activan alertas.
