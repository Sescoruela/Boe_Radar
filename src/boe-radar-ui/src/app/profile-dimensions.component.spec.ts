import { TestBed } from '@angular/core/testing';
import { ProfileDimensionsComponent } from './profile-dimensions.component';

describe('Profile dimensions', () => {
  it('distinguishes metadata, source, restriction, missing profile data and unknown scope', () => {
    const fixture = TestBed.createComponent(ProfileDimensionsComponent);
    fixture.componentRef.setInput('dimensions', [
      { key: 'activity', label: 'Actividad', status: 'mention', origin: 'metadata', message: 'Pista, no confirmación', quotes: ['Título con innovación'] },
      { key: 'businessType', label: 'Tipo de negocio', status: 'restriction', origin: 'source', message: 'Revisa la excepción', quotes: ['No podrán recibir ayudas las pymes con deudas.'] },
      { key: 'territory', label: 'Territorio', status: 'notSpecified', message: 'Concreta tu perfil', quotes: [] },
    ]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Pista en título o epígrafe');
    expect(fixture.nativeElement.textContent).toContain('Posible restricción · revisar');
    expect(fixture.nativeElement.textContent).toContain('Dato del perfil sin concretar');
    expect(fixture.nativeElement.textContent).toContain('No podrán recibir ayudas');
    expect(fixture.nativeElement.querySelectorAll('details')).toHaveLength(2);
    expect(fixture.nativeElement.querySelector('details').open).toBe(false);
  });
  it('never treats unknown or unspecified values as exclusions', () => {
    const fixture = TestBed.createComponent(ProfileDimensionsComponent);
    fixture.componentRef.setInput('dimensions', [{ key: 'territory', label: 'Territorio', status: 'unknown', message: 'No significa que esté excluido', quotes: [] }]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Por comprobar');
    expect(fixture.nativeElement.textContent).not.toContain('Posible restricción');
    expect(fixture.nativeElement.querySelector('details')).toBeNull();
  });
});
