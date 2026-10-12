# Hito 4 de producto: personalización explicable

## Qué cambia

- Cada coincidencia se explica por actividad, tipo de negocio y territorio.
- Se distingue la pista del título/epígrafe de la mención en fragmentos oficiales.
- Cada dimensión muestra mención, posible restricción, dato del perfil sin concretar o pendiente de comprobar.
- Las citas originales pueden desplegarse. El contraste recién consultado aparece directamente en la ficha.
- Concretar actividad y territorio es opcional; el radar explica qué datos faltan y no esconde publicaciones.
- Se añaden términos de videojuegos/cultura digital y trabajo por cuenta propia; se evita equiparar extranjería con servicios profesionales.

## Reglas y límites

No se calcula elegibilidad ni probabilidad de éxito. Una mención puede ser incidental y una restricción
puede ser condicional. La interfaz exige revisar la cita y el documento completo.

Las restricciones reconocidas no suman prioridad positiva en la dimensión afectada.
Si el texto oficial aporta una restricción, también se retiran los puntos de título de esa dimensión.
Si el título ya contiene una posible restricción, otras menciones positivas no la ocultan.
Una dimensión desconocida no se interpreta como exclusión. Un territorio no especificado no se infiere.
El ámbito nacional es una pista general, no una confirmación de cobertura territorial.

El contraste usa grupos pertinentes: destinatarios, afectados, condiciones, obligaciones y excepciones;
para territorio se incluyen también sus fragmentos específicos. Importes, plazos e instrucciones de
solicitud no se emplean como evidencia de encaje. Solo expresiones explícitas de destinatarios exclusivos
públicos se señalan como posible desajuste; mencionar un ayuntamiento no basta.

Las reglas son léxicas y conservadoras, no una interpretación jurídica. No comprenden todas las
negaciones, condiciones o destinatarios. No se añaden CNAE, tamaño, forma jurídica ni datos no declarados.
Los puntos siguen siendo de ordenación interna, no porcentaje de compatibilidad.

## Arquitectura y privacidad

Campo aditivo `dimensions` en `BusinessProfileMatch` y `origin` en las dimensiones.
Angular mantiene compatibilidad con respuestas antiguas sin estos campos.
No hay migración ni cambio de extractor: el contraste se recalcula con las reglas actuales sobre
las revisiones generales existentes. No se persisten perfiles o contrastes personalizados en la caché
general. Se mantiene la instantánea de evidencia al paginar y abrir fichas no reordena el catálogo.
El hito 3 de seguimiento permanece independiente; no guarda análisis ni coincidencias.
No se activan correos ni se llama a Gemini. La publicación conjunta está autorizada por el usuario.

## Validación pendiente

Validación automática del 12/10/2026: 137 pruebas .NET superadas, 19 pruebas de integración
omitidas por no disponer de PostgreSQL/Mailpit configurados en esta sesión y 52 pruebas Angular superadas.
Compilación Release de toda la solución con 0 errores/advertencias y build Angular de producción
sin avisos (313,86 kB iniciales). Incluye regresiones de origen de evidencia, restricciones en
título y fuente, referencias incidentales, destinatarios exclusivos, datos sin concretar,
aislamiento entre perfiles y contraste visible fuera de los detalles técnicos.

Validación posterior: 158 pruebas .NET superadas sin omisiones, con PostgreSQL temporal y Mailpit
locales; 52 pruebas Angular correctas. Se ha revisado una muestra dirigida de cuatro fuentes reales
con tres perfiles de pruebas y corregido la exclusión explícita de personas físicas para el perfil
autónomo. Ver [registro de cierre técnico](26-validacion-hitos-3-4.md).

La comprobación visual y de teclado en escritorio/móvil sigue pendiente por el fallo de arranque
del navegador automatizado. El usuario ha autorizado publicar con esta limitación explícita.
La muestra dirigida no constituye una evaluación estadística de la calidad del catálogo completo.
