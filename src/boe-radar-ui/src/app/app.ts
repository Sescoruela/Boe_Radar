import { DatePipe } from '@angular/common';
import { Component, DestroyRef, HostListener, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  PublicationFilters,
  CatalogStatus,
  PublicationsApi,
} from './publications';
import { CatalogState } from './catalog-state';
import { SubscriptionsApi, SubscriptionView } from './subscriptions';
import { ProfileMatchComponent } from './profile-match.component';
import { SourceProfileComponent } from './source-profile.component';
import { BusinessProfile, businessTypes, activityOptions, territoryOptions,
  isBusinessProfile, loadBusinessProfile, storeBusinessProfile } from './business-profile';

@Component({
  selector: 'app-root',
  providers: [CatalogState],
  imports: [DatePipe, FormsModule, ProfileMatchComponent, SourceProfileComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  private readonly api = inject(PublicationsApi);
  private readonly subscriptions = inject(SubscriptionsApi);
  private readonly catalog = inject(CatalogState);
  private readonly destroyRef = inject(DestroyRef);

  readonly businessTypes = businessTypes;
  readonly activityOptions = activityOptions;
  readonly territoryOptions = territoryOptions;
  readonly businessProfile = signal<BusinessProfile | null>(null);
  readonly personalizedView = signal(false);
  readonly profileMessage = signal<string | null>(null);
  readonly selectedProfileMatch = this.catalog.selectedProfileMatch;
  profileDraft: BusinessProfile = { businessType: 'autonomous', activity: 'other', territory: 'all' };

  readonly result = this.catalog.result;
  readonly catalogStatus = signal<CatalogStatus | null>(null);
  readonly catalogStatusError = signal(false);
  readonly loading = this.catalog.loading;
  readonly error = this.catalog.error;
  readonly selected = this.catalog.selected;
  readonly detailLoading = this.catalog.detailLoading;
  readonly sourceReview = this.catalog.sourceReview;
  readonly sourceReviewLoading = this.catalog.sourceReviewLoading;
  readonly sourceReviewError = this.catalog.sourceReviewError;
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
    const profile = loadBusinessProfile();
    if (profile) {
      this.businessProfile.set(profile);
      this.profileDraft = { ...profile };
      this.personalizedView.set(true);
    }
    this.search();
    this.api.getStatus().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (status) => this.catalogStatus.set(status),
      error: () => this.catalogStatusError.set(true),
    });
    this.handleSubscriptionLink();
  }

  selectView(businessSignalsOnly: boolean): void {
    if (this.filters.businessSignalsOnly === businessSignalsOnly && !this.personalizedView()) return;
    this.personalizedView.set(false);
    this.filters.businessSignalsOnly = businessSignalsOnly;
    this.search();
  }

  applyBusinessProfile(): void {
    if (!isBusinessProfile(this.profileDraft)) {
      this.profileMessage.set('Selecciona un tipo de negocio, actividad y territorio válidos.');
      return;
    }
    const profile = { ...this.profileDraft };
    this.businessProfile.set(profile);
    const saved = storeBusinessProfile(profile);
    this.profileMessage.set(saved ? 'Perfil guardado en este navegador. Puedes cambiarlo o borrarlo cuando quieras.'
      : 'Perfil aplicado durante esta visita. El navegador no ha permitido guardarlo.');
    this.selectPersonalizedView();
  }

  selectPersonalizedView(): void {
    if (!this.businessProfile()) return;
    this.personalizedView.set(true);
    this.filters.businessSignalsOnly = true;
    this.search();
  }

  removeBusinessProfile(): void {
    const removed = storeBusinessProfile(null);
    this.businessProfile.set(null);
    this.profileDraft = { businessType: 'autonomous', activity: 'other', territory: 'all' };
    this.personalizedView.set(false);
    this.selectedProfileMatch.set(null);
    this.profileMessage.set(removed ? 'Perfil borrado. Vuelves a las señales generales.'
      : 'Perfil desactivado en esta visita. Borra los datos del sitio en el navegador para eliminar la copia guardada.');
    this.filters.businessSignalsOnly = true;
    this.search();
  }

  businessProfileLabel(): string {
    const profile = this.businessProfile();
    if (!profile) return '';
    return [businessTypes.find(option => option.value === profile.businessType)?.label,
      activityOptions.find(option => option.value === profile.activity)?.label,
      territoryOptions.find(option => option.value === profile.territory)?.label].join(' · ');
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

  search(page = 1, retainEvidenceSnapshot = false): void {
    this.filters.page = page;
    this.catalog.search(this.filters, this.personalizedView() ? this.businessProfile() : null,
      retainEvidenceSnapshot);
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
    this.catalog.openDetails(id, this.personalizedView() ? this.businessProfile() : null);
  }

  closeDetails(): void {
    this.catalog.closeDetails();
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

}
