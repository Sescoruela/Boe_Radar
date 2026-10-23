import { TestBed } from '@angular/core/testing';
import { FollowUpControlsComponent } from './follow-up-controls.component';

describe('Publication sharing', () => {
  it('shares only the publication identifier, excluding management tokens and query data', () => {
    const previous = window.location.href;
    window.history.replaceState(null, '', '?profile=private#manage=secret');
    const fixture = TestBed.createComponent(FollowUpControlsComponent);
    fixture.componentRef.setInput('publication', { id: '11111111-1111-4111-8111-111111111111' });
    expect(fixture.componentInstance.shareUrl()).toBe(window.location.origin + window.location.pathname
      + '#publication=11111111-1111-4111-8111-111111111111');
    window.history.replaceState(null, '', previous);
    fixture.destroy();
  });

  it('provides a usable link when clipboard access is denied', async () => {
    const fixture = TestBed.createComponent(FollowUpControlsComponent);
    fixture.componentRef.setInput('publication', { id: '11111111-1111-4111-8111-111111111111' });
    const descriptor = Object.getOwnPropertyDescriptor(navigator, 'clipboard');
    Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: () => Promise.reject(new Error('denied')) } });
    await fixture.componentInstance.copyLink();
    expect(fixture.componentInstance.shareMessage()).toContain('Puedes copiar');
    if (descriptor) Object.defineProperty(navigator, 'clipboard', descriptor);
    else Reflect.deleteProperty(navigator, 'clipboard');
    fixture.destroy();
  });
});
