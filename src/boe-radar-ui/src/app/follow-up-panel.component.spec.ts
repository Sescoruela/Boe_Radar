import { TestBed } from '@angular/core/testing';
import { FollowUpPanelComponent } from './follow-up-panel.component';
import { FollowUpStore } from './follow-up';
import { PublicationListItem } from './publications';

const publication: PublicationListItem = { id: 'a', externalId: 'BOE-A-2026-1', title: 'Ayuda para pymes',
  publicationDate: '2026-10-01', department: 'Ministerio', sectionCode: '3', sectionName: 'Otras disposiciones' };

describe('FollowUpPanelComponent', () => {
  beforeEach(() => { localStorage.clear(); TestBed.configureTestingModule({ imports: [FollowUpPanelComponent] }); });
  afterEach(() => localStorage.clear());
  it('explains the empty state and browser-only storage', () => {
    const fixture = TestBed.createComponent(FollowUpPanelComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Todavía no has guardado');
    expect(fixture.nativeElement.textContent).toContain('sin cuenta ni sincronización');
    expect(fixture.nativeElement.textContent).toContain('no indican una solicitud presentada');
  });
  it('filters statuses without changing saved data and separates discarded items', () => {
    const store = TestBed.inject(FollowUpStore);
    store.setStatus(publication, 'interested');
    store.setStatus({ ...publication, id: 'b', externalId: 'BOE-B-2026-2', title: 'Descartada' }, 'discarded');
    const fixture = TestBed.createComponent(FollowUpPanelComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('article')).toHaveLength(1);
    expect(fixture.nativeElement.textContent).toContain('1 publicaciones en seguimiento · 1 descartadas');
    fixture.componentInstance.filter.set('discarded');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('article').textContent).toContain('Descartada');
    fixture.componentInstance.filter.set('completed');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No hay publicaciones en este estado');
    expect(store.entries()).toHaveLength(2);
  });
  it('emits the saved publication and builds the official link from its identifier', () => {
    TestBed.inject(FollowUpStore).setStatus(publication, 'review');
    const fixture = TestBed.createComponent(FollowUpPanelComponent);
    let opened: PublicationListItem | undefined;
    fixture.componentInstance.openPublication.subscribe(value => opened = value);
    fixture.detectChanges();
    fixture.nativeElement.querySelector('article button').click();
    expect(opened?.externalId).toBe(publication.externalId);
    expect(fixture.nativeElement.querySelector('article a').href).toBe('https://www.boe.es/diario_boe/txt.php?id=BOE-A-2026-1');
  });
});
