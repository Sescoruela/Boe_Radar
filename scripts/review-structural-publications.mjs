import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

const labelsPath = resolve('data/business-signals-reviewed.json');
const sourcePath = resolve('.tmp/business-review-sources.json');
const labels = JSON.parse(readFileSync(labelsPath, 'utf8'));
const sources = JSON.parse(readFileSync(sourcePath, 'utf8'));
const known = new Set(labels.labels.map(item => item.externalId));

const categories = {
  '2A': {
    reason: 'Nombramiento, destino, situación o rectificación sobre personal público; no abre una ayuda ni establece una obligación para negocios.',
    valid: source => /(?:nombra|nombramiento|adscrib|jubilaci[oó]n|situaci[oó]n administrativa|libre designaci[oó]n|funcionari|concurso de traslados|concurso espec[ií]fico|provisi[oó]n de puestos|puesto de trabajo|personal docente)/i.test(source.title + source.text.slice(0, 500)),
  },
  '2B': {
    reason: 'Proceso selectivo, provisión de plazas o actuación sobre su tribunal para empleo público; se dirige a aspirantes individuales, no a negocios.',
    valid: source => /(?:convocatoria|convoca|proceso selectivo|oposici[oó]n|provisi[oó]n de puestos|tribunal calificador|aspirantes|plazas)/i.test(source.title + source.text.slice(0, 500)),
  },
  '4': {
    reason: 'Anuncio de subasta judicial de un expediente concreto; fuera de las ayudas, cambios fiscales y obligaciones empresariales generales del MVP.',
    valid: source => /subasta judicial/i.test(source.text),
  },
  '5A': {
    reason: 'Anuncio de contratación pública o su rectificación; puede ser una oportunidad comercial, pero las licitaciones quedan fuera del alcance actual del radar.',
    valid: source => /(?:poder adjudicador|anuncio de licitaci[oó]n|anuncio de formalizaci[oó]n|rectificaci[oó]n.*licitaci[oó]n|rectificaci[oó]n.*formalizaci[oó]n)/i.test(source.text.slice(0, 700)),
  },
};

const proposals = [];
for (const source of sources) {
  const category = categories[source.sectionCode];
  if (!category || known.has(source.externalId)) continue;
  if (source.error || !source.text || !source.textSha256 || !category.valid(source)) {
    throw new Error(`Revisión estructural insegura: ${source.externalId}`);
  }
  const digest = createHash('sha256').update(source.text).digest('hex');
  if (digest !== source.textSha256) throw new Error(`Fuente modificada: ${source.externalId}`);
  proposals.push({ externalId: source.externalId, relevant: false,
    reason: category.reason, sources: [source.officialUrl] });
}

console.log(`Propuestas estructurales: ${proposals.length}`);
console.log(JSON.stringify(Object.fromEntries(
  Object.keys(categories).map(section => [section, proposals.filter(proposal =>
    sources.find(source => source.externalId === proposal.externalId)?.sectionCode === section).length]))));

if (process.argv.includes('--apply')) {
  labels.labels.push(...proposals);
  writeFileSync(labelsPath, `${JSON.stringify(labels, null, 2)}\n`);
  console.log(`Etiquetas guardadas: ${labels.labels.length}`);
}
