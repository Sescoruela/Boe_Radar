import { Component, computed, input } from '@angular/core';
import { ActionableSourceReview, PublicationDetail } from './publications';
import { briefTitle, officialSource } from './publication-presentation';

@Component({
  selector: 'app-decision-summary',
  template: `
    <section aria-labelledby="decision-title">
      <h3 id="decision-title">Qué significa para tu negocio</h3>

      <div class="impact" [attr.data-impact]="impact().key">
        <span class="impact-label">{{ impact().label }}</span>
        <span class="impact-help">{{ impact().help }}</span>
      </div>

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

      <h4>Fechas que no deben confundirse</h4>
      <dl class="dates">
        <div>
          <dt>Publicación</dt>
          <dd>{{ publication().publicationDate }}</dd>
        </div>
        <div>
          <dt>Entrada en vigor / efectos</dt>
          <dd>{{ effectiveDateText() }}</dd>
        </div>
        <div>
          <dt>Plazo / fecha límite</dt>
          <dd>{{ deadlineText() }}</dd>
        </div>
      </dl>

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

      <h4>Qué conviene revisar ahora</h4>
      <p>{{ review()?.nextStep ?? 'Abre la fuente oficial y comprueba a quién afecta, las condiciones y las fechas antes de actuar.' }}</p>

      <h4>Qué no sabemos todavía</h4>
      <p class="pending">Esta ficha no confirma por sí sola que la publicación te afecte, que cumplas los requisitos ni que exista un plazo abierto. La fecha de publicación tampoco equivale a la entrada en vigor.</p>
    </section>
  `,
  styles: `
    section { background: white; border-left: 3px solid var(--accent); padding: 22px; margin: 24px 0; }
    h3 { font-family: var(--font-display); font-size: 1.6rem; margin: 0 0 16px; }
    h4 { font-size: .9rem; margin: 20px 0 8px; }
    p, blockquote, summary, dt, dd { font-size: .87rem; line-height: 1.6; overflow-wrap: anywhere; }
    p { margin: 8px 0; } .pending { color: var(--muted); }
    summary { cursor: pointer; color: var(--accent-dark); }
    blockquote { border-left: 2px solid var(--line); padding-left: 12px; margin: 12px 0; }
    a { display: inline-block; background: var(--ink); color: white; padding: 12px 16px; margin: 10px 0; }
    .impact { border: 1px solid var(--line); padding: 12px 14px; margin: 0 0 12px; display: grid; gap: 3px; }
    .impact-label { font-weight: 700; }
    .impact-help { color: var(--muted); font-size: .82rem; }
    .dates { display: grid; gap: 8px; margin: 8px 0 0; }
    .dates div { border-bottom: 1px solid var(--line); padding: 7px 0; }
    dt { font-weight: 700; }
    dd { margin: 2px 0 0; color: var(--muted); }
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

  readonly impact = computed(() => {
    const kind = this.review()?.kind;
    if (kind === 'grant') {
      return { key: 'opportunity', label: 'Oportunidad', help: 'Ayuda o convocatoria que puede merecer una revisión.' };
    }
    if (kind === 'tax' || kind === 'regulation') {
      return { key: 'obligation', label: 'Posible obligación o cambio', help: 'Revisa si el ámbito y las condiciones alcanzan a tu negocio.' };
    }
    return { key: 'information', label: 'Información', help: 'Novedad que conviene entender antes de decidir si requiere acción.' };
  });

  readonly effectiveDateText = computed(() =>
    this.firstQuote(['effective']) ?? 'No confirmada en esta ficha. Revisa la fuente oficial.');

  readonly deadlineText = computed(() =>
    this.firstQuote(['deadlines']) ?? 'No consta un plazo confirmado en esta ficha.');

  readonly facts = computed(() => {
    const groups = this.review()?.groups ?? [];
    return [
      { label: 'A quién podría afectar', keys: ['recipients', 'affected'], fallback: 'Destinatarios pendientes de comprobar.' },
      { label: 'Condiciones y requisitos', keys: ['requirements', 'obligations'], fallback: 'Requisitos pendientes de comprobar.' },
    ].map(fact => ({ ...fact, quote: groups.find(group => fact.keys.includes(group.key) && group.quotes.length)?.quotes[0] }));
  });

  private firstQuote(keys: string[]): string | undefined {
    const group = (this.review()?.groups ?? []).find(item => keys.includes(item.key) && item.quotes.length);
    return group?.quotes[0];
  }
}
