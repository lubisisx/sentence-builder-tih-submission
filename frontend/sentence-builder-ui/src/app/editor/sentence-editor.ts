import { afterNextRender, Component, computed, inject, Injector, input, output, signal } from '@angular/core';
import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDropList } from '@angular/cdk/drag-drop';
import { CdkScrollable } from '@angular/cdk/scrolling';
import { DraftWord, Word, WordType } from '../api/models';

export interface WordGroup {
  type: WordType;
  words: Word[];
  loading: boolean;
  loaded: boolean;
  error: string;
}

@Component({
  selector: 'app-sentence-editor',
  imports: [CdkDrag, CdkDragHandle, CdkDropList, CdkScrollable],
  templateUrl: './sentence-editor.html',
})
export class SentenceEditor {
  private readonly injector = inject(Injector);
  readonly groups = input.required<WordGroup[]>();
  readonly draft = input.required<DraftWord[]>();
  readonly editingId = input<number | null>(null);
  readonly busy = input(false);
  readonly saving = input(false);
  readonly typesLoading = input(false);
  readonly typesError = input('');
  readonly error = input('');
  readonly addWord = output<Word>();
  readonly removeWord = output<number>();
  readonly moveWord = output<{ index: number; direction: -1 | 1 }>();
  readonly reorderWord = output<{ previousIndex: number; currentIndex: number }>();
  readonly saveDraft = output<void>();
  readonly cancelDraft = output<void>();
  readonly retryTypes = output<void>();
  readonly retryWords = output<number>();
  readonly announcement = signal('');
  readonly dragging = signal(false);
  readonly preview = computed(() => this.draft().map(word => word.text).join(' '));
  readonly atLimit = computed(() => this.draft().length >= 100);

  add(word: Word) {
    if (this.busy() || this.atLimit()) return;
    const position = this.draft().length + 1;
    this.addWord.emit(word);
    this.announcement.set(`Word added at position ${position}.`);
  }

  drop(event: CdkDragDrop<DraftWord[]>) {
    this.dragging.set(false);
    if (this.busy() || !event.isPointerOverContainer) return;
    if (event.previousContainer === event.container) {
      this.reorderWord.emit({ previousIndex: event.previousIndex, currentIndex: event.currentIndex });
      this.announcement.set(`Moved word ${event.previousIndex + 1} to position ${event.currentIndex + 1}.`);
    } else {
      // Library drops append just like tap/click; source words remain reusable.
      this.add(event.item.data as Word);
    }
  }

  move(index: number, direction: -1 | 1) {
    this.moveWord.emit({ index, direction });
    this.announcement.set(`Moved word ${index + 1} to position ${index + direction + 1}.`);
  }

  remove(index: number) {
    const word = this.draft()[index];
    this.removeWord.emit(index);
    this.announcement.set(`Removed ${word?.text ?? 'word'} at position ${index + 1}.`);
    // Keep keyboard focus in the sentence after the focused occurrence disappears.
    afterNextRender(() => {
      const target = document.getElementById(`word-remove-${Math.min(index, this.draft().length - 1)}`) ?? document.getElementById('editor-heading');
      target?.focus();
    }, { injector: this.injector });
  }
}
