import { DatePipe } from '@angular/common';
import { Component, computed, inject, output, signal } from '@angular/core';
import { FollowUpStore, FollowUpStatus, followUpStates } from './follow-up';
import { FollowUpControlsComponent } from './follow-up-controls.component';
import { PublicationListItem } from './publications';
import { briefTitle, officialSource } from './publication-presentation';

@Component({
  selector: 'app-follow-up-panel',
  imports: [DatePipe, FollowUpControlsComponent],
  template: `
    <section id="seguimiento" aria-labelledby="follow-up-title">
      <h2 id="follow-up-title">Mi seguimiento</h2>
      <p>{{ store.activeCount() }} publicaciones en seguimiento · {{ discardedCount() }} descartadas</p>
      <p class="note">Solo se guarda en este navegador, sin cuenta ni sincronización entre dispositivos.
        Borrar los datos del sitio elimina el seguimiento. Guardar una publicación no activa alertas ni confirma que puedas solicitarla.</p>
      <p class="note">Los estados son tuyos: no indican una solicitud presentada, una concesión ni el cumplimiento de una obligación.</p>
      <div class="filters" role="group" aria-label="Filtrar mi seguimiento">
        <button type="button" [attr.aria-pressed]="filter() === 'active'" (click)="filter.set('active')">En seguimiento</button>
        @for (state of states; track state.value) {
          <button type="button" [attr.aria-pressed]="filter() === state.value" (click)="filter.set(state.value)">{{ state.label }}</button>
        }
      </div>
      @if (visibleEntries().length) {
        <div class="grid">
          @for (entry of visibleEntries(); track entry.publication.externalId) {
            <article>
              <p>{{ stateLabel(entry.status) }} · Publicado {{ (entry.publication.publicationDate + 'T00:00:00Z') | date:'d MMM yyyy':'UTC' }}</p>
              <h3>{{ briefTitle(entry.publication.title) }}</h3>
              <p>{{ entry.publication.department }} · {{ entry.publication.externalId }}</p>
              <p class="note">Guardado {{ entry.savedAt | date:'d MMM yyyy, HH:mm' }}. La ficha se consulta de nuevo al abrirla.</p>
              <div class="actions">
                <button type="button" (click)="openPublication.emit(entry.publication)">Ver ficha →</button>
                <a [href]="officialSource(entry.publication)" target="_blank" rel="noopener noreferrer">Fuente oficial ↗</a>
              </div>
              <app-follow-up-controls [publication]="entry.publication" />
            </article>
          }
        </div>
      } @else {
        <p role="status">{{ store.entries().length ? 'No hay publicaciones en este estado. Prueba otro filtro.' : 'Todavía no has guardado publicaciones. Usa «Guardar publicación» en una tarjeta o ficha para empezar.' }}</p>
      }
    </section>
  `,
  styles: `
    section { border-top:1px solid var(--line); padding:36px 0; scroll-margin-top:24px; }
    h2 { font-family:var(--font-display); font-weight:500; } p { line-height:1.6; overflow-wrap:anywhere; }
    .note { color:var(--muted); font-size:.85rem; max-width:780px; }
    .filters, .actions { display:flex; flex-wrap:wrap; gap:8px; margin:18px 0; align-items:center; }
    button { background:var(--paper); border:1px solid var(--line); color:var(--ink);
      font:inherit; font-size:.85rem; padding:10px 14px; cursor:pointer; }
    button[aria-pressed=true] { background:var(--ink); color:var(--paper); }
    button:focus-visible, a:focus-visible { outline:2px solid var(--accent); outline-offset:3px; }
    a { color:var(--accent-dark); font-size:.85rem; } .grid { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:18px; }
    article { border:1px solid var(--line); padding:20px; min-width:0; } h3 { font-size:1.05rem; line-height:1.5; overflow-wrap:anywhere; }
    @media(max-width:700px) { .grid { grid-template-columns:1fr; } }
  `,
})
export class FollowUpPanelComponent {
  readonly store = inject(FollowUpStore);
  readonly states = followUpStates;
  readonly filter = signal<'active' | FollowUpStatus>('active');
  readonly discardedCount = computed(() => this.store.entries().filter(entry => entry.status === 'discarded').length);
  readonly visibleEntries = computed(() => this.store.entries().filter(entry =>
    this.filter() === 'active' ? entry.status !== 'discarded' : entry.status === this.filter()));
  readonly openPublication = output<PublicationListItem>();
  readonly briefTitle = briefTitle;
  readonly officialSource = officialSource;
  stateLabel(status: FollowUpStatus): string { return this.states.find(state => state.value === status)!.label; }
}
