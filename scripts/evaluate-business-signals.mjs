import { createHash } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export function evaluateSignals(labels, catalogIds, signalIds) {
  const catalog = new Set(catalogIds);
  const signals = new Set(signalIds);
  for (const externalId of signals) {
    if (!catalog.has(externalId)) {
      throw new Error(`La señal no figura en el catálogo de la misma ventana: ${externalId}`);
    }
  }
  const seen = new Set();
  const result = { truePositive: 0, falsePositive: 0, falseNegative: 0, trueNegative: 0,
    falsePositiveIds: [], falseNegativeIds: [] };

  for (const entry of labels) {
    if (!/^BOE-[A-Z]-\d{4}-\d{1,10}$/.test(entry.externalId) ||
        typeof entry.relevant !== 'boolean' || seen.has(entry.externalId)) {
      throw new Error(`Etiqueta inválida o duplicada: ${entry.externalId}`);
    }
    seen.add(entry.externalId);
    if (!catalog.has(entry.externalId)) {
      throw new Error(`Falta en el catálogo evaluado: ${entry.externalId}`);
    }
    const selected = signals.has(entry.externalId);
    if (entry.relevant && selected) result.truePositive++;
    else if (!entry.relevant && selected) {
      result.falsePositive++;
      result.falsePositiveIds.push(entry.externalId);
    } else if (entry.relevant) {
      result.falseNegative++;
      result.falseNegativeIds.push(entry.externalId);
    } else result.trueNegative++;
  }

  const precisionDenominator = result.truePositive + result.falsePositive;
  const recallDenominator = result.truePositive + result.falseNegative;
  return {
    reviewed: labels.length,
    ...result,
    precision: precisionDenominator ? result.truePositive / precisionDenominator : null,
    recall: recallDenominator ? result.truePositive / recallDenominator : null,
    accuracy: labels.length ? (result.truePositive + result.trueNegative) / labels.length : null,
  };
}

export function validateReviewQueue(queue, sample, catalogIds) {
  const digest = createHash('sha256').update([...catalogIds].sort().join('\n')).digest('hex');
  const queueIds = new Set(queue.entries.map(entry => entry.externalId));
  if (queue.dateFrom !== sample.dateFrom || queue.dateTo !== sample.dateTo ||
      queue.catalogSha256 !== digest || queueIds.size !== queue.entries.length ||
      queue.entries.length !== queue.requestedCount) {
    throw new Error('La cola no corresponde a este catálogo; regenera o revisa la muestra.');
  }
  for (const label of sample.labels) {
    if (!queueIds.has(label.externalId)) {
      throw new Error(`La etiqueta no pertenece a la cola: ${label.externalId}`);
    }
    const officialSource = `https://www.boe.es/diario_boe/txt.php?id=${label.externalId}`;
    if (typeof label.reason !== 'string' || !label.reason.trim() ||
        !Array.isArray(label.sources) || !label.sources.includes(officialSource)) {
      throw new Error(`La etiqueta carece de motivo o enlace BOE verificable: ${label.externalId}`);
    }
  }
  return { queueCount: queue.entries.length, queueReviewed: sample.labels.length,
    queueRemaining: queue.entries.length - sample.labels.length };
}

export async function fetchPublications(baseUrl, sample, businessSignalsOnly) {
  const items = [];
  for (let page = 1; ; page++) {
    const url = new URL('/api/v1/publications', baseUrl);
    url.searchParams.set('dateFrom', sample.dateFrom);
    url.searchParams.set('dateTo', sample.dateTo);
    url.searchParams.set('businessSignalsOnly', String(businessSignalsOnly));
    url.searchParams.set('pageSize', '100');
    url.searchParams.set('page', String(page));
    const response = await fetch(url);
    if (!response.ok) throw new Error(`La API devolvió ${response.status}: ${url}`);
    const result = await response.json();
    items.push(...result.items);
    if (page >= result.totalPages) {
      if (items.length !== result.totalItems ||
          new Set(items.map(item => item.externalId)).size !== items.length) {
        throw new Error('El catálogo cambió durante la paginación; repite la evaluación.');
      }
      return items;
    }
  }
}

async function main() {
  const baseUrl = new URL(process.argv[2] ?? 'http://127.0.0.1:5080');
  if (!['http:', 'https:'].includes(baseUrl.protocol)) throw new Error('La URL debe usar HTTP o HTTPS.');
  const labelsPath = process.argv[3]
    ? resolve(process.argv[3])
    : fileURLToPath(new URL('../data/business-signals-reviewed.json', import.meta.url));
  const sample = JSON.parse(readFileSync(labelsPath, 'utf8'));
  if (!/^\d{4}-\d{2}-\d{2}$/.test(sample.dateFrom) ||
      !/^\d{4}-\d{2}-\d{2}$/.test(sample.dateTo) ||
      sample.dateFrom > sample.dateTo || !Array.isArray(sample.labels)) {
    throw new Error('La ventana o las etiquetas de la muestra no son válidas.');
  }
  const [catalog, signals] = await Promise.all([
    fetchPublications(baseUrl, sample, false),
    fetchPublications(baseUrl, sample, true),
  ]);
  const queuePath = process.argv[4] ? resolve(process.argv[4])
    : fileURLToPath(new URL('../data/business-signals-review-queue.json', import.meta.url));
  const progress = existsSync(queuePath)
    ? validateReviewQueue(JSON.parse(readFileSync(queuePath, 'utf8')), sample,
      catalog.map(item => item.externalId))
    : {};
  console.log(JSON.stringify({ baseUrl: baseUrl.origin, dateFrom: sample.dateFrom,
    dateTo: sample.dateTo, catalogCount: catalog.length, signalCount: signals.length,
    ...progress,
    ...evaluateSignals(sample.labels, catalog.map(item => item.externalId),
      signals.map(item => item.externalId)),
    note: 'Muestra dirigida: estas métricas no estiman el rendimiento global del BOE.' }, null, 2));
}

if (process.argv[1] && fileURLToPath(import.meta.url) === resolve(process.argv[1])) {
  main().catch(error => { console.error(error.message); process.exitCode = 1; });
}
