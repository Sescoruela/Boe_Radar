import { provideHttpClient } from '@angular/common/http';
import { registerLocaleData } from '@angular/common';
import localeEs from '@angular/common/locales/es';
import { LOCALE_ID, provideZonelessChangeDetection } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';
import { App } from './app/app';

registerLocaleData(localeEs);

bootstrapApplication(App, {
  providers: [
    provideHttpClient(),
    provideZonelessChangeDetection(),
    { provide: LOCALE_ID, useValue: 'es' },
  ],
}).catch((error: unknown) => console.error(error));
