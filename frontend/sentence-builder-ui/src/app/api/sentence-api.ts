import { HttpClient } from '@angular/common/http';
import { inject, Injectable, InjectionToken } from '@angular/core';
import { deploymentApiUrl } from './deployment-config';
import { SentenceDetail, SentencePage, SentenceRequest, Word, WordType } from './models';

export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', { providedIn: 'root', factory: () => deploymentApiUrl() });

@Injectable({ providedIn: 'root' })
export class SentenceApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  getWordTypes() { return this.http.get<WordType[]>(`${this.base}/word-types`); }
  getWords(wordTypeId: number) { return this.http.get<Word[]>(`${this.base}/words`, { params: { wordTypeId } }); }
  getSentences(page = 1, pageSize = 20) { return this.http.get<SentencePage>(`${this.base}/sentences`, { params: { page, pageSize } }); }
  getSentence(id: number) { return this.http.get<SentenceDetail>(`${this.base}/sentences/${id}`); }
  create(request: SentenceRequest) { return this.http.post<SentenceDetail>(`${this.base}/sentences`, request); }
  update(id: number, request: SentenceRequest) { return this.http.put<SentenceDetail>(`${this.base}/sentences/${id}`, request); }
}
