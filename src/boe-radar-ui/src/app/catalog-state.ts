import { Injectable, OnDestroy, inject, signal } from '@angular/core';
import { Subscription, finalize } from 'rxjs';
import { BusinessProfile } from './business-profile';
import { ActionableSourceReview, BusinessProfileMatch, PublicationDetail,
  PublicationFilters, PublicationSearchResult, PublicationsApi } from './publications';

/** Scoped to the screen: cancelling a read must also clear its loading state. */
@Injectable()
export class CatalogState implements OnDestroy {
  private readonly api = inject(PublicationsApi);
  private searchRequest?: Subscription;
  private detailRequest?: Subscription;
  private reviewRequest?: Subscription;
  private searchRequestId = 0;
  private detailRequestId = 0;

  readonly result = signal<PublicationSearchResult | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly selected = signal<PublicationDetail | null>(null);
  readonly selectedProfileMatch = signal<BusinessProfileMatch | null>(null);
  readonly detailLoading = signal(false);
  readonly sourceReview = signal<ActionableSourceReview | null>(null);
  readonly sourceReviewLoading = signal(false);
  readonly sourceReviewError = signal(false);

  search(filters: PublicationFilters, profile: BusinessProfile | null,
    retainEvidenceSnapshot = false): void {
    const evidenceAsOf = retainEvidenceSnapshot ? this.result()?.evidenceAsOf ?? null : null;
    const requestId = ++this.searchRequestId;
    this.searchRequest?.unsubscribe();
    this.closeDetails();
    this.error.set(null);
    this.result.set(null);
    this.loading.set(false);

    if (filters.dateFrom && filters.dateTo && filters.dateFrom > filters.dateTo) {
      this.error.set('La fecha «Desde» no puede ser posterior a «Hasta».');
      return;
    }

    this.loading.set(true);
    const request = profile
      ? this.api.personalized({ ...filters }, { ...profile }, evidenceAsOf)
      : this.api.search({ ...filters });
    this.searchRequest = request.pipe(finalize(() => {
      if (requestId === this.searchRequestId) this.loading.set(false);
    })).subscribe({
      next: result => {
        if (requestId === this.searchRequestId) this.result.set(result);
      },
      error: () => {
        if (requestId === this.searchRequestId)
          this.error.set('No hemos podido consultar el catálogo. Comprueba tu conexión e inténtalo de nuevo.');
      },
    });
  }

  openDetails(id: string, profile: BusinessProfile | null): void {
    this.closeDetails();
    const requestId = this.detailRequestId;
    const reviewProfile = profile ? { ...profile } : null;
    this.error.set(null);
    this.detailLoading.set(true);
    this.selectedProfileMatch.set(profile
      ? this.result()?.items.find(item => item.id === id)?.profileMatch ?? null : null);
    this.detailRequest = this.api.get(id).pipe(finalize(() => {
      if (requestId === this.detailRequestId) this.detailLoading.set(false);
    })).subscribe({
      next: publication => {
        if (requestId !== this.detailRequestId) return;
        this.selected.set(publication);
        this.sourceReviewLoading.set(true);
        this.reviewRequest = this.api.getSourceReview(publication.externalId, reviewProfile)
          .pipe(finalize(() => {
            if (requestId === this.detailRequestId) this.sourceReviewLoading.set(false);
          })).subscribe({
            next: review => {
              if (requestId === this.detailRequestId) this.sourceReview.set(review);
            },
            error: () => {
              if (requestId === this.detailRequestId) this.sourceReviewError.set(true);
            },
          });
      },
      error: () => {
        if (requestId === this.detailRequestId) {
          this.selectedProfileMatch.set(null);
          this.error.set('No hemos podido abrir la ficha de esta publicación.');
        }
      },
    });
  }

  closeDetails(): void {
    ++this.detailRequestId;
    this.detailRequest?.unsubscribe();
    this.reviewRequest?.unsubscribe();
    this.selected.set(null);
    this.selectedProfileMatch.set(null);
    this.sourceReview.set(null);
    this.detailLoading.set(false);
    this.sourceReviewLoading.set(false);
    this.sourceReviewError.set(false);
  }

  ngOnDestroy(): void {
    ++this.searchRequestId;
    this.searchRequest?.unsubscribe();
    this.loading.set(false);
    this.closeDetails();
  }
}
