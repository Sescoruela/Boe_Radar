import { TestBed } from '@angular/core/testing';
import { ProfileMatchComponent } from './profile-match.component';

describe('Compact profile reasons', () => {
  it('highlights restrictive context on a card and keeps source quotes in the detail', () => {
    const fixture = TestBed.createComponent(ProfileMatchComponent);
    fixture.componentRef.setInput('match', { priority: 0, label: 'Posibles restricciones por revisar',
      reasons: ['El título menciona pymes'], checks: ['Comprueba condiciones'], dimensions: [
        { key: 'businessType', label: 'Tipo de negocio', status: 'restriction', origin: 'source', message: 'Revisa el contexto', quotes: ['Excepto pymes con deudas.'] },
        { key: 'territory', label: 'Territorio', status: 'unknown', message: 'Por comprobar', quotes: [] },
      ] });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Posible restricción en tipo de negocio');
    expect(fixture.nativeElement.textContent).toContain('1 dato por comprobar');
    expect(fixture.nativeElement.textContent).not.toContain('Excepto pymes con deudas.');
    fixture.componentRef.setInput('detail', true);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Excepto pymes con deudas.');
  });
  it('shows one reason on the card and preserves all reasons in the detail', () => {
    const fixture = TestBed.createComponent(ProfileMatchComponent);
    fixture.componentRef.setInput('match', { priority: 1, label: 'Menciones en la fuente',
      reasons: ['Primer motivo', 'Segundo motivo'], checks: ['Comprobar exclusiones'] });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Primer motivo');
    expect(fixture.nativeElement.textContent).not.toContain('Segundo motivo');
    fixture.componentRef.setInput('detail', true);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Segundo motivo');
    expect(fixture.nativeElement.textContent).toContain('Comprobar exclusiones');
  });
});
