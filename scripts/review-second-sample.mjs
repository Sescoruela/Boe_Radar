// One-off assisted audit of the frozen 2026-09-23 sample, not production rules.
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';

const queue = JSON.parse(readFileSync('data/business-signals-review-queue-v2.json', 'utf8'));
const sources = JSON.parse(readFileSync('.tmp/business-review-sources-v2.json', 'utf8'));
const byId = new Map(sources.map(source => [source.externalId, source]));
const specific = {
  'BOE-A-2026-19752': 'Premio económico con candidatura de terceros; el usuario ha excluido los premios del alcance actual del MVP.',
  'BOE-B-2026-30719': 'Las entidades beneficiarias son órganos, organismos e instituciones del sector público estatal; no está abierta a autónomos ni pymes privadas.',
  'BOE-A-2026-19750': 'El panel exige pertenecer a cuerpos docentes de escuelas oficiales de idiomas; no es una convocatoria para negocios privados.',
  'BOE-A-2026-19754': 'Actualiza la carta de servicios de la Biblioteca Nacional; no abre ayuda ni impone obligación empresarial.',
  'BOE-A-2026-19756': 'Publica tipos de cambio diarios, sin nueva ayuda ni modificación de obligaciones fiscales o empresariales.',
};
const labels = [];
for (const entry of queue.entries) {
  const source = byId.get(entry.externalId);
  if (!source || source.error || !source.text ||
      createHash('sha256').update(source.text).digest('hex') !== source.textSha256) {
    throw new Error(`Fuente ausente o modificada: ${entry.externalId}`);
  }
  let reason = specific[entry.externalId];
  if (!reason && entry.sectionCode === '2A') reason = 'Nombramiento, jubilación o provisión de personal público; fuera de ayudas y obligaciones para negocios.';
  if (!reason && entry.sectionCode === '2B') reason = 'Proceso de empleo o provisión de puestos públicos; fuera del alcance empresarial del MVP.';
  if (!reason && entry.sectionCode === '5A' && /Poder adjudicador/.test(source.text)) reason = 'Licitación o formalización de contratación pública, excluida del alcance acordado del MVP.';
  if (!reason && /plan de estudios/i.test(entry.title)) reason = 'Modifica un plan universitario concreto, sin nueva acción para autónomos o pymes.';
  if (!reason && /Convenio/i.test(entry.title)) reason = 'Convenio entre entidades identificadas; no abre convocatoria ni nueva obligación general para negocios.';
  if (!reason && /bien de inter[eé]s cultural/i.test(entry.title)) reason = 'Expediente de protección patrimonial delimitado a inmuebles o parcelas concretas; fuera del alcance general o sectorial acordado, aunque afecta a sus titulares.';
  if (!reason && /extrav[ií]o de t[ií]tulo/i.test(source.text)) reason = 'Notificación de título universitario individual; sin acción empresarial general o sectorial.';
  if (!reason && entry.sectionCode === '5B' && /licencia|concesi[oó]n|recuperaci[oó]n posesoria|investigaci[oó]n patrimonial|autorizaci[oó]n administrativa/i.test(entry.title)) reason = 'Licencia, concesión, autorización o expediente patrimonial de titulares o bienes concretos; excluido del alcance general o sectorial acordado.';
  if (!reason) throw new Error(`Falta decisión revisada para ${entry.externalId}`);
  labels.push({ externalId: entry.externalId, relevant: false, reason,
    sources: [entry.officialUrl], textSha256: source.textSha256 });
}
const result = { dateFrom: queue.dateFrom, dateTo: queue.dateTo,
  reviewMethod: 'Revisión documental asistida; no doble anotación jurídica independiente.',
  pending: [], labels };
if (process.argv.includes('--apply')) writeFileSync('data/business-signals-reviewed-v2.json', `${JSON.stringify(result, null, 2)}\n`);
console.log(JSON.stringify({ reviewed: labels.length, pending: result.pending }));
