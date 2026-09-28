import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { fetchPublications } from './evaluate-business-signals.mjs';

const nearMissPattern = /ayud|subvenci|bonificaci|financiaci|pr[eé]stam|tributari|fiscal|impuesto|cotizaci|aut[oó]nom|empresa/i;
const defaultSeed = 'boe-radar-quality-v1';

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}

export function buildReviewQueue(publications, signalIds, count = 200,
  seed = defaultSeed) {
  if (!Number.isInteger(count) || count < 1 || count > publications.length) {
    throw new Error('El tamaño solicitado debe estar entre 1 y el total del catálogo.');
  }
  const byId = new Map(publications.map(item => [item.externalId, item]));
  if (byId.size !== publications.length) throw new Error('El catálogo contiene IDs duplicados.');
  const signals = new Set(signalIds);
  for (const id of signals) {
    if (!byId.has(id)) throw new Error(`La señal no figura en el catálogo: ${id}`);
  }
  if (signals.size > count) throw new Error('La cola debe incluir todas las señales mostradas.');

  const rank = item => sha256(`${seed}\0${item.externalId}`);
  const sorted = items => items.sort((a, b) =>
    rank(a).localeCompare(rank(b)) || a.externalId.localeCompare(b.externalId));
  const shown = sorted(publications.filter(item => signals.has(item.externalId)));
  const nearMisses = sorted(publications.filter(item => !signals.has(item.externalId) &&
    nearMissPattern.test(item.title)));
  const chosenNearMisses = nearMisses.slice(0, count - shown.length);
  const remaining = count - shown.length - chosenNearMisses.length;

  // Round-robin across section and day, then use a seeded hash inside each
  // bucket. This deliberately oversamples uncommon sections: it is an audit
  // queue, not a representative sample of the full BOE.
  const buckets = new Map();
  for (const item of publications) {
    if (signals.has(item.externalId) || nearMissPattern.test(item.title)) continue;
    const key = `${item.sectionCode}|${item.publicationDate}`;
    if (!buckets.has(key)) buckets.set(key, []);
    buckets.get(key).push(item);
  }
  for (const bucket of buckets.values()) sorted(bucket);
  const keys = [...buckets.keys()].sort();
  const background = [];
  while (background.length < remaining) {
    let progress = false;
    for (const key of keys) {
      if (background.length === remaining) break;
      const item = buckets.get(key).shift();
      if (item) { background.push(item); progress = true; }
    }
    if (!progress) throw new Error('No quedan suficientes publicaciones para completar la cola.');
  }

  const entries = [
    ...shown.map(item => ({ item, stratum: 'shown' })),
    ...chosenNearMisses.map(item => ({ item, stratum: 'near_miss' })),
    ...background.map(item => ({ item, stratum: 'background' })),
  ].map(({ item, stratum }) => ({
    externalId: item.externalId,
    publicationDate: item.publicationDate,
    sectionCode: item.sectionCode,
    title: item.title,
    stratum,
    officialUrl: `https://www.boe.es/diario_boe/txt.php?id=${encodeURIComponent(item.externalId)}`,
  }));

  return {
    seed,
    catalogSha256: sha256([...byId.keys()].sort().join('\n')),
    catalogCount: publications.length,
    requestedCount: count,
    counts: { shown: shown.length, nearMiss: chosenNearMisses.length,
      background: background.length },
    entries,
  };
}

async function main() {
  const baseUrl = new URL(process.argv[2] ?? 'http://127.0.0.1:5080');
  if (!['http:', 'https:'].includes(baseUrl.protocol)) throw new Error('La URL debe usar HTTP o HTTPS.');
  const count = Number(process.argv[3] ?? 200);
  const outputPath = resolve(process.argv[4] ??
    fileURLToPath(new URL('../data/business-signals-review-queue.json', import.meta.url)));
  const reviewed = JSON.parse(readFileSync(
    fileURLToPath(new URL('../data/business-signals-reviewed.json', import.meta.url)), 'utf8'));
  const window = { dateFrom: process.argv[5] ?? reviewed.dateFrom,
    dateTo: process.argv[6] ?? reviewed.dateTo };
  if (!/^\d{4}-\d{2}-\d{2}$/.test(window.dateFrom) ||
      !/^\d{4}-\d{2}-\d{2}$/.test(window.dateTo) || window.dateFrom > window.dateTo) {
    throw new Error('La ventana debe usar fechas AAAA-MM-DD en orden.');
  }
  const [publications, signals] = await Promise.all([
    fetchPublications(baseUrl, window, false),
    fetchPublications(baseUrl, window, true),
  ]);
  const queue = buildReviewQueue(publications, signals.map(item => item.externalId), count);
  writeFileSync(outputPath, `${JSON.stringify({ ...window, ...queue }, null, 2)}\n`,
    { flag: 'wx' });
  console.log(`Cola creada: ${outputPath}`);
  console.log(JSON.stringify(queue.counts));
}

if (process.argv[1] && fileURLToPath(import.meta.url) === resolve(process.argv[1])) {
  main().catch(error => { console.error(error.message); process.exitCode = 1; });
}
