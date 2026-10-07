# Hito — alertas personalizadas reales

Estado a 4 de octubre de 2026: primera entrega implementada y comprobada en local.
No está publicada ni activada para usuarios. Falta elegir proveedor, remitente y
ejecutor programado antes de completar el envío real en producción.

## Qué aporta esta entrega

- Perfil de negocio opcional en las preferencias de alertas: tipo, actividad y territorio.
- Elección expresa en el formulario; no copia ni guarda automáticamente el perfil del navegador.
  Se puede copiar voluntariamente y editar de forma independiente.
- Persistencia del perfil con la suscripción, disponible al verificar y gestionar el correo.
  No es una cuenta de usuario. Los enlaces de gestión siguen siendo privados.
- Orden de novedades y motivos personalizados utilizando las mismas reglas que el radar.
  Las coincidencias no garantizan cobertura, elegibilidad ni obligaciones.
- Las categorías y palabras clave siguen siendo filtros conjuntivos. El perfil solo ordena:
  una novedad con ámbito desconocido no se descarta por falta de menciones.
- Se usan revisiones oficiales almacenadas únicamente cuando su hash coincide con el del
  análisis del digest. No hay descargas de BOE ni llamadas a Gemini durante la preparación.
- Al quitar el perfil y guardar se elimina de la suscripción. Cambiar preferencias o darse
  de baja cancela y vacía los digests pendientes del suscriptor, incluidas las reservas.
  No se puede retirar un correo ya aceptado por el proveedor o iniciado tras la última comprobación.

La distinción de privacidad es intencionada: el radar del navegador sigue sin persistir
perfiles en servidor. El perfil de alertas, elegido expresamente, sí necesita guardarse
para preparar mensajes cuando el usuario no está conectado. Desactivar este perfil no
borra el correo ni el historial operativo de la suscripción; no equivale a borrar todos
los datos del usuario.

## Selección e idempotencia

La selección diaria primero determina el análisis más reciente de cada documento y
después comprueba relevancia, `radar-v2` y método permitido. Antes podía recuperar un
positivo antiguo tras descartar el último análisis. El desempate utiliza el identificador.
En producción continúa exigiéndose `gemini`; `--include-heuristic` es solo para demos.

Se conserva un digest por fecha/suscriptor, las claves únicas de outbox y la deduplicación
por documento previamente incluido. Cambiar preferencias no regenera un digest de la
misma fecha: las nuevas preferencias se aplican a los próximos resúmenes. Una reanálisis
del mismo documento no provoca por sí solo una nueva alerta; detectar cambios normativos
relevantes es otro trabajo.

La entrega externa sigue siendo **al menos una vez**. Un fallo tras la aceptación por
SMTP y antes de marcar el envío puede originar un duplicado. No se promete exactamente
una entrega ni ausencia absoluta de mensajes después de una baja que coincida con el envío.

## API y migración

`POST /api/v1/subscriptions` y `PUT /api/v1/subscriptions/me` aceptan:

```json
{
  "categories": ["Grant"],
  "keywords": [],
  "digestHour": 8,
  "profile": { "businessType": "sme", "activity": "retail", "territory": "baleares" }
}
```

El alta añade `email` y `consent: true`. `profile: null`, o su ausencia, desactiva la
personalización. El servidor valida el perfil; un alta pública repetida no modifica
una suscripción activa. La gestión requiere su token en cabecera, no en la URL de consulta.

`20261004141813_AddSubscriptionBusinessProfile` añade una columna `jsonb` nullable,
sin modificar las suscripciones existentes ni darles un perfil por defecto. La migración
se ha aplicado únicamente a PostgreSQL local. Antes de producción: copia de seguridad,
comprobación de CI y aplicación de migraciones con la configuración habitual.

## Verificación realizada

