import { Component, input } from '@angular/core';
import { SourceProfileDimension } from './publications';

@Component({
  selector: 'app-profile-dimensions',
  template: `
    @for (dimension of dimensions(); track dimension.key) {
      <div class="dimension" [class.restriction]="dimension.status === 'restriction'">
        <h4>{{ dimension.label }} · {{ statusLabel(dimension) }}</h4>
        <p>{{ dimension.message }}</p>
        @if (dimension.quotes.length) {
          <details>
            <summary>{{ dimension.origin === 'metadata' ? 'Ver pista del título o epígrafe' : 'Ver citas del texto oficial' }}</summary>
            @for (quote of dimension.quotes; track quote) { <blockquote>{{ quote }}</blockquote> }
          </details>
        }
      </div>
    }
  `,
  styles: `
    .dimension { padding:12px 0; border-bottom:1px solid var(--line); }
    .restriction { border-left:3px solid #96511c; padding-left:12px; }
    h4 { margin:0 0 8px; font-size:.9rem; } p, blockquote, summary { font-size:.85rem; line-height:1.6; overflow-wrap:anywhere; }
    summary { cursor:pointer; color:var(--accent-dark); }
    blockquote { border-left:2px solid var(--line); margin:12px 0; padding-left:12px; white-space:pre-line; }
  `,
})
export class ProfileDimensionsComponent {
  readonly dimensions = input.required<SourceProfileDimension[]>();
  statusLabel(dimension: SourceProfileDimension): string {
    if (dimension.status === 'restriction') return 'Posible restricción · revisar';
    if (dimension.status === 'notSpecified') return 'Dato del perfil sin concretar';
    if (dimension.status === 'unknown') return 'Por comprobar';
    return dimension.origin === 'metadata' ? 'Pista en título o epígrafe' : 'Mención en la fuente';
  }
}
