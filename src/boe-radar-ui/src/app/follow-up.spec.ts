import { TestBed } from '@angular/core/testing';
import { FOLLOW_UP_KEY, FollowUpStore } from './follow-up';
import { PublicationListItem } from './publications';

const publication: PublicationListItem = { id: 'a', externalId: 'BOE-A-2026-1', title: 'Ayuda para pymes',
  publicationDate: '2026-10-01', department: 'Ministerio', sectionCode: '3', sectionName: 'Otras disposiciones' };

describe('FollowUpStore', () => {
  beforeEach(() => { localStorage.clear(); TestBed.configureTestingModule({}); });
  afterEach(() => { vi.restoreAllMocks(); localStorage.clear(); });
  it('persists identification metadata, deduplicates and recovers state in a new visit', () => {
    const store = TestBed.inject(FollowUpStore);
    store.setStatus({ ...publication, analysis: { summary: 'Old analysis', isRelevant: true, category: 'Grant', method: 'gemini', confidence: 1 } }, 'review');
    const savedAt = store.entries()[0].savedAt;
    store.setStatus({ ...publication, id: 'new-id' }, 'preparing');
    expect(store.entries()).toHaveLength(1);
    expect(store.entries()[0].savedAt).toBe(savedAt);
    expect(store.entries()[0].publication.analysis).toBeUndefined();
    const reloaded = new FollowUpStore();
    expect(reloaded.find(publication.externalId)?.status).toBe('preparing');
    expect(reloaded.find(publication.externalId)?.publication.id).toBe('new-id');
  });
  it('discards and restores the previous status, also for an unsaved publication', () => {
    const store = TestBed.inject(FollowUpStore);
    store.setStatus(publication, 'completed');
    store.setStatus(publication, 'discarded');
    store.setStatus(publication, 'discarded');
    expect(store.activeCount()).toBe(0);
    store.restore(publication);
    expect(store.find(publication.externalId)?.status).toBe('completed');
    const other = { ...publication, id: 'b', externalId: 'BOE-B-2026-2' };
    store.setStatus(other, 'discarded');
    store.restore(other);
    expect(store.find(other.externalId)?.status).toBe('review');
    expect(store.activeCount()).toBe(2);
  });
  it('keeps changes in memory but warns when saving is unavailable', () => {
    const store = TestBed.inject(FollowUpStore);
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('Quota'); });
    store.setStatus(publication, 'interested');
    expect(store.entries()).toHaveLength(1);
    expect(store.warning()).toContain('solo durante esta visita');
    expect(localStorage.getItem(FOLLOW_UP_KEY)).toBeNull();
  });
  it('handles a blocked storage read without crashing', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('Blocked'); });
    const store = new FollowUpStore();
    expect(store.entries()).toEqual([]);
    expect(store.warning()).toContain('No se ha podido leer');
    const write = vi.spyOn(Storage.prototype, 'setItem');
    store.setStatus(publication, 'review');
    expect(store.entries()).toHaveLength(1);
    expect(write).not.toHaveBeenCalled();
  });
  it('merges the latest stored entries before editing from a different tab', () => {
    const first = new FollowUpStore();
    const second = new FollowUpStore();
    first.setStatus(publication, 'review');
    second.setStatus({ ...publication, id: 'b', externalId: 'BOE-B-2026-2' }, 'interested');
    first.reload();
    expect(first.entries()).toHaveLength(2);
  });
  it.each(['{broken', JSON.stringify({ version: 2, entries: [] }),
    JSON.stringify({ version: 1, entries: [{ publication, status: 'invented' }] }), 'null'])
    ('does not overwrite corrupt or unsupported data: %s', raw => {
      localStorage.setItem(FOLLOW_UP_KEY, raw);
      const store = new FollowUpStore();
      store.setStatus(publication, 'review');
      expect(store.entries()).toHaveLength(0);
      expect(localStorage.getItem(FOLLOW_UP_KEY)).toBe(raw);
      expect(store.warning()).toContain('No lo sobrescribiremos');
    });
  it('reloads changes from another tab and rejects duplicate identities', () => {
    const store = TestBed.inject(FollowUpStore);
    const other = new FollowUpStore();
    other.setStatus(publication, 'interested');
    store.reload();
    expect(store.find(publication.externalId)?.status).toBe('interested');
    const entry = store.entries()[0];
    localStorage.setItem(FOLLOW_UP_KEY, JSON.stringify({ version: 1, entries: [entry, entry] }));
    store.reload();
    expect(store.warning()).toContain('No lo sobrescribiremos');
  });
  it.each(['2026-02-31', '2026-13-01', 'not-a-date'])('rejects an invalid stored publication date: %s', publicationDate => {
    const now = new Date().toISOString();
    const raw = JSON.stringify({ version: 1, entries: [{ publication: { ...publication, publicationDate }, status: 'review', savedAt: now, updatedAt: now }] });
    localStorage.setItem(FOLLOW_UP_KEY, raw);
    const store = new FollowUpStore();
    expect(store.entries()).toHaveLength(0);
    expect(store.warning()).toContain('No lo sobrescribiremos');
  });
  it('limits new entries to 500 but still allows changing existing states', () => {
    const now = new Date().toISOString();
    const entries = Array.from({ length: 500 }, (_, index) => ({
      publication: { ...publication, id: 'id-' + index, externalId: 'BOE-A-2026-' + index }, status: 'review', savedAt: now, updatedAt: now,
    }));
    localStorage.setItem(FOLLOW_UP_KEY, JSON.stringify({ version: 1, entries }));
    const store = new FollowUpStore();
    store.setStatus({ ...publication, externalId: 'BOE-A-2026-501' }, 'review');
    expect(store.entries()).toHaveLength(500);
    expect(store.message()).toContain('límite de 500');
    store.setStatus(entries[0].publication, 'completed');
    expect(store.find(entries[0].publication.externalId)?.status).toBe('completed');
  });
});
