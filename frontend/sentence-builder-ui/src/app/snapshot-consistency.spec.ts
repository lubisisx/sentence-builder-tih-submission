import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { App } from './app';
import { SentenceDetail, SentencePage } from './api/models';

// FIFO regressions retain the three original investigation scenarios and assert
// chip/preview equality with the unchanged SQL-backed server policy.
const mixed: SentenceDetail = {
  id: 42, text: 'old snapshot new snapshot',
  createdAtUtc: '2026-10-20T10:00:00Z', updatedAtUtc: '2026-10-20T10:01:00Z',
  words: [
    { position: 0, wordId: 10, text: 'old snapshot' },
    { position: 1, wordId: 10, text: 'new snapshot' },
  ],
};
const emptyPage: SentencePage = { items: [], page: 1, pageSize: 20, totalCount: 0 };

describe('Snapshot consistency FIFO regressions', () => {
  let fixture: ComponentFixture<App>;
  let app: App;
  let http: HttpTestingController;
  const renderedPreview = () => {
    fixture.detectChanges();
    return (fixture.nativeElement as HTMLElement).querySelector('#sentence-preview')!.textContent!.trim();
  };
  const recall = (detail: SentenceDetail) => {
    app.recall(detail.id);
    http.expectOne(`/api/sentences/${detail.id}`).flush(detail);
    fixture.detectChanges();
  };
  const completeSave = (wordIds: number[], serverWords: SentenceDetail['words']) => {
    const response = { ...mixed, words: serverWords, text: serverWords.map(word => word.text).join(' ') };
    const request = http.expectOne('/api/sentences/42');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ wordIds });
    request.flush(response);
    http.expectOne('/api/sentences?page=1&pageSize=20').flush({ ...emptyPage, items: [response], totalCount: 1 });
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('.sentence-text')!.textContent!.trim()).toBe(response.text);
    return response;
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [App], providers: [provideHttpClient(), provideHttpClientTesting()] }).compileComponents();
    fixture = TestBed.createComponent(App); app = fixture.componentInstance; http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    http.expectOne('/api/word-types').flush([{ id: 1, name: 'Noun' }]);
    http.expectOne('/api/words?wordTypeId=1').flush([]);
    http.expectOne('/api/sentences?page=1&pageSize=20').flush(emptyPage);
  });
  afterEach(() => { http.verify(); vi.restoreAllMocks(); });

  it('resolves removing the first mixed-snapshot duplicate to the historical FIFO prefix', () => {
    recall(mixed);
    expect(renderedPreview()).toBe('old snapshot new snapshot');
    const removeFirst = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('[aria-label="Remove word 1: old snapshot"]')!;
    removeFirst.click();
    const previewBeforeSave = renderedPreview();
    expect(previewBeforeSave).toBe('old snapshot');
    expect(app.draft().map(word => word.wordId)).toEqual([10]);
    app.save();
    const saved = completeSave([10], [{ position: 0, wordId: 10, text: 'old snapshot' }]);
    expect(saved.text).toBe(previewBeforeSave);
  });

  it('keeps canonical text and dirty=false when swapping identical IDs', () => {
    recall(mixed);
    const moveSecondLeft = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('[aria-label="Move word 2 left"]')!;
    moveSecondLeft.click();
    const previewBeforeSave = renderedPreview();
    expect(previewBeforeSave).toBe('old snapshot new snapshot');
    expect(app.dirty()).toBe(false);
    app.save();
    const saved = completeSave([10, 10], mixed.words);
    expect(saved.text).toBe(previewBeforeSave);
  });

  it('retains history when removing and re-adding the same ID without duplicates', () => {
    recall({ ...mixed, text: 'old snapshot', words: [mixed.words[0]!] });
    app.removeWord(0);
    app.loadWords(1, true);
    http.expectOne('/api/words?wordTypeId=1').flush([{ id: 10, wordTypeId: 1, text: 'new snapshot' }]);
    app.addWord(app.groups()[0]!.words[0]!);
    const previewBeforeSave = renderedPreview();
    expect(previewBeforeSave).toBe('old snapshot');
    expect(app.dirty()).toBe(false);
    app.save();
    const saved = completeSave([10], [{ position: 0, wordId: 10, text: 'old snapshot' }]);
    expect(saved.text).toBe(previewBeforeSave);
  });

  const observeCurrent = (text = 'current vocabulary') => {
    app.loadWords(1, true);
    http.expectOne('/api/words?wordTypeId=1').flush([{ id: 10, wordTypeId: 1, text }]);
  };
  const assertDisplay = (texts: string[]) => {
    expect(app.draft().map(word => word.text)).toEqual(texts);
    expect(app.draft().map(word => word.position)).toEqual(texts.map((_, index) => index));
    expect(renderedPreview()).toBe(texts.join(' '));
    const chips = (fixture.nativeElement as HTMLElement).querySelectorAll('.word-caption strong');
    expect(Array.from(chips, chip => chip.textContent!.trim())).toEqual(texts);
  };

  it.each([0, 1, 2])('removing mixed duplicate at index %i retains the same FIFO prefix and surviving keys', index => {
    recall({ ...mixed, words: [
      { position: 0, wordId: 10, text: 'first' },
      { position: 1, wordId: 10, text: 'middle' },
      { position: 2, wordId: 10, text: 'last' },
    ] });
    const keys = app.draft().map(word => word.key);
    app.removeWord(index);
    assertDisplay(['first', 'middle']);
    expect(app.draft().map(word => word.key)).toEqual(keys.filter((_, i) => i !== index));
    app.save();
    const saved = completeSave([10, 10], [
      { position: 0, wordId: 10, text: 'first' }, { position: 1, wordId: 10, text: 'middle' },
    ]);
    expect(saved.text).toBe('first middle');
  });

  it('reorders different IDs around duplicates while retaining per-ID FIFO and local keys', () => {
    recall({ ...mixed, words: [mixed.words[0]!, { position: 1, wordId: 20, text: 'other' }, { ...mixed.words[1]!, position: 2 }] });
    const keys = app.draft().map(word => word.key);
    app.moveWord({ index: 2, direction: -1 });
    assertDisplay(['old snapshot', 'new snapshot', 'other']);
    expect(app.draft().map(word => word.wordId)).toEqual([10, 10, 20]);
    expect(app.draft().map(word => word.key)).toEqual([keys[0], keys[2], keys[1]]);
    expect(app.dirty()).toBe(true);
    app.moveWord({ index: 1, direction: -1 });
    assertDisplay(['old snapshot', 'new snapshot', 'other']);
  });

  it('restores removed historical occurrences before using current text for excess occurrences', () => {
    recall(mixed); observeCurrent();
    app.removeWord(0); assertDisplay(['old snapshot']);
    app.addWord(app.groups()[0]!.words[0]!); assertDisplay(['old snapshot', 'new snapshot']);
    expect(app.dirty()).toBe(false);
    app.addWord(app.groups()[0]!.words[0]!); assertDisplay(['old snapshot', 'new snapshot', 'current vocabulary']);
    app.removeWord(0); assertDisplay(['old snapshot', 'new snapshot']);
    app.addWord(app.groups()[0]!.words[0]!); assertDisplay(['old snapshot', 'new snapshot', 'current vocabulary']);
  });

  it('uses new vocabulary values only for excess occurrences after a later vocabulary read', () => {
    recall(mixed); observeCurrent('live one'); app.addWord(app.groups()[0]!.words[0]!);
    assertDisplay(['old snapshot', 'new snapshot', 'live one']);
    observeCurrent('live two');
    assertDisplay(['old snapshot', 'new snapshot', 'live two']);
  });

  it('keeps an independent immutable copy of recalled snapshots', () => {
    const response = structuredClone(mixed);
    recall(response);
    response.words[0]!.text = 'mutated response';
    app.removeWord(1); assertDisplay(['old snapshot']);
  });

  it('replaces the baseline when another sentence uses the same word IDs', () => {
    recall(mixed);
    recall({ ...mixed, id: 43, words: [{ position: 0, wordId: 10, text: 'other sentence history' }] });
    observeCurrent(); app.addWord(app.groups()[0]!.words[0]!);
    assertDisplay(['other sentence history', 'current vocabulary']);
    expect(app.editingId()).toBe(43);
  });

  it.each([500, 409])('keeps the complete baseline, draft and edit ID after save status %i', status => {
    recall(mixed); observeCurrent(); app.removeWord(0);
    const keys = app.draft().map(word => word.key);
    app.save();
    http.expectOne('/api/sentences/42').flush({}, { status, statusText: 'Save failed' });
    assertDisplay(['old snapshot']);
    expect(app.draft().map(word => word.key)).toEqual(keys);
    expect(app.editingId()).toBe(42);
    app.addWord(app.groups()[0]!.words[0]!); assertDisplay(['old snapshot', 'new snapshot']);
    app.addWord(app.groups()[0]!.words[0]!); assertDisplay(['old snapshot', 'new snapshot', 'current vocabulary']);
  });

  it('keeps the previous baseline when a different sentence recall fails', () => {
    recall(mixed); observeCurrent(); app.removeWord(0);
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    app.recall(99); http.expectOne('/api/sentences/99').flush({}, { status: 404, statusText: 'Missing' });
    expect(app.editingId()).toBe(42);
    app.addWord(app.groups()[0]!.words[0]!); assertDisplay(['old snapshot', 'new snapshot']);
  });

  it('clears history after cancellation while allowing loaded vocabulary for a new sentence', () => {
    recall(mixed); observeCurrent(); app.cancel();
    expect(app.editingId()).toBeNull();
    app.addWord(app.groups()[0]!.words[0]!); assertDisplay(['current vocabulary']);
    expect(app.dirty()).toBe(true);
  });

  it('clears history after a successful edit before a new creation', () => {
    recall(mixed); observeCurrent(); app.removeWord(0); app.save();
    completeSave([10], [{ position: 0, wordId: 10, text: 'old snapshot' }]);
    expect(app.editingId()).toBeNull();
    app.addWord(app.groups()[0]!.words[0]!); assertDisplay(['current vocabulary']);
    app.save();
    const post = http.expectOne('/api/sentences');
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual({ wordIds: [10] });
    post.flush({ ...mixed, id: 44, text: 'current vocabulary', words: [{ position: 0, wordId: 10, text: 'current vocabulary' }] });
    http.expectOne('/api/sentences?page=1&pageSize=20').flush(emptyPage);
  });
});
