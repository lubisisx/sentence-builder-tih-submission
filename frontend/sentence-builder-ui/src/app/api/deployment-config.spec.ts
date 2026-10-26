import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SentenceApi } from './sentence-api';
import { deploymentApiUrl } from './deployment-config';

describe('Public deployment API configuration', () => {
  afterEach(() => { delete window.sentenceBuilderConfig; });
  it('preserves the local same-origin API default', () => {
    expect(deploymentApiUrl()).toBe('/api');
  });
  it('reads a deployed public URL and normalizes the trailing slash', () => {
    window.sentenceBuilderConfig = {apiBaseUrl: 'https://assessment.azurewebsites.net/api/'};
    expect(deploymentApiUrl()).toBe('https://assessment.azurewebsites.net/api');
  });
  it.each(['http://example.com/api', 'https://user:secret@example.com/api', '//example.com/api', 'https://example.com', 'https://example.com/api?secret=1', 'https://example.com/api#fragment'])('rejects invalid deployment values without echoing them', value => {
    expect(() => deploymentApiUrl(value)).toThrow('Configure apiBaseUrl');
    try { deploymentApiUrl(value); } catch (error) { expect((error as Error).message).not.toContain(value); }
  });
  it('uses the configured deployment URL in actual API service requests', () => {
    window.sentenceBuilderConfig = {apiBaseUrl:'https://assessment.azurewebsites.net/api'};
    TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting()]});
    const http=TestBed.inject(HttpTestingController);
    TestBed.inject(SentenceApi).getWordTypes().subscribe();
    http.expectOne('https://assessment.azurewebsites.net/api/word-types').flush([]);
    http.verify();
  });

});
