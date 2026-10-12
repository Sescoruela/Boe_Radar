import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { App } from './app';
import { FOLLOW_UP_KEY } from './follow-up';

describe('App catalog wiring', () => {
  it('shows a source contrast outside collapsed technical details without reordering results', async () => {
    const fixture = TestBed.createComponent(App);
    const http = TestBed.inject(HttpTestingController);
    fixture.componentInstance.businessProfile.set({ businessType: 'sme', activity: 'technology', territory: 'canarias' });
    fixture.componentInstance.personalizedView.set(true);
    fixture.detectChanges();
    const publication = { id: 'a', externalId: 'BOE-A-2026-1', title: 'Ayuda para innovación',
      publicationDate: '2026-10-01', sectionCode: '3', sectionName: 'Otras disposiciones', department: 'Ministerio' };
    http.expectOne('/api/v1/publications/personalized').flush({ items: [publication], page: 1, pageSize: 12, totalItems: 1, totalPages: 1 });
    http.expectOne('/api/v1/catalog/status').flush({ totalPublications: 1, latestPublicationDate: null, emailAlertsEnabled: false });
    await fixture.whenStable();
    fixture.componentInstance.openDetails('a');
    http.expectOne('/api/v1/publications/a').flush({ ...publication, departmentCode: '1', issueNumber: '1' });
    const review = http.expectOne('/api/v1/source-review/BOE-A-2026-1/personalized');
    expect(review.request.body).toEqual({ businessType: 'sme', activity: 'technology', territory: 'canarias' });
    review.flush({ sourceHash: 'hash', groups: [], kind: 'grant', nextStep: 'Revisa las bases', profileContrast: { sourceHash: 'hash', dimensions: [
      { key: 'businessType', label: 'Tipo de negocio', status: 'restriction', origin: 'source', message: 'Posible desajuste, no exclusión confirmada', quotes: ['Beneficiarias exclusivamente las entidades locales.'] },
    ] } });
    await fixture.whenStable();
    const contrast = fixture.nativeElement.querySelector('[role="dialog"] app-source-profile');
    expect(contrast.closest('details')).toBeNull();
    expect(contrast.textContent).toContain('Posible restricción');
    expect(fixture.componentInstance.result()?.items[0].id).toBe('a');
    http.expectNone('/api/v1/publications/personalized');
    fixture.destroy();
    http.verify();
  });

  it('identifies unspecified profile fields without making them mandatory', () => {
    const fixture = TestBed.createComponent(App);
    fixture.componentInstance.businessProfile.set({ businessType: 'sme', activity: 'other', territory: 'all' });
    expect(fixture.componentInstance.profileMissingFields()).toEqual(['actividad principal', 'territorio de actividad']);
    fixture.componentInstance.businessProfile.set({ businessType: 'sme', activity: 'retail', territory: 'canarias' });
    expect(fixture.componentInstance.profileMissingFields()).toEqual([]);
    fixture.destroy();
  });
  it('saves from a card, changes state in its detail and restores from follow-up', async () => {
    const fixture = TestBed.createComponent(App);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    const publication = { id: 'a', externalId: 'BOE-A-2026-1', title: 'Ayuda de prueba',
      publicationDate: '2026-10-01', sectionCode: '3', sectionName: 'Otras disposiciones', department: 'Ministerio' };
    http.expectOne(request => request.url === '/api/v1/publications').flush({ items: [publication], page: 1, pageSize: 12, totalItems: 1, totalPages: 1 });
    http.expectOne('/api/v1/catalog/status').flush({ totalPublications: 1, latestPublicationDate: null, emailAlertsEnabled: false });
    await fixture.whenStable();
    const button = Array.from(fixture.nativeElement.querySelectorAll('.publication-card button'))
      .find((element: any) => element.textContent.includes('Guardar publicación')) as HTMLButtonElement;
    button.click();
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('#seguimiento').textContent).toContain(publication.title);
    expect(JSON.parse(localStorage.getItem(FOLLOW_UP_KEY)!).entries[0].status).toBe('review');
    fixture.componentInstance.openDetails('a');
    http.expectOne('/api/v1/publications/a').flush({ ...publication, departmentCode: '1', issueNumber: '1' });
    http.expectOne('/api/v1/source-review/BOE-A-2026-1').flush({ groups: [], kind: 'general', nextStep: 'Revisa la fuente.' });
    await fixture.whenStable();
    const state = fixture.nativeElement.querySelector('[role="dialog"] app-follow-up-controls select') as HTMLSelectElement;
    state.value = 'preparing';
    state.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(fixture.componentInstance.followUp.find(publication.externalId)?.status).toBe('preparing');
    fixture.componentInstance.closeDetails();
    await fixture.whenStable();
    const panel = fixture.nativeElement.querySelector('#seguimiento') as HTMLElement;
    Array.from(panel.querySelectorAll('article button')).find((element: any) => element.textContent === 'Descartar')!.dispatchEvent(new Event('click'));
    await fixture.whenStable();
    expect(panel.querySelector('article')).toBeNull();
    Array.from(panel.querySelectorAll('.filters button')).find((element: any) => element.textContent === 'Descartado')!.dispatchEvent(new Event('click'));
    await fixture.whenStable();
    Array.from(panel.querySelectorAll('article button')).find((element: any) => element.textContent === 'Recuperar publicación')!.dispatchEvent(new Event('click'));
    await fixture.whenStable();
    expect(fixture.componentInstance.followUp.find(publication.externalId)?.status).toBe('preparing');
    fixture.destroy();
    http.verify();
  });

  it('keeps saved items and their official links usable when the catalog and detail are unavailable', async () => {
    const fixture = TestBed.createComponent(App);
    const http = TestBed.inject(HttpTestingController);
    const publication = { id: 'saved', externalId: 'BOE-A-2026-2', title: 'Publicación guardada',
      publicationDate: '2026-10-01', sectionCode: '1', sectionName: 'Disposiciones', department: 'Ministerio' };
    fixture.componentInstance.followUp.setStatus(publication, 'interested');
    fixture.detectChanges();
    http.expectOne(request => request.url === '/api/v1/publications').flush('', { status: 503, statusText: 'Unavailable' });
    http.expectOne('/api/v1/catalog/status').flush('', { status: 503, statusText: 'Unavailable' });
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('#seguimiento').textContent).toContain(publication.title);
    fixture.nativeElement.querySelector('#seguimiento article button').click();
    http.expectOne('/api/v1/publications/saved').flush('', { status: 503, statusText: 'Unavailable' });
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('[role="dialog"] a').href).toContain('BOE-A-2026-2');
    fixture.componentInstance.retryDetails();
    http.expectOne('/api/v1/publications/saved').flush('', { status: 503, statusText: 'Unavailable' });
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('[role="dialog"] a').href).toContain('BOE-A-2026-2');
    fixture.destroy();
    http.verify();
  });
  it('shows intent-first filters and offers safe recovery from an empty result', async () => {
    const fixture = TestBed.createComponent(App);
    const http = TestBed.inject(HttpTestingController);
    fixture.componentInstance.filters.intent = 'grants';
    fixture.componentInstance.filters.query = 'digitalización';
    fixture.detectChanges();
    http.expectOne(request => request.url === '/api/v1/publications' && request.params.get('intent') === 'grants')
      .flush({ items: [], page: 1, pageSize: 12, totalItems: 0, totalPages: 0 });
    http.expectOne('/api/v1/catalog/status').flush({ totalPublications: 0, latestPublicationDate: null, emailAlertsEnabled: false });
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Publicado desde');
    expect(fixture.nativeElement.querySelector('.advanced-filters').open).toBe(false);
    expect(fixture.nativeElement.textContent).toContain('No significa que no existan ayudas');
    fixture.componentInstance.filters.intent = 'tax'; // Unsubmitted edit must not mislabel displayed results.
    fixture.detectChanges();
    expect(fixture.componentInstance.appliedIntentLabel()).toBe('Ayudas y subvenciones');
    fixture.componentInstance.clearIntent();
    const wider = http.expectOne(request => request.url === '/api/v1/publications');
    expect(wider.request.params.has('intent')).toBe(false);
    expect(wider.request.params.get('query')).toBe('digitalización');
    wider.flush({ items: [], page: 1, pageSize: 12, totalItems: 0, totalPages: 0 });
    fixture.componentInstance.broadenSearch();
    const all = http.expectOne(request => request.url === '/api/v1/publications');
    expect(all.request.params.has('query')).toBe(false);
    expect(all.request.params.has('businessSignalsOnly')).toBe(false);
    all.flush({ items: [], page: 1, pageSize: 12, totalItems: 0, totalPages: 0 });
    fixture.destroy();
    http.verify();
  });
  beforeEach(() => {
    localStorage.clear();
    window.history.replaceState(null, '', '/');
    TestBed.configureTestingModule({
      imports: [App], providers: [provideHttpClient(), provideHttpClientTesting()],
    });
  });

  it('does not send the browser profile without explicit alert-profile opt-in', () => {
    const fixture = TestBed.createComponent(App);
    const http = TestBed.inject(HttpTestingController);
    fixture.componentInstance.businessProfile.set({ businessType: 'sme', activity: 'retail', territory: 'baleares' });
    fixture.componentInstance.subscriptionEmail = 'test@example.invalid';
    fixture.componentInstance.consent = true;
    fixture.componentInstance.submitSubscription();
    const plain = http.expectOne('/api/v1/subscriptions');
    expect(plain.request.body.profile).toBeNull();
    plain.flush({});
    fixture.componentInstance.personalizedAlerts = true;
    fixture.componentInstance.copyRadarProfileToAlerts();
    fixture.componentInstance.submitSubscription();
    const personalized = http.expectOne('/api/v1/subscriptions');
    expect(personalized.request.body.profile).toEqual({ businessType: 'sme', activity: 'retail', territory: 'baleares' });
    personalized.flush({});
    fixture.destroy();
    http.verify();
  });

  it('blocks registration without consent or with an invalid alert profile', () => {
    const fixture = TestBed.createComponent(App);
    const http = TestBed.inject(HttpTestingController);
    fixture.componentInstance.submitSubscription();
    http.expectNone('/api/v1/subscriptions');
    fixture.componentInstance.consent = true;
    fixture.componentInstance.personalizedAlerts = true;
    fixture.componentInstance.alertProfileDraft.territory = 'inventado';
    fixture.componentInstance.submitSubscription();
    http.expectNone('/api/v1/subscriptions');
    fixture.destroy();
    http.verify();
  });

  it('loads and removes the subscription profile without changing the browser radar profile', async () => {
    window.history.replaceState(null, '', '/#manage=test-management-token');
    const fixture = TestBed.createComponent(App);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    http.expectOne(request => request.url === '/api/v1/publications').flush({ items: [], page: 1, pageSize: 12, totalItems: 0, totalPages: 0 });
    http.expectOne('/api/v1/catalog/status').flush({ latestPublicationDate: null, totalPublications: 0, emailAlertsEnabled: false });
    const manage = http.expectOne('/api/v1/subscriptions/me');
    expect(manage.request.headers.get('X-Management-Token')).toBe('test-management-token');
    manage.flush({ email: 'test@example.invalid', status: 'Active', preferences: {
      categories: [], keywords: [], digestHour: 8, profile: { businessType: 'sme', activity: 'retail', territory: 'baleares' },
    } });
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('este perfil se almacena en el servidor');
    expect(window.location.hash).toBe('');
    expect(fixture.componentInstance.personalizedAlerts).toBe(true);
    expect(fixture.componentInstance.businessProfile()).toBeNull();
    fixture.componentInstance.personalizedAlerts = false;
    fixture.componentInstance.savePreferences();
    const update = http.expectOne('/api/v1/subscriptions/me');
    expect(update.request.method).toBe('PUT');
    expect(update.request.body.profile).toBeNull();
    update.flush(null, { status: 204, statusText: 'No Content' });
    expect(fixture.componentInstance.managedSubscription()?.preferences.profile).toBeNull();
    expect(fixture.componentInstance.businessProfile()).toBeNull();
    fixture.destroy();
    http.verify();
  });

  it('renders the catalog and opens/closes a card using the scoped state', async () => {
    const fixture = TestBed.createComponent(App);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    const publication = {
      id: 'a', externalId: 'BOE-A-2026-1', title: 'Ayuda de prueba para pymes',
      publicationDate: '2026-10-01', sectionCode: '3', sectionName: 'Otras disposiciones',
      department: 'Ministerio', departmentCode: '1', issueNumber: '1',
    };
    http.expectOne(request => request.url === '/api/v1/publications').flush({
      items: [publication], page: 1, pageSize: 12, totalItems: 1, totalPages: 1,
    });
    http.expectOne('/api/v1/catalog/status').flush({
      latestPublicationDate: '2026-10-01', totalPublications: 1, emailAlertsEnabled: false,
    });
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain(publication.title);
    fixture.componentInstance.openDetails('a');
    http.expectOne('/api/v1/publications/a').flush(publication);
    const review = http.expectOne('/api/v1/source-review/BOE-A-2026-1');
    await fixture.whenStable();
    expect(fixture.componentInstance.selected()?.id).toBe('a');
    fixture.componentInstance.closeDetails();
    expect(review.cancelled).toBe(true);
    expect(fixture.componentInstance.detailLoading()).toBe(false);
    fixture.destroy();
    http.verify();
  });

  it('cancels catalog and status reads when the root component is destroyed', () => {
    const fixture = TestBed.createComponent(App);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    const search = http.expectOne(request => request.url === '/api/v1/publications');
    const status = http.expectOne('/api/v1/catalog/status');
    fixture.destroy();
    expect(search.cancelled).toBe(true);
    expect(status.cancelled).toBe(true);
    http.verify();
  });

  it('allows closing a loading dialog with Escape and keeps the catalog and source link available on errors', async () => {
    const fixture = TestBed.createComponent(App);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    const publication = { id: 'a', externalId: 'BOE-A-2026-1', title: 'Orden de 1 de octubre por la que se modifican las bases.',
      publicationDate: '2026-10-01', sectionCode: '3', sectionName: 'Otras disposiciones', department: 'Ministerio' };
    http.expectOne(request => request.url === '/api/v1/publications').flush({
      items: [publication], page: 1, pageSize: 12, totalItems: 1, totalPages: 1,
    });
    http.expectOne('/api/v1/catalog/status').flush({ latestPublicationDate: null, totalPublications: 1, emailAlertsEnabled: false });
    await fixture.whenStable();
    fixture.componentInstance.openDetails('a');
    const loading = http.expectOne('/api/v1/publications/a');
    await fixture.whenStable();
    const dialog = fixture.nativeElement.querySelector('[role="dialog"]');
    expect(dialog.querySelector('button[aria-label="Cerrar"]')).not.toBeNull();
    expect(dialog.querySelector('a').href).toContain('boe.es');
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    await fixture.whenStable();
    expect(loading.cancelled).toBe(true);
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeNull();
    fixture.componentInstance.openDetails('a');
    http.expectOne('/api/v1/publications/a').flush('Unavailable', { status: 503, statusText: 'Unavailable' });
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('.publication-card')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[role="dialog"]').textContent).toContain('Reintentar ficha');
    fixture.componentInstance.retryDetails();
    http.expectOne('/api/v1/publications/a').flush({ ...publication, departmentCode: '1', issueNumber: '1' });
    http.expectOne('/api/v1/source-review/BOE-A-2026-1').flush({ groups: [], kind: 'general', nextStep: 'Comprueba las condiciones.' });
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('#detail-title').textContent).toBe('Orden: Se modifican las bases.');
    expect(fixture.nativeElement.querySelector('.technical-details').textContent).toContain(publication.title);
    expect(fixture.nativeElement.querySelector('app-decision-summary')).not.toBeNull();
    fixture.destroy();
    http.verify();
  });
});
