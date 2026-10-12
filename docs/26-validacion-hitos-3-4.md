# Validación y publicación de los hitos 3 y 4 de producto

## Estado de aceptación

El usuario ha autorizado publicar con la revisión visual expresamente pendiente.
Esto no equivale a dar por comprobados escritorio, móvil, teclado o recarga en un navegador real.
El entorno de navegador no consigue arrancar; las pruebas Angular usan el entorno de tests.
El cierre técnico y la publicación no deben presentarse como cierre de toda la validación visual.

## Comprobaciones realizadas (12/10/2026)

- 158 pruebas .NET superadas: 134 en UnitTests, incluidas las 19 de integración, y 24 SourceSpike.
- Ninguna prueba omitida. PostgreSQL 17 temporal en loopback:55439 y Mailpit local; no SMTP externo.
- 52 pruebas Angular superadas, incluidas persistencia, estados, recuperación, errores y aislamiento de perfiles.
- Compilación Release de toda la solución: 0 errores y 0 advertencias.
- Build Angular de producción sin avisos: 313,86 kB iniciales.
- API local saludable y base conectada. Web local responde HTTP 200.
- Tres jornadas BOE importadas en la base temporal: 7 octubre, 24 septiembre y 26 septiembre.
  No se ha modificado la base de datos de Render.

## Muestra dirigida de fuentes reales

Se han consultado los XML oficiales con las reglas nuevas, utilizando perfiles de pruebas:
autónomo/comercio/Baleares, autónomo/tecnología/Canarias y pyme/tecnología/Canarias.
No son perfiles de clientes y no se guardan como suscripciones.

| Fuente | Resultado observado |
| --- | --- |
| [Auto+, BOE-B-2026-30841](https://www.boe.es/diario_boe/txt.php?id=BOE-B-2026-30841) | Menciones de trabajo por cuenta propia y empresas pequeñas en el cuerpo; no se confirma elegibilidad. |
| [Precios del tabaco, BOE-A-2026-19942](https://www.boe.es/diario_boe/txt.php?id=BOE-A-2026-19942) | Plantilla normativa. Las pistas de comercio/Baleares se mantienen como metadatos; los fragmentos sin mención quedan desconocidos. |
| [Acuerdo de Seguridad Social, BOE-A-2026-19628](https://www.boe.es/diario_boe/txt.php?id=BOE-A-2026-19628) | Mención de trabajador por cuenta propia, sin atribuir una ayuda ni confirmar obligación para todos los autónomos. |
| [Cultura digital, BOE-B-2026-32589](https://www.boe.es/diario_boe/txt.php?id=BOE-B-2026-32589) | Se detectó y corrigió la exclusión expresa de personas físicas: ahora muestra una posible restricción para autónomos con cita literal. |

Las citas del contraste se comprobaron contra los fragmentos generales y las huellas coinciden.
La revisión general GET no contiene el perfil enviado en los POST personalizados.
El resultado personalizado de cultura digital conserva la pista tecnológica del título,
señala la restricción de destinatarios y deja el territorio por comprobar.

Esta muestra es de regresión dirigida, no representativa estadísticamente. No valida todas las
formas jurídicas, ámbitos, excepciones ni publicaciones del BOE. Se mantiene la incertidumbre explícita.

## Antes y después de publicar

- Los cambios no requieren una nueva migración de producción.
- Las alertas por correo deben continuar desactivadas.
- Comprobar CI en GitHub, versión desplegada en Render, salud API/base y nuevas funciones públicas.
- No dar el despliegue por confirmado únicamente por haber enviado el commit.
- Completar después la revisión visual pendiente y los casos de recarga real y teclado.
