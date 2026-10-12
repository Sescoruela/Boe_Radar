import { Component, inject, input } from '@angular/core';
import { PublicationListItem } from './publications';
import { FollowUpStore, FollowUpStatus, followUpStates } from './follow-up';

@Component({
  selector: 'app-follow-up-controls',
  template: `
    <div class="controls" role="group" [attr.aria-label]="'Seguimiento de ' + publication().externalId">
      @if (store.find(publication().externalId); as entry) {
        @if (entry.status === 'discarded') {
          <span>Descartado</span>
          <button type="button" (click)="store.restore(publication())">Recuperar publicación</button>
        } @else {
          <label>Estado
            <select [value]="entry.status" (change)="change($any($event.target).value)">
              @for (state of states; track state.value) {
                @if (state.value !== 'discarded') { <option [value]="state.value">{{ state.label }}</option> }
              }
            </select>
          </label>
          <button type="button" (click)="change('discarded')">Descartar</button>
        }
      } @else {
        <button type="button" (click)="change('review')">Guardar publicación</button>
        <button type="button" (click)="change('discarded')">Descartar</button>
      }
    </div>
  `,
  styles: `
    .controls { display:flex; flex-wrap:wrap; gap:10px; align-items:center; margin-top:16px; }
    button, select { background:var(--paper); color:var(--ink); border:1px solid var(--line);
      border-radius:3px; padding:9px 12px; font:inherit; font-size:.82rem; max-width:100%; }
    button { cursor:pointer; } label { display:flex; flex-wrap:wrap; align-items:center; gap:8px; font-size:.82rem; }
    button:focus-visible, select:focus-visible { outline:2px solid var(--accent); outline-offset:3px; }
    span { color:var(--muted); font-size:.82rem; }
  `,
})
export class FollowUpControlsComponent {
  readonly publication = input.required<PublicationListItem>();
  readonly store = inject(FollowUpStore);
  readonly states = followUpStates;
  change(status: FollowUpStatus): void { this.store.setStatus(this.publication(), status); }
}
