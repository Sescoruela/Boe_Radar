# Relevancia con evidencia: primera entrega

Implementada en local el 4 de octubre de 2026, sobre las fichas por tipo de la
entrega anterior. Ambas entregas están pendientes de publicación en Render.

Este documento describe la primera entrega. La persistencia y el orden con
evidencia se incorporan en [la segunda entrega](17-orden-con-evidencia.md).

## Alcance

La vista «Para mi negocio» conserva su prioridad inicial basada en título y
epígrafe. Al abrir una ficha, un nuevo bloque «Tu perfil en el texto oficial»
contrasta actividad, tipo de negocio y territorio con los fragmentos oficiales
localizados. No descarga el catálogo entero ni cambia el orden mientras navegas.

Para cada dimensión se muestran citas literales o un estado pendiente:

- `mention`: términos relacionados, con advertencia sobre condiciones y exclusiones.
- `unknown`: sin coincidencia encontrada, que no equivale a estar excluido.
- `notSpecified`: el usuario ha indicado varias actividades o territorios.

El bloque no decide elegibilidad ni obligaciones. Puede conservar una cita
negativa («No podrán...»); nunca la convierte en una afirmación favorable.
Una mención territorial tampoco garantiza cobertura.

## Arquitectura y privacidad

`POST /api/v1/source-review/{externalId}/personalized` recibe los tres códigos
del perfil existente, validados contra listas permitidas. Devuelve la revisión
de la fuente más `profileContrast`, con huella y dimensiones. Se limita con la
misma política de la consulta de fuente, 30 solicitudes por hora por dirección
remota. La respuesta personalizada lleva `Cache-Control: no-store`.

GET y POST comparten descarga y caché del XML revisado. Solo se almacena en
caché la revisión general, no el perfil ni el contraste. Este se calcula en
cada petición sobre una copia del resultado. No hay migraciones, persistencia
de perfiles ni llamadas nuevas a Gemini. Las citas proceden exclusivamente
de los grupos de evidencia de la revisión y conservan su huella canónica.

Angular envía el perfil únicamente cuando se abre desde la vista personalizada.
En las vistas generales sigue usando GET. Mantiene la protección existente
contra respuestas de fichas cerradas o peticiones anteriores.

## Validación

- 70 pruebas .NET aprobadas, ocho casos nuevos para contraste de fuente.
- Compilaciones .NET y Angular de producción correctas, sin advertencias.
- Auto+ real: el tipo autónomo recibe una mención en el cuerpo aunque no
  aparezca en el título; actividad y territorio permanecen por comprobar.
- Acuerdo de Filipinas real: cita sobre el trabajador por cuenta propia;
  no se interpreta como una ayuda ni como una obligación ya confirmada.
- HTTP 200, `no-store`, igualdad de huellas y pertenencia literal de todas
  las citas al resultado oficial general. GET posterior permanece sin perfil,
  comprobando que no se contamina la caché compartida.
- Perfil inválido: HTTP 400. Las pruebas cubren instituciones autónomas,
  menciones generales a empresas, tildes, exclusiones y perfiles diferentes.
- Navegador local: Auto+ muestra evidencia del tipo autónomo y dimensiones
  pendientes en la vista personalizada. En «Señales para negocios» no aparece
  el contraste del perfil; al volver a «Para mi negocio» reaparece correctamente.

## Lo que queda para completar el hito de relevancia

Esta entrega contrasta fragmentos ya seleccionados, no todo el documento.
La ausencia de un término en ellos no permite medir cobertura. La actividad
sigue siendo un vocabulario amplio y una referencia incidental puede dar una
mención. No son porcentajes de confianza ni recomendaciones individuales.

Pendiente de esta primera entrega: construir una muestra etiquetada por perfiles y dimensiones,
ampliar el contraste fiscal y laboral real, mejorar la selección de pasajes
de destinatarios/alcance y evaluar sus errores. Solo después incorporar la
evidencia al orden del catálogo mediante procesamiento persistido y acotado.
No se da por cerrado el hito completo de relevancia ni la validación con usuarios.
