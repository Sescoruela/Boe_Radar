import { DatePipe } from '@angular/common';
import { Component, HostListener, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  PublicationFilters,
  PublicationDetail,
  PublicationSearchResult,
  CatalogStatus,
  PublicationsApi,
} from './publications';
import { SubscriptionsApi, SubscriptionView } from './subscriptions';

@Component({
  selector: 'app-root',
  imports: [DatePipe, FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  private readonly api = inject(PublicationsApi);
  private readonly subscriptions = inject(SubscriptionsApi);

  readonly result = signal<PublicationSearchResult | null>(null);
  readonly catalogStatus = signal<CatalogStatus | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly selected = signal<PublicationDetail | null>(null);
  readonly detailLoading = signal(false);
  readonly currentYear = new Date().getFullYear();
  readonly subscriptionMessage = signal<string | null>(null);
  readonly subscriptionBusy = signal(false);
  readonly managedSubscription = signal<SubscriptionView | null>(null);
  readonly categoryOptions = [
    { value: 'Grant', label: 'Ayudas' },
    { value: 'Subsidy', label: 'Subvenciones' },
    { value: 'Tax', label: 'Fiscalidad' },
    { value: 'Obligation', label: 'Obligaciones' },
    { value: 'Employment', label: 'Laboral' },
    { value: 'Financing', label: 'Financiación' },
  ];
  subscriptionEmail = '';
  subscriptionKeywords = '';
  subscriptionCategories: string[] = [];
  digestHour = 8;
  consent = false;
  private managementToken: string | null = null;

  filters: PublicationFilters = {
    query: '',
    section: '',
    dateFrom: '',
    dateTo: '',
    page: 1,
    pageSize: 12,
    businessSignalsOnly: true,
  };

  ngOnInit(): void {
    this.search();
    this.api.getStatus().subscribe({
      next: (status) => this.catalogStatus.set(status),
      error: () => this.catalogStatus.set(null),
    });
    this.handleSubscriptionLink();
  }

  selectView(businessSignalsOnly: boolean): void {
    if (this.filters.businessSignalsOnly === businessSignalsOnly) return;
    this.filters.businessSignalsOnly = businessSignalsOnly;
    this.search();
  }

  @HostListener('window:hashchange')
  handleSubscriptionLink(): void {
    const fragment = new URLSearchParams(window.location.hash.slice(1));
    const verify = fragment.get('verify');
    const manage = fragment.get('manage');
    const unsubscribe = fragment.get('unsubscribe');
    if (verify || manage || unsubscribe) {
      window.history.replaceState(null, '', window.location.pathname + window.location.search);
    }
    if (verify) {
      this.subscriptionBusy.set(true);
      this.subscriptions.verify(verify).pipe(finalize(() => this.subscriptionBusy.set(false)))
        .subscribe({
          next: ({ managementToken }) => {
            this.subscriptionMessage.set('Suscripción confirmada. Guarda el enlace de gestión que recibirás por correo.');
            this.loadManagement(managementToken);
          },
          error: () => this.subscriptionMessage.set('El enlace de verificación no es válido o ha caducado.'),
        });
    } else if (manage) {
      this.loadManagement(manage);
    } else if (unsubscribe) {
      this.subscriptions.unsubscribe(unsubscribe).subscribe({
        next: () => {
          this.managedSubscription.set(null);
          this.managementToken = null;
          this.subscriptionMessage.set('Has dado de baja las alertas.');
        },
        error: () => this.subscriptionMessage.set('El enlace de baja no es válido o ya se ha utilizado.'),
      });
    }
  }

  toggleCategory(category: string, enabled: boolean): void {
    this.subscriptionCategories = enabled
      ? [...this.subscriptionCategories, category]
      : this.subscriptionCategories.filter((item) => item !== category);
  }

  submitSubscription(): void {
    this.subscriptionBusy.set(true);
    this.subscriptionMessage.set(null);
    this.subscriptions.register(this.subscriptionEmail, this.preferences(), this.consent)
      .pipe(finalize(() => this.subscriptionBusy.set(false)))
      .subscribe({
        next: () => this.subscriptionMessage.set('Si procede, recibirás un correo para confirmar la suscripción.'),
        error: () => this.subscriptionMessage.set('No se pudo guardar la solicitud. Revisa el correo y las preferencias.'),
      });
  }

  savePreferences(): void {
    if (!this.managementToken) return;
    this.subscriptionBusy.set(true);
    this.subscriptions.update(this.managementToken, this.preferences())
      .pipe(finalize(() => this.subscriptionBusy.set(false)))
      .subscribe({
        next: () => this.subscriptionMessage.set('Preferencias guardadas.'),
        error: () => this.subscriptionMessage.set('No se pudieron guardar las preferencias.'),
      });
  }

  leaveSubscription(): void {
    if (!this.managementToken) return;
    this.subscriptionBusy.set(true);
    this.subscriptions.unsubscribe(this.managementToken)
      .pipe(finalize(() => this.subscriptionBusy.set(false)))
      .subscribe({
        next: () => {
          this.managedSubscription.set(null);
          this.managementToken = null;
          this.subscriptionMessage.set('Has dado de baja las alertas.');
        },
        error: () => this.subscriptionMessage.set('No se pudo completar la baja.'),
      });
  }

  private loadManagement(token: string): void {
    this.subscriptions.get(token).subscribe({
      next: (view) => {
        this.managementToken = token;
        this.managedSubscription.set(view);
        this.subscriptionEmail = view.email;
        this.subscriptionCategories = [...view.preferences.categories];
        this.subscriptionKeywords = view.preferences.keywords.join(', ');
        this.digestHour = view.preferences.digestHour;
      },
      error: () => this.subscriptionMessage.set('El enlace de gestión no es válido.'),
    });
  }

  private preferences() {
    return {
      categories: this.subscriptionCategories,
      keywords: this.subscriptionKeywords.split(',').map((item) => item.trim()).filter(Boolean),
      digestHour: this.digestHour,
    };
  }

  search(page = 1): void {
    this.filters.page = page;
    this.loading.set(true);
    this.error.set(null);

    this.api
      .search(this.filters)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (result) => this.result.set(result),
        error: () =>
          this.error.set(
            'No hemos podido consultar el radar. Comprueba que la API y PostgreSQL están disponibles.',
          ),
      });
  }

  clear(): void {
    this.filters = {
      query: '',
      section: '',
      dateFrom: '',
      dateTo: '',
      page: 1,
      pageSize: 12,
      businessSignalsOnly: this.filters.businessSignalsOnly,
    };
    this.search();
  }

  openDetails(id: string): void {
    this.detailLoading.set(true);
    this.api
      .get(id)
      .pipe(finalize(() => this.detailLoading.set(false)))
      .subscribe({
        next: (publication) => this.selected.set(publication),
        error: () =>
          this.error.set('No hemos podido abrir la ficha de esta publicación.'),
      });
  }

  closeDetails(): void {
    this.selected.set(null);
  }

  categoryLabel(category: string): string {
    return (
      {
        Grant: 'Ayuda',
        Subsidy: 'Subvención',
        Tax: 'Fiscalidad',
        Obligation: 'Obligación',
        Employment: 'Laboral',
        Financing: 'Financiación',
        Other: 'Otra',
      }[category] ?? category
    );
  }

  confidencePercent(confidence: number): number {
    return Math.round(confidence * 100);
  }
}
