import { HttpErrorResponse } from '@angular/common/http';

export function apiErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof HttpErrorResponse)) return fallback;
  if (error.status === 0) return 'The API could not be reached. Check your connection. Your draft has been kept. If you were saving, check the saved list before trying again.';
  if (error.status === 409) return 'This sentence changed while saving. Your draft has been kept. Reload the saved sentence before trying again.';
  if (error.status === 404) return 'The requested record was not found. Your current draft has been kept.';
  // Only display the documented validation strings; never surface raw server errors.
  if (error.status === 400 && typeof error.error === 'object' && error.error !== null) {
    const problem = error.error as { errors?: Record<string, unknown> };
    if (problem.errors && typeof problem.errors === 'object') {
      const messages = Object.values(problem.errors).flatMap(value =>
        Array.isArray(value) ? value.filter((message): message is string => typeof message === 'string') : []);
      if (messages.length) return messages.join(' ');
    }
  }
  return fallback;
}
