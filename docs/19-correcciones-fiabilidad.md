# Correcciones de fiabilidad — 4 de octubre de 2026

Esta entrega corrige defectos del análisis de código. Las optimizaciones de consultas,
extracción y componentes quedan separadas de estas correcciones.

## Cambios

1. **Actualización del catálogo.** Se reimportan los últimos siete días aunque ya tengan
   documentos, para incorporar ediciones adicionales y cambios. Un cursor persistente en
   `catalog_refresh_progress` recupera periodos de inactividad, hasta 30 días antiguos por
   ejecución, además de la ventana reciente. Solo avanza sobre días completados o días
   pasados sin sumario; un error mantiene pendiente la fecha. La inicialización toma la
   última publicación menos seis días, o los últimos siete días si el catálogo está vacío.
   No constituye una carga de todo el archivo histórico ni una garantía de ejecución diaria
   si Render mantiene dormida la instancia. Antes de la implantación del cursor tampoco
   existe evidencia suficiente para detectar todos los huecos históricos antiguos.

2. **Procesamiento por lotes.** La lectura de metadatos avanza por identificador. `--limit`
   limita los nuevos análisis intentados, no el primer bloque del catálogo. Los análisis
   existentes se omiten y se siguen buscando candidatos. Un error aislado devuelve su
   identificador y tipo, sin publicar su contenido ni detener el resto. La cancelación del
   proceso sigue propagándose. El resultado incluye `failures` y `nextCursor`.
   Gemini rechaza las fuentes de más de 40.000 caracteres mediante
   `AnalysisSourceTooLongException`: no publica un análisis de un texto truncado como si
   cubriese todo el documento. Su procesamiento por fragmentos queda pendiente.

3. **Contrato `radar-v2`.** Cada requisito coincide literalmente con una cita identificada
   mediante `supports=requirements[i]`, conservando condiciones y excepciones. Cada plazo
   tiene una cita `supports=deadlines[i]`; las fechas absolutas usan ISO estricto y deben
   aparecer completas, incluido el año, en esa cita. Los plazos relativos no admiten fechas
   calculadas. No se deduce el año de la publicación. Esto verifica trazabilidad textual,
   no vigencia normativa, elegibilidad ni interpretación jurídica. Los registros antiguos
   se conservan: sus requisitos y fechas no se exponen como datos validados, y quedan fuera
   de los nuevos correos hasta que se reanalicen.

4. **Paginación.** El desplazamiento se calcula sin desbordamiento y se limita al total.
   Una página extremadamente alta devuelve una lista vacía, no un error de PostgreSQL.

5. **Historial de revisiones.** Las versiones siguen siendo inmutables; una tabla adicional
   registra cuándo se observa cada versión distinta. A → B → A vuelve a seleccionar A y
   mantiene B para las consultas históricas. La migración recupera las primeras observaciones
   existentes. Una lectura de caché no se registra como una nueva observación de la fuente.

6. **Cola de correo.** El número de intento actúa como identificador de la reserva. Enviar,
   fallar o cancelar exige la reserva vigente, no solo el identificador del mensaje. Un
   proceso antiguo no puede sobrescribir un mensaje recuperado por otro. El quinto intento
   caducado pasa a `Failed` con `claim-expired-final-delivery-unknown`, sin más reintentos
   automáticos. Los mensajes se reservan de uno en uno para no consumir la reserva mientras
   esperan a otros envíos. SMTP no proporciona entrega exactamente una vez: si el proveedor
   acepta un correo y el proceso cae antes de confirmar, sigue siendo posible un duplicado.
   Las alertas públicas siguen desactivadas.

7. **Dependencias.** Angular, su CLI y sus herramientas quedan fijadas en 22.2.1. El árbol
   incorpora Piscina 5.3.2. Se regeneró la instalación sin forzar dependencias incompatibles;
   la auditoría de npm no detecta vulnerabilidades a fecha de esta verificación.

## Continuar un análisis

```powershell
dotnet run --project src/BoeRadar.Worker -- --mode analyze --date 2026-10-01 --limit 100
# Si el resultado trae nextCursor:
dotnet run --project src/BoeRadar.Worker -- --mode analyze --date 2026-10-01 --limit 100 --after BOE-A-2026-12345
```

El cursor del ejemplo debe sustituirse por el devuelto. Para reintentar errores anteriores
se vuelve a ejecutar sin `--after`: los análisis ya guardados se omiten. Alcanzar exactamente
el límite puede requerir una última ejecución de continuación sin nuevos análisis. El
proceso devuelve código 3 si hay errores o continuación pendiente y no ejecuta los correos
de `--mode all` con un lote incompleto.

## Verificación reproducible

```powershell
docker compose up -d postgres
dotnet restore BoeRadar.slnx
dotnet test BoeRadar.slnx --configuration Release
```

Para incluir las regresiones de PostgreSQL, define `BOERADAR_TEST_CONNECTION` con la conexión
local de desarrollo antes de ejecutar las pruebas. Solo se aceptan `localhost` o `127.0.0.1`,
puerto 54329. Cada prueba crea un esquema aleatorio `boeradar_test_...`, aplica las migraciones
y elimina únicamente ese esquema al terminar. No modifica los datos del esquema de la
aplicación. Sin la variable, estas pruebas se señalan explícitamente como omitidas.

```powershell
node --test scripts/evaluate-business-signals.test.mjs scripts/build-business-review-queue.test.mjs
cd src/boe-radar-ui
npm ci
npm run build:production
npm audit
```

Antes del despliegue se deben aplicar las migraciones, incluida
`AddReviewObservationsAndRefreshProgress`. Render lo hace si se mantiene
`DatabaseMigrateOnStartup=true`. Esta entrega no publica por sí sola cambios en GitHub ni Render.

## Pendiente, separado de las correcciones

- Reducir consultas repetidas de la búsqueda personalizada y lecturas históricas.
- Consolidar las pasadas de extracción del XML y dividir componentes de interfaz.
- Implementar el procesamiento por fragmentos de documentos que superan el límite de
  entrada de Gemini, ahora rechazados explícitamente sin enviar una petición al proveedor.
- Verificar Gemini con credenciales reales, además de las regresiones deterministas.
- Automatizar estas verificaciones en CI y ampliar las pruebas de interfaz.
- Validar proveedor real de correo, rebotes y supervisión antes de activar alertas públicas.

## Resultado de la verificación local

- 108 pruebas de .NET superadas, sin omisiones; incluyen nueve regresiones en PostgreSQL.
- Ocho pruebas de reglas y evaluación de señales superadas.
- Compilación completa de .NET sin errores ni advertencias; compilación Angular de producción correcta.
- Auditoría npm: cero vulnerabilidades detectadas; versiones Angular alineadas en 22.2.1.
- Migración aplicada localmente sin eliminar el catálogo: se conservan sus 1.373 publicaciones.
- Web local y comprobación de disponibilidad: HTTP 200. La página 2.147.483.647 devuelve
  cero elementos conservando el total, tanto a través de la API como del proxy de la web.
- Ficha y consulta real de fuente oficial operativas: BOE-A-2026-19943 devuelve seis grupos
  y siete referencias oficiales. Las alertas permanecen desactivadas; no se enviaron correos reales.
- No se ha probado Gemini contra Vertex AI ni se han desplegado estos cambios en Render.
