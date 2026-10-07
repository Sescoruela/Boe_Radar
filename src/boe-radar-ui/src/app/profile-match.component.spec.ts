import { TestBed } from '@angular/core/testing';
import { ProfileMatchComponent } from './profile-match.component';

describe('Compact profile reasons', () => {
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
