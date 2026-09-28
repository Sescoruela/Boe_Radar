import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

const labelsPath = resolve('data/business-signals-reviewed.json');
const labels = JSON.parse(readFileSync(labelsPath, 'utf8'));
const sources = JSON.parse(readFileSync(resolve('.tmp/business-review-sources.json'), 'utf8'));
const known = new Set(labels.labels.map(label => label.externalId));

function decision(source) {
  const title = source.title;
  const text = source.text;
  switch (source.sectionCode) {
    case '1':
      if (source.externalId === 'BOE-A-2026-19845') return 'Publica la convalidación de un real decreto-ley ya publicado el 9 de septiembre; no introduce por sí misma otra solicitud, plazo u obligación para negocios. El cambio original debe evaluarse en su primera publicación.';
      if (source.externalId === 'BOE-A-2026-19846') return 'Regula el reconocimiento de cotizaciones históricas de ministros de culto concretos para prestaciones personales, no obligaciones generales o sectoriales para autónomos y pymes.';
      if (source.externalId === 'BOE-A-2026-19848') return 'Crea un centro de referencia nacional de formación profesional identificado; no abre convocatoria ni impone un nuevo requisito general a empresas aeronáuticas.';
      if (source.externalId === 'BOE-A-2026-19849') return 'Regula el reconocimiento de cotizaciones históricas de deportistas profesionales para su pensión personal, no una ayuda u obligación para negocios.';
      break;
    case '3':
      if (/convenio|adenda/i.test(title)) return 'Publica un convenio o adenda entre entidades identificadas; no abre una convocatoria ni establece un cambio general o sectorial aplicable a autónomos y pymes.';
      if (/plan de estudios/i.test(title)) return 'Modifica un plan de estudios universitario concreto; no implica una acción empresarial general o sectorial.';
      if (/Gran Cruz|Orden del M[eé]rito/i.test(title)) return 'Concede una distinción individual sin ayuda u obligación para negocios.';
      if (/autorizaci[oó]n administrativa/i.test(title)) return 'Otorga una autorización energética solicitada por una empresa identificada; es un expediente individual, fuera de las alertas generales o sectoriales del MVP.';
      if (/bien de inter[eé]s cultural/i.test(title)) return 'Declara protegida una colección artística de titulares identificados; es un expediente individual, no un cambio general o sectorial para pymes.';
      if (/Audiencia Provincial/i.test(title)) return 'Organiza la competencia de una sección judicial concreta; no establece un requisito o ayuda para negocios.';
      if (/Estatutos de la Federaci[oó]n Espa[nñ]ola de Petanca/i.test(title)) return 'Modifica órganos y disciplina interna de la federación de petanca y clubes afiliados; no establece una obligación general o sectorial para autónomos y pymes como tales.';
      if (/recurso contencioso-administrativo/i.test(title)) return 'Emplazamiento en un procedimiento judicial identificado; fuera de las alertas generales o sectoriales para negocios.';
      break;
    case '5B':
      if (['BOE-B-2026-31264', 'BOE-B-2026-31265', 'BOE-B-2026-31266'].includes(source.externalId)) {
        if (!/Entidades beneficiarias/i.test(text)) break;
        return 'Convocatoria de investigación cuya lista de beneficiarios se limita a universidades, centros de investigación y entidades equiparables; no está abierta a pymes ordinarias como solicitantes.';
      }
      if (/subasta administrativa/i.test(text)) return 'Anuncio de subasta administrativa de un expediente concreto; no es ayuda, cambio fiscal u obligación empresarial general o sectorial.';
      if (/subasta p[uú]blica al alza/i.test(text)) return 'Subasta de bienes de titularidad pública; es una posible compra, no una ayuda ni una obligación general o sectorial del MVP.';
      if (/extrav[ií]o de t[ií]tulo/i.test(text)) return 'Notificación del extravío de un título universitario individual, sin acción para negocios.';
      if (/se convoca a (?:todos )?los comuneros/i.test(text)) return 'Convocatoria de junta de una comunidad de regantes concreta; solo afecta a sus miembros, fuera de las alertas generales o sectoriales.';
      if (/expropiaci[oó]n forzosa/i.test(title + text.slice(0, 350))) return 'Información pública y citación a propietarios concretos en una expropiación; expediente individual fuera del alcance general o sectorial del MVP.';
      if (/concesi[oó]n de aguas|concesi[oó]n para aprovechamiento|autorizaci[oó]n de vertido|extinci[oó]n del derecho|extinci[oó]n del siguiente aprovechamiento/i.test(title + text.slice(0, 900))) return 'Anuncio de concesión, vertido o extinción de un derecho de aguas en un expediente identificado; no introduce una obligación general o sectorial para negocios.';
      break;
  }
  throw new Error(`No hay decisión justificada para ${source.externalId}: ${title}`);
}

const proposals = [];
for (const source of sources) {
  if (known.has(source.externalId)) continue;
  if (source.error || !source.text || !source.textSha256) throw new Error(`Fuente ausente: ${source.externalId}`);
  if (createHash('sha256').update(source.text).digest('hex') !== source.textSha256) {
    throw new Error(`Fuente modificada: ${source.externalId}`);
  }
  proposals.push({ externalId: source.externalId, relevant: false,
    reason: decision(source), sources: [source.officialUrl] });
}

console.log(`Propuestas restantes: ${proposals.length}`);
console.log(JSON.stringify(Object.fromEntries(['1', '3', '5B'].map(section =>
  [section, proposals.filter(proposal => sources.find(source =>
    source.externalId === proposal.externalId)?.sectionCode === section).length]))));

if (process.argv.includes('--apply')) {
  labels.labels.push(...proposals);
  writeFileSync(labelsPath, `${JSON.stringify(labels, null, 2)}\n`);
  console.log(`Etiquetas guardadas: ${labels.labels.length}`);
}
