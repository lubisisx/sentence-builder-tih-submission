export interface WordType { id: number; name: string; }
export interface Word { id: number; wordTypeId: number; text: string; }
export interface SentenceWord { position: number; wordId: number; text: string; }
export interface SentenceSummary { id: number; text: string; createdAtUtc: string; updatedAtUtc: string; }
export interface SentenceDetail extends SentenceSummary { words: SentenceWord[]; }
export interface SentencePage { items: SentenceSummary[]; page: number; pageSize: number; totalCount: number; }
export interface SentenceRequest { wordIds: number[]; }
export interface DraftWord extends SentenceWord { key: number; }
