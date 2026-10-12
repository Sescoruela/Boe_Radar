import { Injectable, computed, signal } from '@angular/core';
import { PublicationListItem } from './publications';

export const FOLLOW_UP_KEY = 'boe-radar.follow-up.v1';
export const followUpStates = [
  { value: 'review', label: 'Por revisar' },
  { value: 'interested', label: 'Me interesa' },
  { value: 'preparing', label: 'En preparación' },
  { value: 'completed', label: 'Completado' },
  { value: 'discarded', label: 'Descartado' },
] as const;
export type FollowUpStatus = typeof followUpStates[number]['value'];
export interface FollowUpEntry {
  publication: PublicationListItem;
  status: FollowUpStatus;
  previousStatus?: Exclude<FollowUpStatus, 'discarded'>;
  savedAt: string;
  updatedAt: string;
}
function validStatus(value: unknown): value is FollowUpStatus {
  return followUpStates.some(state => state.value === value);
}
/** Only identification metadata: analyses and inferred deadlines must be refreshed. */
function snapshot(item: PublicationListItem): PublicationListItem {
  return { id: item.id, externalId: item.externalId, title: item.title,
    publicationDate: item.publicationDate, department: item.department,
    sectionCode: item.sectionCode, sectionName: item.sectionName };
}
function validEntry(value: unknown): value is FollowUpEntry {
  if (!value || typeof value !== 'object') return false;
  const entry = value as FollowUpEntry;
  const item = entry.publication;
  const previousStatus: unknown = entry.previousStatus;
  return !!item && ['id', 'externalId', 'title', 'publicationDate', 'department', 'sectionCode', 'sectionName']
    .every(key => typeof item[key as keyof PublicationListItem] === 'string'
      && (item[key as keyof PublicationListItem] as string).length <= 10000)
    && /^[a-zA-Z0-9-]+$/.test(item.id) && /^BOE-[AB]-\d{4}-\d+$/.test(item.externalId)
    && /^\d{4}-\d{2}-\d{2}$/.test(item.publicationDate)
    && Number.isFinite(Date.parse(item.publicationDate + 'T00:00:00Z'))
    && new Date(item.publicationDate + 'T00:00:00Z').toISOString().slice(0, 10) === item.publicationDate
    && validStatus(entry.status)
    && (previousStatus === undefined || (validStatus(previousStatus) && previousStatus !== 'discarded'))
    && typeof entry.savedAt === 'string' && Number.isFinite(Date.parse(entry.savedAt))
    && typeof entry.updatedAt === 'string' && Number.isFinite(Date.parse(entry.updatedAt));
}
@Injectable({ providedIn: 'root' })
export class FollowUpStore {
  private readonly state = signal<FollowUpEntry[]>([]);
  private blocked = false;
  private sessionOnly = false;
  readonly entries = this.state.asReadonly();
  readonly activeCount = computed(() => this.entries().filter(entry => entry.status !== 'discarded').length);
  readonly warning = signal<string | null>(null);
  readonly message = signal<string | null>(null);
  constructor() { this.reload(); }
  reload(): void {
    let raw: string | null;
    try { raw = localStorage.getItem(FOLLOW_UP_KEY); }
    catch {
      this.sessionOnly = true;
      this.warning.set('No se ha podido leer el seguimiento del navegador. Los cambios podrían conservarse solo durante esta visita.');
      return;
    }
    try {
      const data = raw === null ? { version: 1, entries: [] } : JSON.parse(raw);
      if (data.version !== 1 || !Array.isArray(data.entries) || data.entries.length > 500
        || !data.entries.every(validEntry)
        || new Set(data.entries.map((entry: FollowUpEntry) => entry.publication.externalId)).size !== data.entries.length) throw new Error('Invalid storage');
      this.state.set(data.entries.map((entry: FollowUpEntry) => ({ ...entry, publication: snapshot(entry.publication) })));
      this.blocked = false;
      this.sessionOnly = false;
      this.warning.set(null);
    } catch {
      this.blocked = true;
      this.warning.set('No se puede leer el seguimiento guardado. No lo sobrescribiremos. Revisa los datos del sitio en este navegador antes de continuar.');
    }
  }
  find(externalId: string): FollowUpEntry | undefined {
    return this.entries().find(entry => entry.publication.externalId === externalId);
  }
  setStatus(publication: PublicationListItem, status: FollowUpStatus): void {
    // Merge with the latest stored entries before editing, unless this visit is already in memory-only mode.
    if (!this.sessionOnly) this.reload();
    if (this.blocked) {
      this.message.set('El seguimiento guardado no se ha modificado porque no se puede leer de forma segura.');
      return;
    }
    if (!validStatus(status)) return;
    const previous = this.find(publication.externalId);
    if (!previous && this.entries().length >= 500) {
      this.message.set('Has alcanzado el límite de 500 publicaciones guardadas.');
      return;
    }
    const now = new Date().toISOString();
    const entry: FollowUpEntry = {
      publication: snapshot(publication), status, savedAt: previous?.savedAt ?? now, updatedAt: now,
      previousStatus: status === 'discarded'
        ? previous?.status === 'discarded' ? previous.previousStatus : previous?.status ?? 'review'
        : undefined,
    };
    if (!validEntry(entry)) return;
    this.state.set([entry, ...this.entries().filter(item => item.publication.externalId !== publication.externalId)]);
    this.persist();
    this.message.set(`${publication.externalId}: ${followUpStates.find(state => state.value === status)!.label}.`);
  }
  restore(publication: PublicationListItem): void {
    if (!this.sessionOnly) this.reload();
    this.setStatus(publication, this.find(publication.externalId)?.previousStatus ?? 'review');
  }
  private persist(): void {
    if (this.sessionOnly) {
      this.warning.set('No se ha podido guardar el seguimiento. Este cambio se conserva solo durante esta visita; podría perderse al recargar.');
      return;
    }
    try {
      localStorage.setItem(FOLLOW_UP_KEY, JSON.stringify({ version: 1, entries: this.entries() }));
      this.warning.set(null);
    } catch {
      this.sessionOnly = true;
      this.warning.set('No se ha podido guardar el seguimiento. Este cambio se conserva solo durante esta visita; podría perderse al recargar.');
    }
  }
}
