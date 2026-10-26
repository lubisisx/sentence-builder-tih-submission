import { writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
const value = process.env.API_BASE_URL || '/api';
if (value !== '/api') {
  let url;
  try { url = new URL(value); } catch { throw new Error('API_BASE_URL must be /api or an absolute HTTPS URL ending in /api.'); }
  if (url.protocol !== 'https:' || url.username || url.password || url.search || url.hash || url.pathname.replace(/\/$/, '') !== '/api')
    throw new Error('API_BASE_URL must be /api or an absolute HTTPS URL ending in /api.');
}
const target = resolve(process.argv[2] || 'dist/sentence-builder-ui/browser', 'runtime-config.js');
await writeFile(target, `window.sentenceBuilderConfig = ${JSON.stringify({apiBaseUrl:value.replace(/\/$/, '')})};\n`);
console.log('Public frontend API URL configured. No secrets belong in this file.');
