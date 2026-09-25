import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface PublicationListItem {
  id: string;
  externalId: string;
  publicationDate: string;
  title: string;
  sectionCode: string;
  sectionName: string;
  department: string;
  epigraph?: string;
  officialPdfUrl?: string;
  analysis?: RadarAnalysisSummary;
}

export interface RadarAnalysisSummary {
  isRelevant: boolean;
  category: string;
  summary: string;
  confidence: number;
  method: string;
}

export interface RadarDeadline {
  date?: string;
  description: string;
  isExplicit: boolean;
}

export interface RadarEvidence {
  quote: string;
  supports: string;
}

export interface RadarAnalysisDetail extends RadarAnalysisSummary {
  requirements: string[];
  deadlines: RadarDeadline[];
  evidence: RadarEvidence[];
  modelName: string;
  promptVersion: string;
  analyzedAt: string;
}

export interface PublicationSearchResult {
  items: PublicationListItem[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface CatalogStatus {
  latestPublicationDate: string | null;
  totalPublications: number;
  emailAlertsEnabled: boolean;
}

export interface PublicationDetail extends PublicationListItem {
  issueNumber: string;
  departmentCode: string;
  controlNumber?: string;
  officialHtmlUrl?: string;
  officialXmlUrl?: string;
  analysis?: RadarAnalysisDetail;
}

export interface PublicationFilters {
  query: string;
  section: string;
  dateFrom: string;
  dateTo: string;
  page: number;
  pageSize: number;
  businessSignalsOnly: boolean;
}

@Injectable({ providedIn: 'root' })
export class PublicationsApi {
  private readonly http = inject(HttpClient);

  getStatus(): Observable<CatalogStatus> {
    return this.http.get<CatalogStatus>('/api/v1/catalog/status');
  }

  search(filters: PublicationFilters): Observable<PublicationSearchResult> {
    let params = new HttpParams()
      .set('page', filters.page)
      .set('pageSize', filters.pageSize);

    if (filters.businessSignalsOnly) {
      params = params.set('businessSignalsOnly', true);
    }

    for (const [key, value] of Object.entries({
      query: filters.query,
      section: filters.section,
      dateFrom: filters.dateFrom,
      dateTo: filters.dateTo,
    })) {
      if (value) {
        params = params.set(key, value);
      }
    }

    return this.http.get<PublicationSearchResult>('/api/v1/publications', { params });
  }

  get(id: string): Observable<PublicationDetail> {
    return this.http.get<PublicationDetail>(`/api/v1/publications/${id}`);
  }
}
