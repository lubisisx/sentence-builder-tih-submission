/** Public deployment settings only. Never include connection strings or credentials. */
declare global {
  interface Window { sentenceBuilderConfig?: { apiBaseUrl?: string }; }
}

export function deploymentApiUrl(value = window.sentenceBuilderConfig?.apiBaseUrl ?? '/api'): string {
  if (value === '/api') return value;
  try {
    const url = new URL(value);
    if (url.protocol === 'https:' && !url.username && !url.password && !url.search && !url.hash
        && url.pathname.replace(/\/$/, '') === '/api') return value.replace(/\/$/, '');
  } catch { /* Report a safe configuration error without printing the value. */ }
  throw new Error('Configure apiBaseUrl as /api or an absolute HTTPS URL ending in /api.');
}
