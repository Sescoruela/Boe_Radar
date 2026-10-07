import { Component, computed, input } from '@angular/core';
import { ActionableSourceReview, PublicationDetail } from './publications';
import { briefTitle, officialSource } from './publication-presentation';

@Component({
  selector: 'app-decision-summary',
  template: `
    <section aria-labelledby="decision-title">
      <h3 id="decision-title">Antes de decidir</h3>
      @if (sourceUrl(); as url) {
        <a [href]="url" target="_blank" rel="noopener noreferrer">Consultar texto oficial ↗</a>
      }
      <h4>Qué se publica o cambia</h4>
      <p>{{ briefTitle(publication().title) }}</p>
      @if (publication().analysis; as analysis) {
        <h4>Posible impacto · interpretación automática</h4>
        <p>{{ analysis.summary }}</p>
      } @else {
        <p class="pending">El impacto para tu negocio todavía no está analizado.</p>
      }
      @for (fact of facts(); track fact.label) {
        <h4>{{ fact.label }}</h4>
        @if (fact.quote) {
          <p class="pending">{{ fact.quote.length > 260 ? 'Fragmento oficial abreviado' : 'Fragmento oficial' }} · comprueba el contexto.</p>
          <blockquote>{{ excerpt(fact.quote) }}</blockquote>
          @if (fact.quote.length > 260) {
            <details><summary>Leer fragmento completo</summary><blockquote>{{ fact.quote }}</blockquote></details>
          }
        } @else {
          <p class="pending">{{ loading() ? 'Consultando el texto oficial…' : fact.fallback }}</p>
        }
      }
      <h4>Siguiente paso</h4>
      <p>{{ review()?.nextStep ?? 'Abre la fuente oficial y comprueba a quién afecta, las condiciones y las fechas antes de actuar.' }}</p>
      <p class="pending">La publicación no confirma que cumplas los requisitos ni que exista un plazo abierto.
        La fecha de publicación no equivale a la entrada en vigor.</p>
    </section>
  `,
  styles: `
    section { background: white; border-left: 3px solid var(--accent); padding: 22px; margin: 24px 0; }
    h3 { font-family: var(--font-display); font-size: 1.6rem; margin: 0 0 20px; }
    h4 { font-size: .9rem; margin: 20px 0 8px; }
    p, blockquote, summary { font-size: .87rem; line-height: 1.6; overflow-wrap: anywhere; }
    p { margin: 8px 0; } .pending { color: var(--muted); }
    summary { cursor: pointer; color: var(--accent-dark); }
    blockquote { border-left: 2px solid var(--line); padding-left: 12px; margin: 12px 0; }
    a { display: inline-block; background: var(--ink); color: white; padding: 12px 16px; margin: 10px 0; }
  `,
})
export class DecisionSummaryComponent {
  readonly publication = input.required<PublicationDetail>();
  readonly review = input<ActionableSourceReview | null>(null);
  readonly loading = input(false);
  readonly briefTitle = briefTitle;
  excerpt(quote: string): string {
    if (quote.length <= 260) return quote;
    const fragment = quote.slice(0, 260);
    const boundary = fragment.lastIndexOf(' ');
    return `${boundary > 0 ? fragment.slice(0, boundary) : fragment}…`;
  }
  readonly sourceUrl = computed(() => officialSource(this.publication()));
  readonly facts = computed(() => {
    const groups = this.review()?.groups ?? [];
    return [
      { label: 'A quién podría afectar', keys: ['recipients', 'affected'], fallback: 'Destinatarios pendientes de comprobar.' },
      { label: 'Condiciones y requisitos', keys: ['requirements', 'obligations'], fallback: 'Requisitos pendientes de comprobar.' },
      { label: 'Plazo · pendiente de confirmar', keys: ['deadlines'], fallback: 'No hay un plazo confirmado. Consulta las fechas y sus condiciones en la fuente.' },
    ].map(fact => ({ ...fact, quote: groups.find(group => fact.keys.includes(group.key) && group.quotes.length)?.quotes[0] }));
  });
}
