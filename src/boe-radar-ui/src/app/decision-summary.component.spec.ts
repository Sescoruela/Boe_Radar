import { TestBed } from '@angular/core/testing';
import { DecisionSummaryComponent } from './decision-summary.component';
import { ActionableSourceReview, PublicationDetail } from './publications';

describe('Decision summary', () => {
  const publication: PublicationDetail = { id: 'a', externalId: 'BOE-A-2026-1', title: 'Ayuda de prueba',
    publicationDate: '2026-10-01', sectionCode: '3', sectionName: 'Otras disposiciones',
    department: 'Ministerio', departmentCode: '1', issueNumber: '1' };

  const analysis = {
    isRelevant: true, category: 'Tax', summary: 'Resumen fiscal', confidence: .9, method: 'gemini',
    requirements: [], evidence: [], deadlines: [
      { date: '2026-10-15', description: 'Efectos', isExplicit: true, kind: 'EffectiveDate' as const },
      { description: 'Diez días desde la publicación', isExplicit: false, kind: 'ApplicationDeadline' as const },
    ], modelName: 'fixture', promptVersion: 'radar-v3', analyzedAt: '2026-10-01T10:00:00Z', contentHash: 'hash-a',
    actionable: { topic: 'Tax' as const, impactKind: 'Opportunity' as const, whatChanged: 'Deducción fiscal publicada',
      affectedGroups: ['Pymes'], nextSteps: ['Comprueba la deducción con tu asesor'], uncertainties: ['Importe pendiente de verificar'] },
  };

  it('uses independent topic and impact, typed dates and specific next steps', () => {
    const fixture = TestBed.createComponent(DecisionSummaryComponent);
    fixture.componentRef.setInput('publication', { ...publication, analysis });
    fixture.componentRef.setInput('review', { kind: 'tax', groups: [], sourceHash: 'hash-a' });
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Oportunidad');
    expect(text).toContain('Fiscalidad');
    expect(text).toContain('Deducción fiscal publicada');
    expect(text).toContain('Comprueba la deducción con tu asesor');
    expect(text).toContain('Importe pendiente de verificar');
    expect(fixture.componentInstance.effectiveDateText()).toBe('2026-10-15 · Efectos');
    expect(fixture.componentInstance.deadlineText()).toBe('Diez días desde la publicación');
  });

  it('withholds actionable facts when current source differs from the analyzed source', () => {
    const fixture = TestBed.createComponent(DecisionSummaryComponent);
    fixture.componentRef.setInput('publication', { ...publication, analysis });
    fixture.componentRef.setInput('review', { kind: 'general', groups: [], sourceHash: 'hash-b' });
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('El texto oficial ha cambiado');
    expect(text).not.toContain('Deducción fiscal publicada');
    expect(text).not.toContain('Comprueba la deducción con tu asesor');
    expect(fixture.componentInstance.effectiveDateText()).not.toContain('2026-10-15');
  });

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
    expect(text).toContain('No podrán ser beneficiarias');
    expect(text).toContain('La orden producirá efectos el 15 de octubre de 2026.');
    expect(text).toContain('Diez días hábiles a partir de la publicación.');
    expect(text).toContain('Qué no sabemos todavía');
    expect(text).not.toContain('Solicitud abierta');
    expect(fixture.componentInstance.facts()[1].quote).toBeUndefined();
  });

  it('does not infer an obligation from a regulatory topic alone', () => {
    const fixture = TestBed.createComponent(DecisionSummaryComponent);
    fixture.componentRef.setInput('publication', publication);
    fixture.componentRef.setInput('review', { kind: 'regulation', kindLabel: 'Cambio normativo', sourceHash: 'hash', reviewedAt: '',
      nextStep: 'Revisa el ámbito.', groups: [] } satisfies ActionableSourceReview);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Información');
    expect(fixture.nativeElement.textContent).toContain('Impacto pendiente de clasificar');
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
