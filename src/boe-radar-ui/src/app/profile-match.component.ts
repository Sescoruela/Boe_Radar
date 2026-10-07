import { Component, input } from '@angular/core';
import { BusinessProfileMatch } from './publications';

@Component({
  selector: 'app-profile-match',
  template: `
    @if (detail()) {
      <section class="detail" aria-labelledby="profile-match-title">
        <h3 id="profile-match-title">Por qué aparece en tu radar</h3>
        <p>Coincidir con una mención no confirma que cumplas los requisitos ni que tengas una obligación.</p>
        @for (reason of match().reasons; track reason) { <p>{{ reason }}</p> }
        <h4>Antes de actuar</h4>
        <ul>@for (check of match().checks; track check) { <li>{{ check }}</li> }</ul>
        <p>{{ match().sourceHash ? 'La prioridad combina metadatos y menciones del texto revisado.' : 'La prioridad usa el título y el epígrafe; todavía no incorpora una revisión guardada.' }} Revisa los requisitos, los plazos y el texto oficial antes de decidir.</p>
      </section>
    } @else {
      <div class="match" [class.has-match]="match().priority > 0">
        <strong>{{ match().label }}</strong>
        <p>{{ match().priority > 0 ? 'Aplicabilidad pendiente de comprobar.' : 'Sin coincidencias claras; conservamos la señal para que puedas revisarla.' }}</p>
        @if (match().reasons[0]; as reason) { <p>{{ reason }}</p> }
        @if (match().reasons.length > 1) { <p>Más motivos en la ficha.</p> }
      </div>
    }
  `,
  styles: `
    :host { display: block; }
    .match {
      background: var(--paper);
      border-left: 3px solid var(--line);
      font-size: 0.78rem;
      line-height: 1.5;
      margin: 16px 0;
      padding: 12px;
    }
    .match p { margin: 6px 0 0; }
    .match.has-match { border-color: var(--accent); background: var(--accent-wash); }
    .detail { background: var(--accent-wash); padding: 18px; margin-bottom: 24px; font-size: 0.9rem; line-height: 1.6; }
  `,
})
export class ProfileMatchComponent {
  readonly match = input.required<BusinessProfileMatch>();
  readonly detail = input(false);
}
