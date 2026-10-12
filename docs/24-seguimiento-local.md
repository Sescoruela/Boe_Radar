# Hito 3: Mi seguimiento

## Alcance

- Guardar desde tarjetas y fichas, sin registro ni envío de preferencias al servidor.
- Estados: Por revisar, Me interesa, En preparación, Completado y Descartado.
- Vista independiente con filtros por estado, contadores y enlace directo a la fuente.
- Descartar no elimina datos: sale del seguimiento activo y puede recuperarse al estado anterior.
- Las descartadas permanecen marcadas en el catálogo; no se altera la paginación ni la cobertura de búsquedas.
- El seguimiento permanece disponible aunque falle el catálogo. La ficha se vuelve a consultar al abrirla.

## Persistencia y seguridad

Clave `boe-radar.follow-up.v1` de localStorage, formato `{ version: 1, entries: [...] }`.
Cada entrada guarda identificación, título original, fecha de publicación, organismo, sección,
estado, estado anterior al descarte y fechas de guardado/último cambio. Se identifica por externalId
para no duplicar una misma publicación. No se guardan análisis, plazos inferidos ni coincidencias del perfil.
Los enlaces oficiales se construyen desde el identificador BOE validado, nunca desde URLs almacenadas.

Datos corruptos, versiones desconocidas y duplicados no se sobrescriben. Si no se puede guardar,
el cambio funciona en memoria con aviso explícito de posible pérdida al recargar. Límite de 500 entradas.
Los cambios de almacenamiento de otras pestañas se vuelven a leer mediante el evento storage.
No hay sincronización entre dispositivos, recordatorios, alertas ni confirmación de elegibilidad.
Borrar los datos del sitio o cerrar una sesión privada puede eliminar el seguimiento.

## Validación

Pruebas del almacén: persistencia, deduplicación, metadatos mínimos, descarte/recuperación,
almacenamiento bloqueado, cuota, datos corruptos, versiones incompatibles y recarga entre pestañas.
Pruebas de integración: guardar desde tarjeta, cambiar estado desde ficha, filtrar/recuperar y
seguir consultando la fuente si el catálogo o la ficha fallan. Ejecutar también la suite Angular y build de producción.

Antes de cerrar el hito: comprobar en navegador escritorio/móvil, recarga real, teclado y revisión de mensajes.
No publicar ni desplegar sin solicitud del usuario.

La comprobación visual quedó bloqueada por el fallo de arranque del entorno de navegador.
La implementación no requiere cambios en la base de datos de la aplicación.

Resultado automático (12/10/2026): 47 pruebas Angular correctas y compilación de producción
sin avisos (310,49 kB iniciales). Incluye filtros de seguimiento y validación de fechas y límite de entradas.

La publicación conjunta con el hito 4 ha sido autorizada expresamente con la revisión visual pendiente.
Ver [registro de validación y publicación](26-validacion-hitos-3-4.md).
