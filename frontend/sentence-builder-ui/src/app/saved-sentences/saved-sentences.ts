import { DatePipe } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';
import { SentencePage } from '../api/models';

@Component({ selector: 'app-saved-sentences', imports: [DatePipe], templateUrl: './saved-sentences.html' })
export class SavedSentences {
  readonly result = input.required<SentencePage>();
  readonly loading = input(false);
  readonly busy = input(false);
  readonly error = input('');
  readonly edit = output<number>();
  readonly pageChanged = output<number>();
  readonly refresh = output<void>();
  readonly pages = computed(() => Math.max(1, Math.ceil(this.result().totalCount / this.result().pageSize)));
}
