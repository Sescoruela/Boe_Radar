import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CatalogState } from './catalog-state';
import { PublicationDetail, PublicationFilters, PublicationSearchResult } from './publications';

describe('CatalogState', () => {
  let state: CatalogState;
  let http: HttpTestingController;
  const filters: PublicationFilters = {
    query: '', section: '', dateFrom: '', dateTo: '', page: 1, pageSize: 12, businessSignalsOnly: true,
  };
  const page: PublicationSearchResult = {
    items: [], page: 1, pageSize: 12, totalItems: 0, totalPages: 0,
    evidenceAsOf: '2026-10-04T08:00:00Z',
  };
  const detail: PublicationDetail = {
    id: 'a', externalId: 'BOE-A-2026-1', title: 'Ayuda', publicationDate: '2026-10-01',
    sectionCode: '3', sectionName: 'Otras disposiciones', department: 'Ministerio',
    departmentCode: '1', issueNumber: '1',
  };
  const searchRequest = () => http.expectOne(request => request.url === '/api/v1/publications');

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CatalogState, provideHttpClient(), provideHttpClientTesting()],
    });
    state = TestBed.inject(CatalogState);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    state.ngOnDestroy();
    http.verify();
  });

  it('cancels a superseded search without clearing the current loading state', () => {
    state.search(filters, null);
    const previous = searchRequest();
    state.search({ ...filters, query: 'pymes' }, null);
    expect(previous.cancelled).toBe(true);
    expect(state.loading()).toBe(true);
    const current = searchRequest();
    expect(current.request.params.get('query')).toBe('pymes');
    current.flush(page);
    expect(state.result()).toEqual(page);
    expect(state.loading()).toBe(false);
  });

  it('cancels the pending search when dates become invalid, without a new request', () => {
    state.search(filters, null);
    const previous = searchRequest();
    state.search({ ...filters, dateFrom: '2026-10-04', dateTo: '2026-10-01' }, null);
    expect(previous.cancelled).toBe(true);
    expect(state.loading()).toBe(false);
    expect(state.result()).toBeNull();
    expect(state.error()).toContain('Desde');
    http.expectNone(request => request.url === '/api/v1/publications');
  });

  it('keeps the evidence snapshot between personalized pages but resets it for a new search', () => {
    const profile = { businessType: 'sme', activity: 'retail', territory: 'baleares' } as const;
    state.search(filters, profile);
    http.expectOne('/api/v1/publications/personalized').flush(page);
    state.search({ ...filters, page: 2 }, profile, true);
    const second = http.expectOne('/api/v1/publications/personalized');
    expect(second.request.body.search.evidenceAsOf).toBe(page.evidenceAsOf);
    expect(second.request.body.search.page).toBe(2);
    second.flush({ ...page, page: 2 });
    state.search(filters, profile);
    const fresh = http.expectOne('/api/v1/publications/personalized');
    expect(fresh.request.body.search.evidenceAsOf).toBeNull();
    fresh.flush(page);
  });

  it('cancels the previous detail when another card is opened', () => {
    state.openDetails('a', null);
    const previous = http.expectOne('/api/v1/publications/a');
    state.openDetails('b', null);
    expect(previous.cancelled).toBe(true);
    expect(state.detailLoading()).toBe(true);
    const current = http.expectOne('/api/v1/publications/b');
    state.closeDetails();
    expect(current.cancelled).toBe(true);
    expect(state.detailLoading()).toBe(false);
    expect(state.selected()).toBeNull();
  });

  it('cancels a source review and clears all detail state when the card is closed', () => {
    state.openDetails('a', null);
    http.expectOne('/api/v1/publications/a').flush(detail);
    const review = http.expectOne('/api/v1/source-review/BOE-A-2026-1');
    expect(state.sourceReviewLoading()).toBe(true);
    state.closeDetails();
    expect(review.cancelled).toBe(true);
    expect(state.sourceReviewLoading()).toBe(false);
    expect(state.detailLoading()).toBe(false);
    expect(state.selected()).toBeNull();
    expect(state.selectedProfileMatch()).toBeNull();
    expect(state.sourceReviewError()).toBe(false);
  });

  it('keeps the official detail available if source review fails', () => {
    state.openDetails('a', null);
    http.expectOne('/api/v1/publications/a').flush(detail);
    http.expectOne('/api/v1/source-review/BOE-A-2026-1')
      .flush('Unavailable', { status: 503, statusText: 'Unavailable' });
    expect(state.selected()).toEqual(detail);
    expect(state.sourceReviewError()).toBe(true);
    expect(state.sourceReviewLoading()).toBe(false);
    expect(state.detailLoading()).toBe(false);
  });

  it('closes a stale personalized card when the profile or search changes', () => {
    const profile = { businessType: 'sme', activity: 'retail', territory: 'baleares' } as const;
    state.openDetails('a', profile);
    http.expectOne('/api/v1/publications/a').flush(detail);
    const review = http.expectOne('/api/v1/source-review/BOE-A-2026-1/personalized');
    expect(review.request.body).toEqual(profile);
    state.search(filters, null);
    expect(review.cancelled).toBe(true);
    expect(state.selected()).toBeNull();
    searchRequest().flush(page);
  });

  it('cancels pending reads when the screen is destroyed', () => {
    state.search(filters, null);
    const search = searchRequest();
    state.openDetails('a', null);
    const card = http.expectOne('/api/v1/publications/a');
    TestBed.resetTestingModule();
    expect(search.cancelled).toBe(true);
    expect(card.cancelled).toBe(true);
    expect(state.loading()).toBe(false);
    expect(state.detailLoading()).toBe(false);
  });
});
