import { Component, input } from '@angular/core';
import { SourceProfileContrast } from './publications';

@Component({
  selector: 'app-source-profile',
  template: `
    <section aria-labelledby="source-profile-title">
      <h3 id="source-profile-title">Tu perfil en el texto oficial</h3>
      <p>Contraste de los fragmentos localizados, no de todos los requisitos. No confirma elegibilidad ni obligaciones. Vuelve a explorar el radar para incorporar esta revisión al orden; la página actual no se reordena al abrir una ficha.</p>
      @for (dimension of contrast().dimensions; track dimension.key) {
        <h4>{{ dimension.label }} · {{ dimension.status === 'mention' ? 'Mención en la fuente' : 'Por comprobar' }}</h4>
        <p>{{ dimension.message }}</p>
        @for (quote of dimension.quotes; track quote) { <blockquote>“{{ quote }}”</blockquote> }
      }
      <p>Antes de actuar, revisa también las exclusiones, la vigencia y las condiciones del documento completo.</p>
    </section>
  `,
  styles: `
    :host { display: block; }
    section { border: 1px solid var(--line); padding: 18px; margin: 24px 0; }
    p, blockquote { font-size: .85rem; line-height: 1.6; }
    h4 { margin-bottom: 8px; }
    blockquote { border-left: 3px solid var(--accent); margin: 12px 0; padding-left: 12px; }
  `,
})
export class SourceProfileComponent {
  readonly contrast = input.required<SourceProfileContrast>();
}
