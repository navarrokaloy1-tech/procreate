// Rewrites src/environments/environment.ts from the API_BASE env var before a
// build. Render's Blueprint injects API_BASE as the API service's host (no
// scheme), so the deployed UI points at the real API URL even if the service
// is renamed. A no-op locally, where API_BASE is unset, leaving the committed
// localhost default in place.
import { writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const host = process.env.API_BASE?.trim();
if (!host) {
  console.log('[set-api-base] API_BASE not set — keeping environment.ts as committed (localhost).');
  process.exit(0);
}

const origin = /^https?:\/\//i.test(host) ? host : `https://${host}`;
const apiBase = `${origin.replace(/\/+$/, '')}/api`;

const target = join(dirname(fileURLToPath(import.meta.url)), '..', 'src', 'environments', 'environment.ts');
writeFileSync(
  target,
  `// Generated at build time by scripts/set-api-base.mjs from API_BASE.
export const environment = {
  production: true,
  apiBase: '${apiBase}',
};
`,
);
console.log(`[set-api-base] environment.ts apiBase = ${apiBase}`);
