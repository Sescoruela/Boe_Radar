import { TestBed } from '@angular/core/testing';
import { DecisionSummaryComponent } from './decision-summary.component';
import { ActionableSourceReview, PublicationDetail } from './publications';

describe('Decision summary', () => {
  const publication: PublicationDetail = { id: 'a', externalId: 'BOE-A-2026-1', title: 'Ayuda de prueba',
    publicationDate: '2026-10-01', sectionCode: '3', sectionName: 'Otras disposiciones',
    department: 'Ministerio', departmentCode: '1', issueNumber: '1' };

  it('keeps absent facts unknown and exposes the official source immediately', () => {
    const fixture = TestBed.createComponent(DecisionSummaryComponent);
    fixture.componentRef.setInput('publication', publication);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Destinatarios pendientes de comprobar');
    expect(text).toContain('Requisitos pendientes de comprobar');
    expect(text).toContain('No consta un plazo confirmado');
    expect(text).toContain('2026-10-01');
    expect(text).toContain('Información');
    expect(fixture.nativeElement.querySelector('a').href).toContain('www.boe.es/diario_boe/txt.php');
  });

  it('separates publication, effects and deadline without claiming eligibility', () => {
    const fixture = TestBed.createComponent(DecisionSummaryComponent);
    fixture.componentRef.setInput('publication', publication);
    fixture.componentRef.setInput('review', { kind: 'grant', kindLabel: 'Ayudas', sourceHash: 'hash', reviewedAt: '',
      nextStep: 'Comprueba las bases completas.', groups: [
        { key: 'recipients', label: 'Destinatarios', quotes: ['No podrán ser beneficiarias las entidades excluidas.'] },
        { key: 'effective', label: 'Entrada en vigor', quotes: ['La orden producirá efectos el 15 de octubre de 2026.'] },
        { key: 'deadlines', label: 'Plazos', quotes: ['Diez días hábiles a partir de la publicación.'] },
      ] } satisfies ActionableSourceReview);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Oportunidad');
    expect(text).toContain('No podrán ser beneficiarias');
    expect(text).toContain('La orden producirá efectos el 15 de octubre de 2026.');
    expect(text).toContain('Diez días hábiles a partir de la publicación.');
    expect(text).toContain('Qué no sabemos todavía');
    expect(text).not.toContain('Solicitud abierta');
    expect(fixture.componentInstance.facts()[1].quote).toBeUndefined();
  });

  it('labels regulatory items conservatively as possible obligations', () => {
    const fixture = TestBed.createComponent(DecisionSummaryComponent);
    fixture.componentRef.setInput('publication', publication);
    fixture.componentRef.setInput('review', { kind: 'regulation', kindLabel: 'Cambio normativo', sourceHash: 'hash', reviewedAt: '',
      nextStep: 'Revisa el ámbito.', groups: [] } satisfies ActionableSourceReview);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Posible obligación o cambio');
    expect(fixture.nativeElement.textContent).toContain('Revisa si el ámbito y las condiciones alcanzan a tu negocio');
  });

  it('labels truncated previews and keeps the complete passage alongside them', () => {
    const fixture = TestBed.createComponent(DecisionSummaryComponent);
    const quote = 'No podrán ser beneficiarias las empresas que incumplan estas condiciones. ' + 'Condición adicional. '.repeat(25);
    fixture.componentRef.setInput('publication', publication);
    fixture.componentRef.setInput('review', { groups: [{ key: 'recipients', quotes: [quote] }] });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Fragmento oficial abreviado');
    const quotes = fixture.nativeElement.querySelectorAll('blockquote');
    expect(quotes[0].textContent).toContain('No podrán ser beneficiarias');
    expect(quotes[0].textContent.endsWith('…')).toBe(true);
    expect(quotes[1].textContent).toBe(quote);
  });
});
