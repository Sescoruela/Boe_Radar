import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { App } from './app';

describe('App catalog wiring', () => {
  beforeEach(() => {
    localStorage.clear();
    window.history.replaceState(null, '', '/');
    TestBed.configureTestingModule({
      imports: [App], providers: [provideHttpClient(), provideHttpClientTesting()],
    });
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
});
