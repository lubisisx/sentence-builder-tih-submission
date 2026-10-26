import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { App } from './app';
import { By } from '@angular/platform-browser';
import { CdkDragDrop, CdkDropList } from '@angular/cdk/drag-drop';
import { DraftWord } from './api/models';
import { SentenceEditor } from './editor/sentence-editor';
import { SentenceDetail, SentencePage } from './api/models';

const detail: SentenceDetail = {
  id: 42, text: 'cat runs cat', createdAtUtc: '2026-10-20T10:00:00Z', updatedAtUtc: '2026-10-20T10:00:00Z',
  words: [{ position: 0, wordId: 10, text: 'cat' }, { position: 1, wordId: 20, text: 'runs' }, { position: 2, wordId: 10, text: 'cat' }],
};
const emptyPage: SentencePage = { items: [], page: 1, pageSize: 20, totalCount: 0 };

describe('App', () => {
  let fixture: ComponentFixture<App>;
  let app: App;
  let http: HttpTestingController;
  const listRequest = (page = 1) => http.expectOne(`/api/sentences?page=${page}&pageSize=20`);
  const cat = { id: 10, wordTypeId: 1, text: 'cat' };
  const dog = { id: 11, wordTypeId: 1, text: 'dog' };
  const flushInitial = () => {
    http.expectOne('/api/word-types').flush([{ id: 1, name: 'Noun' }, { id: 2, name: 'Verb' }]);
    http.expectOne('/api/words?wordTypeId=1').flush([cat, dog]);
    http.expectOne('/api/words?wordTypeId=2').flush([{ id: 20, wordTypeId: 2, text: 'runs' }]);
    listRequest().flush(emptyPage);
  };
  const add = () => app.addWord(cat);

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [App], providers: [provideHttpClient(), provideHttpClientTesting()] }).compileComponents();
    fixture = TestBed.createComponent(App); app = fixture.componentInstance; http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });
  afterEach(() => { http.verify(); vi.restoreAllMocks(); });

  it('should create the app', () => { flushInitial(); expect(app).toBeTruthy(); });
  it('should render title', async () => {
    flushInitial(); await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Build a sentence');
  });

  it('loads database categories and an empty saved page on startup', () => {
    expect(app.typesLoading()).toBe(true); expect(app.listLoading()).toBe(true);
    flushInitial(); fixture.detectChanges();
    expect(app.types().map(type => type.name)).toEqual(['Noun', 'Verb']);
    expect(app.typesLoading()).toBe(false);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No sentences on this page yet');
  });

  it('loads all category vocabularies once and caches successful reads', () => {
    flushInitial(); fixture.detectChanges();
    expect(app.groups().map(group => group.words.length)).toEqual([2, 1]);
    app.loadWords(1); app.loadWords(2);
    http.expectNone('/api/words?wordTypeId=1');
    const category = (fixture.nativeElement as HTMLElement).querySelector('details')!;
    category.open = false; category.open = true;
    http.expectNone(request => request.url === '/api/words');
  });

  it('keeps category responses independent when requests complete out of order', () => {
    http.expectOne('/api/word-types').flush([{ id: 1, name: 'Noun' }, { id: 2, name: 'Verb' }]);
    listRequest().flush(emptyPage);
    http.expectOne('/api/words?wordTypeId=2').flush([{ id: 20, wordTypeId: 2, text: 'runs' }]);
    http.expectOne('/api/words?wordTypeId=1').flush([cat]);
    expect(app.groups().map(group => group.words[0]?.text)).toEqual(['cat', 'runs']);
  });

  it('adds repeated occurrences with distinct keys and sequential positions', () => {
    flushInitial(); add(); app.addWord(cat);
    expect(app.draft().map(word => word.wordId)).toEqual([10, 10]);
    expect(new Set(app.draft().map(word => word.key)).size).toBe(2);
    expect(app.draft().map(word => word.position)).toEqual([0, 1]);
  });

  it('removes one occurrence and reorders without losing IDs', () => {
    flushInitial(); add(); app.addWord(dog); app.addWord(cat);
    app.moveWord({ index: 1, direction: -1 });
    expect(app.draft().map(word => word.wordId)).toEqual([11, 10, 10]);
    app.removeWord(1);
    expect(app.draft().map(word => word.wordId)).toEqual([11, 10]);
    expect(app.draft().map(word => word.position)).toEqual([0, 1]);
    app.moveWord({ index: 0, direction: -1 }); expect(app.draft().length).toBe(2);
  });

  it('disables empty saves and validates empty or invalid selections without HTTP writes', () => {
    flushInitial(); fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('#save-sentence')?.disabled).toBe(true);
    app.save(); expect(app.editorError()).toContain('Select between');
    app.addWord({ id: 999, wordTypeId: 1, text: 'unavailable' }); expect(app.editorError()).toContain('Choose an available word');
    http.expectNone(request => request.method === 'POST');
  });

  it('enforces the 100 occurrence limit', () => {
    flushInitial();
    for (let i = 0; i < 101; i++) app.addWord(cat);
    expect(app.draft().length).toBe(100); expect(app.editorError()).toContain('at most 100');
  });

  it('creates using IDs only, blocks duplicate submit and refreshes the first page', () => {
    flushInitial(); add(); app.addWord(cat); app.save(); app.save();
    const request = http.expectOne('/api/sentences');
    expect(request.request.method).toBe('POST'); expect(request.request.body).toEqual({ wordIds: [10, 10] });
    expect(app.saving()).toBe(true);
    request.flush(detail); listRequest().flush({ ...emptyPage, items: [detail], totalCount: 1 });
    expect(app.draft()).toEqual([]); expect(app.success()).toContain('saved successfully'); expect(app.result().totalCount).toBe(1);
  });

  it('uses server paging metadata and preserves page state on request failure', () => {
    flushInitial(); app.loadPage(2);
    listRequest(2).flush({ ...emptyPage, page: 2, totalCount: 41, items: [detail] });
    expect(app.result().page).toBe(2); expect(app.result().totalCount).toBe(41);
    fixture.detectChanges(); expect((fixture.nativeElement as HTMLElement).textContent).toContain('Page 2 of 3');
    app.loadPage(3); listRequest(3).flush({}, { status: 500, statusText: 'Failure' });
    expect(app.result().page).toBe(2); expect(app.listError()).toContain('Could not load saved');
    app.retryList(); listRequest(3).flush({ ...emptyPage, page: 3, totalCount: 41 });
  });

  it('recalls ordered detail snapshots without parsing rendered sentence text', () => {
    flushInitial(); app.recall(42);
    http.expectOne('/api/sentences/42').flush({ ...detail, text: 'not used to reconstruct', words: [...detail.words].reverse() });
    expect(app.draft().map(word => word.wordId)).toEqual([10, 20, 10]);
    expect(app.draft().map(word => word.text)).toEqual(['cat', 'runs', 'cat']);
    expect(app.editingId()).toBe(42); expect(app.dirty()).toBe(false);
  });

  it('saves edits through PUT with the complete new order and refreshes the current page', () => {
    flushInitial(); app.recall(42); http.expectOne('/api/sentences/42').flush(detail);
    app.moveWord({ index: 1, direction: -1 }); app.removeWord(2); app.save();
    const request = http.expectOne('/api/sentences/42');
    expect(request.request.method).toBe('PUT'); expect(request.request.body).toEqual({ wordIds: [20, 10] });
    request.flush(detail); listRequest().flush(emptyPage);
    expect(app.editingId()).toBeNull(); expect(app.success()).toContain('updated successfully');
  });

  it('preserves the complete draft on server failure and never retries writes', () => {
    flushInitial(); add(); const draft = app.draft(); app.save();
    http.expectOne('/api/sentences').flush({ detail: 'private stack' }, { status: 500, statusText: 'Failure' });
    expect(app.draft()).toEqual(draft); expect(app.editorError()).toContain('draft has been kept');
    expect(app.editorError()).not.toContain('private'); expect(app.saving()).toBe(false);
    http.expectNone(request => request.method === 'POST');
  });

  it('preserves edit state and explains concurrency conflicts', () => {
    flushInitial(); app.recall(42); http.expectOne('/api/sentences/42').flush(detail);
    app.removeWord(1); app.save();
    http.expectOne('/api/sentences/42').flush({}, { status: 409, statusText: 'Conflict' });
    expect(app.draft().map(word => word.wordId)).toEqual([10, 10]); expect(app.editingId()).toBe(42);
    expect(app.editorError()).toContain('changed while saving');
  });

  it('preserves an existing draft when recall fails', () => {
    flushInitial(); add(); vi.spyOn(window, 'confirm').mockReturnValue(true);
    app.recall(99); http.expectOne('/api/sentences/99').flush({}, { status: 404, statusText: 'Not Found' });
    expect(app.draft().length).toBe(1); expect(app.editorError()).toContain('not found');
  });

  it('confirms before discarding a draft for cancel or recall', () => {
    flushInitial(); add(); const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    app.cancel(); app.recall(42); expect(app.draft().length).toBe(1);
    http.expectNone('/api/sentences/42'); expect(confirm).toHaveBeenCalledTimes(2);
    confirm.mockReturnValue(true); app.cancel(); expect(app.draft()).toEqual([]);
  });

  it('warns before refresh with unsaved changes', () => {
    flushInitial(); add(); const event = new Event('beforeunload', { cancelable: true });
    app.beforeUnload(event as BeforeUnloadEvent); expect(event.defaultPrevented).toBe(true);
  });

  it('handles vocabulary failure and supports explicit retry', () => {
    http.expectOne('/api/word-types').flush({}, { status: 503, statusText: 'Unavailable' }); listRequest().flush(emptyPage);
    expect(app.typesError()).toContain('Could not load word categories');
    app.loadTypes(); http.expectOne('/api/word-types').flush([{id: 1, name: 'Noun'}]); expect(app.typesError()).toBe('');
    http.expectOne('/api/words?wordTypeId=1').flush({}, { status: 500, statusText: 'Failure' });
    expect(app.groups()[0]?.error).toContain('Could not load words');
    app.loadWords(1); http.expectOne('/api/words?wordTypeId=1').flush([cat]);
    expect(app.groups()[0]?.error).toBe('');
  });

  it('handles unavailable API and exposes only supported validation strings', () => {
    flushInitial(); add(); app.save();
    http.expectOne('/api/sentences').error(new ProgressEvent('error'));
    expect(app.editorError()).toContain('could not be reached'); expect(app.draft().length).toBe(1);
    app.save(); http.expectOne('/api/sentences').flush({ errors: { wordIds: ['A selected word was removed.'] } }, { status: 400, statusText: 'Bad Request' });
    expect(app.editorError()).toBe('A selected word was removed.');
  });
  const editor = () => fixture.debugElement.query(By.directive(SentenceEditor)).componentInstance as SentenceEditor;
  const drop = (sameList: boolean, previousIndex = 0, currentIndex = 0, over = true) => {
    fixture.detectChanges();
    const target = fixture.debugElement.queryAll(By.directive(CdkDropList)).find(node => node.attributes['id'] === 'sentence-drop')!;
    const container = target.injector.get(CdkDropList);
    container.dropped.emit({ container, previousContainer: sameList ? container : {}, previousIndex, currentIndex,
      item: {data: cat}, isPointerOverContainer: over } as CdkDragDrop<DraftWord[]>);
    fixture.detectChanges();
  };

  it('renders every category returned by the API, including nine groups, with independent loading states', () => {
    const types = Array.from({length: 9}, (_, i) => ({id: i + 1, name: 'Category ' + (i + 1)}));
    http.expectOne('/api/word-types').flush(types); listRequest().flush(emptyPage);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('details').length).toBe(9);
    expect(app.groups().every(group => group.loading)).toBe(true);
    for (const type of types) http.expectOne('/api/words?wordTypeId=' + type.id).flush([]);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No words in this category yet');
  });

  it('adds repeated words by native button clicks without selecting a category or dragging', () => {
    flushInitial(); fixture.detectChanges();
    const button = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('[aria-label="Add cat (Noun)"]')!;
    button.click(); button.click(); fixture.detectChanges();
    expect(app.draft().map(word => word.wordId)).toEqual([10, 10]);
    expect((fixture.nativeElement as HTMLElement).querySelector('#sentence-preview')!.textContent).toContain('cat cat');
    expect(button.tagName).toBe('BUTTON'); expect(button.disabled).toBe(false);
  });

  it('adds a copied library word when the CDK editor drop event fires, leaving the source reusable', () => {
    flushInitial(); drop(false); drop(false);
    expect(app.draft().map(word => word.wordId)).toEqual([10, 10]);
    expect(app.groups()[0]!.words.map(word => word.id)).toEqual([10, 11]);
    expect(new Set(app.draft().map(word => word.key)).size).toBe(2);
  });

  it('routes CDK reorder events through the canonical draft resolver', () => {
    flushInitial(); add(); app.addWord(dog); add();
    const keys = app.draft().map(word => word.key);
    drop(true, 2, 0);
    expect(app.draft().map(word => word.key)).toEqual([keys[2], keys[0], keys[1]]);
    expect(app.draft().map(word => word.position)).toEqual([0, 1, 2]);
    expect(app.draft().map(word => word.wordId)).toEqual([10, 10, 11]);
  });

  it('ignores drops outside the target or while saving and disables library actions at the limit', () => {
    flushInitial(); drop(false, 0, 0, false); expect(app.draft()).toEqual([]);
    for (let i = 0; i < 100; i++) add();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.library-word')!.disabled).toBe(true);
    drop(false); expect(app.draft().length).toBe(100);
    app.save(); drop(true, 0, 1);
    expect(app.draft()[0]!.position).toBe(0);
    http.expectOne('/api/sentences').flush({}, {status: 500, statusText: 'Failed'});
    expect(app.draft().length).toBe(100);
  });

  it('supports non-drag move and remove controls on individual occurrences', () => {
    flushInitial(); add(); app.addWord(dog); add(); fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    root.querySelector<HTMLButtonElement>('[aria-label="Move word 2 left"]')!.click(); fixture.detectChanges();
    root.querySelector<HTMLButtonElement>('[aria-label="Remove word 2: cat"]')!.click(); fixture.detectChanges();
    expect(app.draft().map(word => word.wordId)).toEqual([11, 10]);
    expect(editor().announcement()).toContain('Removed cat');
  });

  it('retains recalled FIFO snapshots for drag reorder and additional library occurrences', () => {
    flushInitial(); app.recall(42);
    http.expectOne('/api/sentences/42').flush({...detail, words: [
      {wordId: 10, position: 0, text: 'old cat'}, {wordId: 10, position: 1, text: 'new cat'}
    ]});
    drop(true, 1, 0); expect(app.draft().map(word => word.text)).toEqual(['old cat', 'new cat']);
    expect(app.dirty()).toBe(false);
    drop(false); expect(app.draft().map(word => word.text)).toEqual(['old cat', 'new cat', 'cat']);
    app.save();
    const request = http.expectOne('/api/sentences/42'); expect(request.request.body).toEqual({wordIds: [10, 10, 10]});
    request.flush({}, {status: 409, statusText: 'Conflict'});
    expect(app.draft().map(word => word.text)).toEqual(['old cat', 'new cat', 'cat']);
  });

  it('avoids duplicate word reads while in flight and retries only the failed category', () => {
    http.expectOne('/api/word-types').flush([{id: 1, name: 'Noun'}, {id: 2, name: 'Verb'}]);
    listRequest().flush(emptyPage); app.loadWords(1);
    http.expectOne('/api/words?wordTypeId=1').flush({}, {status: 500, statusText: 'Failure'});
    http.expectOne('/api/words?wordTypeId=2').flush([]);
    fixture.detectChanges();
    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.category .notice button')!.click();
    http.expectOne('/api/words?wordTypeId=1').flush([cat]);
    http.expectNone('/api/words?wordTypeId=2');
  });

  it('shows empty categories without disabling a recalled sentence', () => {
    http.expectOne('/api/word-types').flush([]); listRequest().flush(emptyPage);
    app.recall(42); http.expectOne('/api/sentences/42').flush(detail); fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No word categories are available yet');
    expect((fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('#save-sentence')!.disabled).toBe(false);
  });

  it('can append from a cached library after cancelling an edit or completing a save without leaking historical text', () => {
    flushInitial(); app.recall(42);
    http.expectOne('/api/sentences/42').flush({...detail, words:[{wordId:10,position:0,text:'historic cat'}]});
    app.cancel(); add(); expect(app.draft()[0]!.text).toBe('cat');
    app.save(); http.expectOne('/api/sentences').flush(detail); listRequest().flush(emptyPage);
    add(); expect(app.draft()[0]!.text).toBe('cat');
    http.expectNone('/api/words?wordTypeId=1');
  });

  it('keeps a library word loaded when another category fails and retains the draft on failed recall', () => {
    http.expectOne('/api/word-types').flush([{id:1,name:'Noun'},{id:2,name:'Verb'}]);listRequest().flush(emptyPage);
    http.expectOne('/api/words?wordTypeId=1').flush([cat]);
    http.expectOne('/api/words?wordTypeId=2').flush({}, {status:503,statusText:'Unavailable'});
    fixture.detectChanges(); (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.library-word')!.click();
    vi.spyOn(window,'confirm').mockReturnValue(true); app.recall(99);
    http.expectOne('/api/sentences/99').flush({}, {status:404,statusText:'Not found'});
    expect(app.draft().map(word=>word.wordId)).toEqual([10]);expect(app.groups()[0]!.words[0]!.text).toBe('cat');
  });

  it('restores focus to a surviving removal control after the occurrence DOM updates', async () => {
    flushInitial(); add(); add(); add(); fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const remove = root.querySelector<HTMLButtonElement>('[aria-label="Remove word 2: cat"]')!;
    remove.focus(); remove.click(); fixture.detectChanges(); await fixture.whenStable();
    expect(document.activeElement?.id).toBe('word-remove-1');
    app.removeWord(1); fixture.detectChanges();
    root.querySelector<HTMLButtonElement>('[aria-label="Remove word 1: cat"]')!.click();fixture.detectChanges();await fixture.whenStable();
    expect(document.activeElement?.id).toBe('editor-heading');
  });

});
