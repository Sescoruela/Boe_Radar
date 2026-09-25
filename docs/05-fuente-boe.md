# Fuente BOE: contrato y reutilización

## API utilizada

El spike consulta:

```text
GET https://www.boe.es/datosabiertos/api/boe/sumario/{fecha}
Accept: application/json
```

`fecha` usa el formato `AAAAMMDD`. La documentación oficial indica respuestas
200, 400, 404 y 500. El sumario contiene los enlaces HTML, XML y PDF de cada
entrada.

- Documentación: <https://www.boe.es/datosabiertos/api/api.php?lang=es>
- FAQ: <https://www.boe.es/datosabiertos/faq/boe.php>

## Particularidades observadas

- `diario`, `seccion`, `departamento`, `epigrafe` e `item` pueden aparecer como
  un objeto cuando hay uno o como un array cuando hay varios.
- `url_pdf` es un objeto cuyo campo `texto` contiene la URL, mientras que
  `url_html` y `url_xml` suelen ser strings.
- El documento XML separa metadatos y texto. El texto canónico del spike procede
  de `texto` o `texto_original`; no se mezclan metadatos ELI con el cuerpo.
- Un 404 puede representar un día sin sumario y no debe reintentarse de forma
  indefinida.

Estas observaciones están cubiertas por fixtures y pruebas de contrato. Los
fixtures son copias puntuales; una comprobación periódica contra la API real
detectará cambios futuros del esquema.

## Condiciones de reutilización

Revisión realizada el 21 de septiembre de 2026. Antes de publicar el producto
deben volver a comprobarse las condiciones vigentes:

<https://www.boe.es/informacion/aviso_legal/index.php>

Implicaciones relevantes para BOE Radar IA:

- la reutilización comercial y no comercial está permitida con las condiciones
  indicadas por la Agencia;
- el producto derivado debe usar la atribución «Basado en datos de la Agencia
  Estatal Boletín Oficial del Estado» y enlazar a `boe.es`;
- no se puede desnaturalizar el sentido de la información;
- debe quedar claro qué contenido es análisis o adaptación y qué procede de la
  fuente;
- el producto no puede aparentar carácter oficial, patrocinio o participación
  de la Agencia;
- se deben conservar los metadatos de actualización y condiciones aplicables
  cuando estén presentes;
- los datos personales, si aparecen, deben tratarse conforme a la normativa de
  protección de datos;
- la edición electrónica oficial enlazada es la referencia auténtica y el uso
  de los datos reutilizados se hace bajo responsabilidad del reutilizador.

Este resumen sirve para orientar el diseño, no sustituye una revisión jurídica.

## Política técnica del cliente

- `User-Agent` identificable: `BOE-Radar-IA/<versión>`.
- Timeout y reintentos acotados; nunca bucles agresivos.
- Una descarga de sumario por fecha y caché persistente por identificador/hash.
- Preferencia por XML para extracción y HTML como fallback.
- Conservación de las tres URLs oficiales en el registro normalizado.
- El PDF no se replica en el MVP; se enlaza a la copia oficial.

