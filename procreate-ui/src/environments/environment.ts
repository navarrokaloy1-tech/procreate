// API base for the app. Committed value is the local dev default; the Render
// build rewrites this file from the API_BASE env var (scripts/set-api-base.mjs)
// so the deployed UI points at the real API.
export const environment = {
  production: false,
  apiBase: 'http://localhost:5230/api',
};
