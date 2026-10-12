import { Component, computed, input } from '@angular/core';
import { BusinessProfileMatch } from './publications';
import { ProfileDimensionsComponent } from './profile-dimensions.component';

@Component({
  selector: 'app-profile-match',
  imports: [ProfileDimensionsComponent],
  template: `
    @if (detail()) {
      <section class="detail" aria-labelledby="profile-match-title">
        <h3 id="profile-match-title">Por qué aparece en tu radar</h3>
        <p>Coincidir con una mención no confirma que cumplas los requisitos ni que tengas una obligación.</p>
        @for (reason of match().reasons; track reason) { <p>{{ reason }}</p> }
        @if (match().dimensions?.length) {
          <app-profile-dimensions [dimensions]="match().dimensions!" />
        }
        <h4>Antes de actuar</h4>
        <ul>@for (check of match().checks; track check) { <li>{{ check }}</li> }</ul>
        <p>{{ match().sourceHash ? 'La prioridad combina metadatos y menciones del texto revisado.' : 'La prioridad usa el título y el epígrafe; todavía no incorpora una revisión guardada.' }} Revisa los requisitos, los plazos y el texto oficial antes de decidir.</p>
      </section>
    } @else {
      <div class="match" [class.has-match]="match().priority > 0">
        <strong>{{ match().label }}</strong>
        <p>{{ restriction() ? 'Hay una posible restricción; no se ha confirmado si te aplica o te excluye.' : match().priority > 0 ? 'Aplicabilidad pendiente de comprobar.' : 'Sin coincidencias claras; conservamos la señal para que puedas revisarla.' }}</p>
        @if (restriction(); as restriction) {
          <p>Posible restricción en {{ restriction.label.toLocaleLowerCase('es') }}. Revisa la cita y su contexto en la ficha.</p>
        } @else if (match().reasons[0]; as reason) { <p>{{ reason }}</p> }
        @if (pendingCount()) { <p>{{ pendingCount() }} {{ pendingCount() === 1 ? 'dato por comprobar o concretar' : 'datos por comprobar o concretar' }}. Consulta el detalle por actividad, tipo de negocio y territorio.</p> }
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
  readonly restriction = computed(() => this.match().dimensions?.find(dimension => dimension.status === 'restriction'));
  readonly pendingCount = computed(() => this.match().dimensions?.filter(dimension => dimension.status === 'unknown' || dimension.status === 'notSpecified').length ?? 0);
}
