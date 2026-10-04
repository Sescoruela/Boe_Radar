# Fichas adaptadas al tipo de publicación

Primera entrega implementada y verificada en local el 4 de octubre de 2026.
No está desplegada todavía en Render.

## Qué cambia

La ficha mantiene los motivos y las comprobaciones del perfil del negocio,
pero deja de presentar todas las publicaciones como convocatorias. El servidor
elige una plantilla orientativa a partir del título del XML oficial:

| Tipo | Apartados de revisión |
| --- | --- |
| Ayudas y subvenciones | Destinatarios, territorio, requisitos, cuantía, plazo, solicitud, efectos y excepciones |
| Fiscalidad | Conceptos fiscales, territorio, afectados, actuaciones, efectos, régimen transitorio, cambios y plazos de cumplimiento |
| Cambios normativos o condiciones de actividad | Cambios, territorio, afectados, actuaciones, efectos y excepciones |
| Sin tipo reconocido | Apartados con etiquetas neutrales, sin presentar una convocatoria como confirmada |

Cada ficha añade un siguiente paso propio del tipo de publicación. La fecha
de publicación sigue separada de los pasajes sobre entrada en vigor o efectos.
No se calculan fechas a partir de fórmulas como «el día de su publicación»:
se conserva el fragmento oficial para comprobarlo.

## Contrato y trazabilidad

`DocumentText` incorpora `OfficialTitle`, leído exclusivamente del nodo
`metadatos/titulo`, no de encabezados arbitrarios del cuerpo. El título selecciona
la plantilla; no altera el texto canónico ni su huella. El extracto Auto+,
cuyo título no dice «ayuda», se reconoce como convocatoria cuando el título
es un extracto de convocatoria y el cuerpo contiene la referencia BDNS.

`ActionableSourceReview` añade `kind`, `kindLabel` y `nextStep`; conserva
`sourceHash`, `reviewedAt` y los grupos de citas. No cambia el catálogo, el
perfil, la base de datos ni el proveedor de IA. Los grupos sin evidencia
siguen indicando que el dato no ha sido identificado automáticamente.

Las citas siguen siendo literales, con el límite existente de 700 caracteres.
Se conserva el párrafo completo si cabe, para no recortar innecesariamente
condiciones. Las actuaciones se buscan por expresiones operativas como
«deberá comunicar», no por una mención genérica a «derechos y obligaciones».

## Verificación

- 62 pruebas .NET aprobadas, incluyendo 13 nuevas sobre plantillas, metadatos,
  neutralidad, Auto+, obligaciones concretas y separación de publicación y efectos.
- Compilaciones .NET y Angular de producción correctas, sin advertencias.
- Consultas a la API local con XML oficial real: 19628 y 19942, normativos;
  31267 y 30841, ayudas. Todos devolvieron la plantilla prevista.
- El acuerdo con Filipinas muestra comunicaciones del empleador y del
  trabajador por cuenta propia, así como el artículo de entrada en vigor;
  ya no incluye presupuesto ni presentación de solicitudes de subvenciones.
- Los precios de tabaco muestran efectos de la resolución, no una convocatoria.
- Auto+ conserva destinatarios, tres fragmentos de presupuesto y los plazos.
- Navegador local: ficha del acuerdo y ficha de Auto+ desde la vista personalizada,
  con el perfil visible, motivos, siguientes pasos y enlaces oficiales.
- La plantilla fiscal se cubre con pruebas de documentos sintéticos; queda
  pendiente ampliar el contraste con publicaciones fiscales reales.

## Límites y continuación

El tipo sigue siendo heurístico, no una calificación jurídica. Los siguientes
pasos son guías por categoría, no instrucciones individuales basadas en una
elegibilidad comprobada. Los fragmentos pueden mencionar otras normas o
personas: hay que revisar el documento completo y sus condiciones.

Pendiente: más casos fiscales y laborales reales, distinguir bases de apertura
de convocatoria como estados explícitos, validar con usuarios y desplegar
esta entrega en Render. El análisis IA preexistente sigue mostrándose como
orientativo; esta entrega adapta la revisión de la fuente, no recalcula esos
análisis ni confirma obligaciones para el perfil.
