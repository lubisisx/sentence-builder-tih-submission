import { Component, computed, DestroyRef, HostListener, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { SentenceApi } from './api/sentence-api';
import { apiErrorMessage } from './api/api-error';
import { DraftWord, SentencePage, SentenceWord, Word, WordType } from './api/models';
import { SentenceEditor, WordGroup } from './editor/sentence-editor';
import { SavedSentences } from './saved-sentences/saved-sentences';

@Component({ selector: 'app-root', imports: [SentenceEditor, SavedSentences], templateUrl: './app.html', styleUrl: './app.css' })
export class App implements OnInit {
  private readonly api = inject(SentenceApi);
  private readonly destroyRef = inject(DestroyRef);
  private listRequest?: Subscription;
  private key = 0;
  private requestedPage = 1;
  private readonly baseline = signal<readonly Readonly<Pick<SentenceWord, 'wordId' | 'text'>>[]>([]);
  private readonly vocabularyText = new Map<number, string>();
  readonly types = signal<WordType[]>([]);
  readonly groups = signal<WordGroup[]>([]);
  readonly draft = signal<DraftWord[]>([]);
  readonly editingId = signal<number | null>(null);
  readonly typesLoading = signal(false);
  readonly listLoading = signal(false);
  readonly saving = signal(false);
  readonly recalling = signal(false);
  readonly typesError = signal('');
  readonly listError = signal('');
  readonly editorError = signal('');
  readonly success = signal('');
  readonly result = signal<SentencePage>({ items: [], page: 1, pageSize: 20, totalCount: 0 });
  readonly busy = computed(() => this.saving() || this.recalling());
  readonly dirty = computed(() => JSON.stringify(this.draft().map(word => word.wordId)) !== JSON.stringify(this.baseline().map(word => word.wordId)));

  ngOnInit() { this.loadTypes(); this.loadPage(1); }

  loadTypes() {
    if (this.typesLoading()) return;
    this.typesLoading.set(true); this.typesError.set('');
    this.api.getWordTypes().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: types => {
        this.types.set(types); this.typesLoading.set(false);
        this.groups.set(types.map(type => ({ type, words: [], loading: false, loaded: false, error: '' })));
        for (const type of types) this.loadWords(type.id);
      },
      error: error => { this.typesLoading.set(false); this.typesError.set(apiErrorMessage(error, 'Could not load word categories. Please try again.')); },
    });
  }

  loadWords(id: number, refresh = false) {
    const group = this.groups().find(group => group.type.id === id);
    if (!group || group.loading || (group.loaded && !refresh)) return;
    this.updateGroup(id, { loading: true, error: '' });
    this.api.getWords(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: words => {
        this.updateGroup(id, { words, loading: false, loaded: true });
        for (const word of words) this.vocabularyText.set(word.id, word.text);
        this.draft.set(this.resolveSnapshots(this.draft()));
      },
      error: error => this.updateGroup(id, { loading: false, error: apiErrorMessage(error, 'Could not load words. Please try again.') }),
    });
  }

  private updateGroup(id: number, change: Partial<WordGroup>) {
    this.groups.update(groups => groups.map(group => group.type.id === id ? { ...group, ...change } : group));
  }

  addWord(selected: Word) {
    if (this.busy()) return;
    const word = this.groups().flatMap(group => group.words).find(word => word.id === selected.id);
    if (!word) { this.editorError.set('Choose an available word before adding it.'); return; }
    if (this.draft().length >= 100) { this.editorError.set('A sentence can contain at most 100 words.'); return; }
    this.vocabularyText.set(word.id, word.text);
    this.setDraft([...this.draft(), { key: ++this.key, position: this.draft().length, wordId: word.id, text: word.text }]);
  }

  reorderWord({ previousIndex, currentIndex }: { previousIndex: number; currentIndex: number }) {
    if (this.busy() || previousIndex < 0 || currentIndex < 0 || previousIndex >= this.draft().length || currentIndex >= this.draft().length) return;
    const words = [...this.draft()];
    const [word] = words.splice(previousIndex, 1);
    words.splice(currentIndex, 0, word!);
    this.setDraft(words);
  }

  removeWord(index: number) {
    if (this.busy() || index < 0 || index >= this.draft().length) return;
    this.setDraft(this.draft().filter((_, i) => i !== index));
  }

  moveWord({ index, direction }: { index: number; direction: -1 | 1 }) {
    this.reorderWord({ previousIndex: index, currentIndex: index + direction });
  }

  private setDraft(words: DraftWord[]) {
    this.draft.set(this.resolveSnapshots(words));
    this.editorError.set(''); this.success.set('');
  }

  // Match the server's FIFO policy from the complete, immutable recall baseline.
  // Fresh queues prevent intermediate remove/add operations from consuming history.
  private resolveSnapshots(words: readonly DraftWord[]): DraftWord[] {
    const historical = new Map<number, string[]>();
    for (const word of this.baseline()) {
      const queue = historical.get(word.wordId) ?? [];
      queue.push(word.text);
      historical.set(word.wordId, queue);
    }
    return words.map((word, position) => ({
      ...word,
      position,
      text: historical.get(word.wordId)?.shift() ?? this.vocabularyText.get(word.wordId) ?? word.text,
    }));
  }

  private canDiscard() { return !this.dirty() || window.confirm('Discard your unsaved sentence changes?'); }

  cancel() {
    if (this.busy() || !this.canDiscard()) return;
    this.reset(); this.success.set('');
  }

  private reset() {
    this.draft.set([]); this.editingId.set(null); this.baseline.set([]); this.editorError.set('');
  }

  recall(id: number) {
    if (this.busy() || !this.canDiscard()) return;
    this.recalling.set(true); this.editorError.set(''); this.success.set('');
    this.api.getSentence(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: sentence => {
        const ordered = [...sentence.words].sort((a, b) => a.position - b.position);
        this.baseline.set(Object.freeze(ordered.map(word => Object.freeze({ wordId: word.wordId, text: word.text }))));
        this.draft.set(this.resolveSnapshots(ordered.map(word => ({ ...word, key: ++this.key }))));
        this.editingId.set(sentence.id); this.recalling.set(false);
        document.getElementById('editor-heading')?.focus();
      },
      error: error => { this.recalling.set(false); this.editorError.set(apiErrorMessage(error, 'Could not recall this sentence. Your draft has been kept.')); },
    });
  }

  save() {
    if (this.busy()) return;
    if (this.draft().length === 0 || this.draft().length > 100) { this.editorError.set('Select between 1 and 100 words before saving.'); return; }
    const editingId = this.editingId();
    const request = { wordIds: this.draft().map(word => word.wordId) };
    this.saving.set(true); this.editorError.set(''); this.success.set('');
    const operation = editingId === null ? this.api.create(request) : this.api.update(editingId, request);
    operation.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: sentence => {
        this.saving.set(false); this.reset();
        this.success.set(`Sentence #${sentence.id} ${editingId === null ? 'saved' : 'updated'} successfully.`);
        this.loadPage(editingId === null ? 1 : this.result().page);
      },
      error: error => { this.saving.set(false); this.editorError.set(apiErrorMessage(error, 'Saving failed. Your draft has been kept. Check the saved list before trying again.')); },
    });
  }

  loadPage(page: number) {
    if (page < 1) return;
    this.listRequest?.unsubscribe(); this.requestedPage = page;
    this.listLoading.set(true); this.listError.set('');
    this.listRequest = this.api.getSentences(page, this.result().pageSize).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: result => { this.result.set(result); this.listLoading.set(false); },
      error: error => { this.listLoading.set(false); this.listError.set(apiErrorMessage(error, 'Could not load saved sentences. Previous results may be out of date.')); },
    });
  }

  retryList() { this.loadPage(this.requestedPage); }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent) { if (this.dirty()) { event.preventDefault(); event.returnValue = ''; } }
}