- 129 pruebas .NET, 13 de Angular y ocho de reglas: 150 casos, sin omisiones.
- PostgreSQL real con esquemas aislados: migración de una suscripción existente,
  conservación del perfil tras confirmar, protección frente a alta repetida,
  eliminación del perfil, selección estricta del análisis actual, prioridad de
  evidencia solo con hash coincidente, idempotencia y cancelación tras actualización/baja.
- Prueba SMTP con Mailpit local: alta, entrega de verificación, confirmación, entrega
  de gestión, preparación/entrega de digest, repetición sin un segundo digest y baja.
  Usa destinatarios aleatorios `@example.invalid`, sin salida al exterior. Los análisis
  `gemini` de esta prueba son fixtures, no llamadas reales al modelo.
- Angular: opt-in explícito, bloqueo por falta de consentimiento/perfil inválido,
  gestión y eliminación independiente del perfil del radar, además de las regresiones.
- Compilación .NET sin advertencias y Angular de producción dentro del presupuesto.
  Las altas de correo de la API local se mantienen desactivadas.

Para reproducir en PowerShell:

```powershell
docker compose up -d postgres mailpit
$env:BOERADAR_TEST_CONNECTION = 'Host=localhost;Port=54329;Database=boeradar;Username=boeradar;Password=boeradar_dev'
$env:BOERADAR_TEST_SMTP = 'true'
dotnet test BoeRadar.slnx --configuration Release
```

La prueba SMTP tiene opt-in: sin esa variable se omite. CI incorpora Mailpit temporal
y la variable para ejecutarla. Este cambio de CI aún no se ha publicado/ejecutado en GitHub.
Mailpit conserva los correos de prueba; los esquemas se eliminan al terminar, por lo que
los enlaces de estos correos automatizados no son una demo persistente de gestión.

## Envío y decisión para Render

El envío SMTP exige TLS salvo en loopback o Mailpit de desarrollo. Tiene un límite
configurable `Email__TimeoutSeconds` (30 segundos por defecto, rango 1–120), sin usar
credenciales del sistema. El puerto debe estar entre 1 y 65.535. SMTP local queda para
pruebas; el adaptador actual usa STARTTLS, no TLS implícito de puerto 465, según la
[documentación de Microsoft](https://learn.microsoft.com/en-us/dotnet/api/system.net.mail.smtpclient.enablessl?view=net-10.0).

El Blueprint actual declara un servicio web gratuito. Render bloquea en estos servicios
el tráfico saliente a los puertos 25, 465 y 587, y puede suspenderlos por inactividad.
Por tanto, para conservar ese plan se propone un proveedor con API **HTTPS**, no conectar
directamente el adaptador SMTP. Véanse las [limitaciones oficiales de Render](https://render.com/docs/free).
No se han cambiado planes, contratado servicios ni solicitado acceso a credenciales.

## Qué falta para cerrar el hito

1. Elegir proveedor con API HTTPS y dominio/remitente verificado. Sus claves se guardan
   como secretos del entorno, nunca en Git ni en esta conversación.
2. Implementar su adaptador y sus webhooks autenticados de rebotes, quejas y supresión.
   Guardar el identificador de entrega y aprovechar idempotencia del proveedor si existe.
3. Probar un correo real a un destinatario de prueba expresamente autorizado, gestión y
   baja; comprobar autenticación del remitente y entregabilidad.
4. Programar análisis, preparación de resúmenes y despacho en un ejecutor fiable.
   El Blueprint todavía no incluye un worker/cron de alertas. Los servicios cron de Render
   tienen facturación: no se crean sin decidir y autorizar esa infraestructura.
   Véase [Render Cron Jobs](https://render.com/docs/cronjobs).
5. Validar Gemini real y que existan análisis publicables: la configuración actual de
   Render es heurística, mientras los digests de producción exigen `gemini`.
6. Revisar límites de volumen, reintentos, bajas durante envío, observabilidad y texto
   de privacidad. Después publicar, comprobar CI/despliegue y activar el piloto.

Hasta entonces `Features__EmailAlertsEnabled` permanece desactivado en producción.
