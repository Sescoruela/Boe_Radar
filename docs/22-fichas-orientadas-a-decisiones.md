# Hito de producto 1: entender una publicación y saber qué comprobar

## Objetivo y alcance

Reducir el trabajo necesario para entender una publicación sin convertir una
selección automática en una confirmación de elegibilidad, vigencia o plazo.
Este hito no activa correos, crea cuentas, añade seguimiento ni cambia la clasificación del backend.

## Cambios

- Las tarjetas destacan la categoría disponible, la fecha **de publicación**, un
  título abreviado y el posible impacto del análisis existente. Sin análisis
  relevante se indica que el impacto está pendiente, sin inferirlo de palabras clave.
  La tarjeta muestra un motivo de personalización; la ficha conserva todos.
- El título breve reutiliza literalmente la cláusula «por la/el que» y mantiene
  el tipo de documento (por ejemplo, Extracto u Orden). No genera ni traduce hechos.
  Para estructuras desconocidas se conserva el título original. El título completo
  sigue disponible en la ficha y en el atributo de ayuda de la tarjeta.
- La cabecera de la ficha muestra qué se publica, el posible impacto automático,
  destinatarios, condiciones, plazo pendiente y siguiente paso. Las menciones
  encontradas se identifican como fragmentos oficiales, no como hechos confirmados.
- El acceso al texto oficial está disponible antes de los detalles. Los metadatos,
  los fragmentos completos, las relaciones y el análisis técnico son desplegables.
- La carga permite cerrar, pulsar Escape y consultar el BOE. La ficha y la revisión
  del texto tienen un límite de espera de 20 segundos y reintento independiente.
- Los errores de ficha no eliminan los resultados. Cerrar cancela las lecturas.
  El diálogo mueve el foco, bloquea el contenido de fondo y permite navegación
  por teclado; al cerrar intenta devolver el foco al control que lo abrió.

## Reglas de confianza

- No se añade una clasificación por título como si fuese un análisis validado.
- No se calculan fechas desde menciones de días hábiles ni desde la publicación.
- Un fragmento que menciona un plazo no acredita que esté abierto ni vigente.
- Los extractos largos muestran una vista abreviada con puntos suspensivos y
  etiqueta explícita; el fragmento íntegro permanece en un desplegable contiguo.
  No se reescriben ni eliminan negaciones del texto; la vista abreviada nunca
  se presenta como una lista completa de condiciones.
- Un requisito ausente no significa que no exista: se muestra pendiente.
- Los resúmenes existentes son interpretaciones automáticas, no asesoramiento.
- El enlace alternativo al BOE se construye únicamente para identificadores
  oficiales BOE-A/BOE-B con formato válido.

## Validación

Pruebas automatizadas: conservación del tipo y título oficial, ausencia de
categorías inventadas, enlaces válidos, datos desconocidos, literalidad de
fragmentos, cancelación, límite de espera, reintento con el perfil original,
catálogo conservado tras errores y cierre con Escape.

Resultado local (4 de octubre de 2026): 25 pruebas Angular aprobadas y compilación
de producción sin avisos, 293,54 kB iniciales. En el navegador se revisó la ficha
BOE-B-2026-30841 con fragmentos reales: acceso inmediato al BOE, contenido
secundario desplegable, recorrido de Tab dentro del diálogo y cierre con Escape
que devuelve el foco al botón de la tarjeta. Los errores y límites de espera se
verificaron con respuestas controladas en pruebas, no provocando fallos reales en producción.
La validación visual en móvil sigue pendiente.

Antes del cierre de producto con clientes, probar con autónomos/pymes si pueden
explicar qué se publica, encontrar la fuente y decir qué les falta comprobar.

## Pendiente para próximos hitos

Filtros por intención, estados de convocatoria, relaciones agrupadas, plazos
verificados, guardados, notas, calendario y novedades desde la última visita.
El envío de correos permanece desactivado. Publicar en GitHub/Render requiere
una petición posterior; estas mejoras se preparan y verifican en local.
