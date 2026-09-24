#!/usr/bin/env node
/**
 * Development identity provider.
 *
 * Stands in for Authentik so the single sign-on flow can be run end to end
 * without Docker, a Google Cloud project or any account at all. It speaks
 * real OpenID Connect — discovery, PKCE, RS256-signed ID tokens validated
 * against a JWKS — so the API exercises exactly the code path it will use in
 * production. The only thing faked is the part that checks a password.
 *
 * NOT FOR PRODUCTION. It signs anyone in as whoever they click. It is wired
 * up in appsettings.Development.json only, and its signing key is generated
 * fresh on every start, so tokens do not outlive the process.
 *
 *   node tools/dev-idp/server.js
 *
 * See docs/sso-authentik.md for the real thing.
 */
const http = require('http');
const crypto = require('crypto');
const { URL, URLSearchParams } = require('url');

const PORT = Number(process.env.DEV_IDP_PORT || 9399);
const ISSUER = `http://localhost:${PORT}`;
const CLIENT_ID = process.env.DEV_IDP_CLIENT_ID || 'procreate-dev';
const CLIENT_SECRET = process.env.DEV_IDP_CLIENT_SECRET || 'procreate-dev-secret';
const STAFF_GROUP = 'procreate-staff';

// Regenerated every start: a key that never touches disk cannot be mistaken
// for one worth protecting.
const { publicKey, privateKey } = crypto.generateKeyPairSync('rsa', { modulusLength: 2048 });
const jwk = { ...publicKey.export({ format: 'jwk' }), kid: 'dev-idp', alg: 'RS256', use: 'sig' };

/**
 * The seeded accounts, so the picker offers something that will actually
 * match a record. Kept in step with AppDbContext (staff) and DataSeeder
 * (patients) — an address that matches nothing is still reachable through
 * the "someone else" box, which is how the refusal paths get tested.
 */
const ACCOUNTS = [
  { email: 'admin@procreate.ai', name: 'System Administrator', note: 'Admin', staff: true },
  { email: 'cashier@procreate.ai', name: 'John Doe', note: 'Cashier', staff: true },
  { email: 'mlopez@procreate.ai', name: 'Dr. Maria Lopez', note: 'Doctor', staff: true },
  { email: 'maria.santos@example.com', name: 'Maria Santos', note: 'Patient, portal enabled', staff: false },
];

// code -> what was agreed at /authorize
const codes = new Map();

const b64 = (v) => Buffer.from(typeof v === 'string' ? v : JSON.stringify(v)).toString('base64url');
const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) =>
  ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

function idToken({ nonce, email, emailVerified, name, groups }) {
  const now = Math.floor(Date.now() / 1000);
  const header = { alg: 'RS256', typ: 'JWT', kid: jwk.kid };
  const payload = {
    iss: ISSUER,
    aud: CLIENT_ID,
    sub: crypto.createHash('sha256').update(email).digest('hex').slice(0, 32),
    iat: now,
    exp: now + 300,
    nonce,
    email,
    email_verified: emailVerified,
    name,
    groups,
  };
  const signing = `${b64(header)}.${b64(payload)}`;
  const sig = crypto.sign('RSA-SHA256', Buffer.from(signing), privateKey).toString('base64url');
  return `${signing}.${sig}`;
}

function readBody(req) {
  return new Promise((resolve) => {
    let data = '';
    req.on('data', (chunk) => (data += chunk));
    req.on('end', () => resolve(data));
  });
}

/**
 * The account picker. Where Authentik would show its own login screen — and,
 * with Google configured as a source, a "Sign in with Google" button.
 */
function pickerPage(params) {
  const hidden = ['redirect_uri', 'state', 'nonce', 'client_id']
    .map((k) => `<input type="hidden" name="${k}" value="${esc(params.get(k))}">`)
    .join('');

  const rows = ACCOUNTS.map((a) => `
    <button class="account" type="submit" name="email" value="${esc(a.email)}">
      <span class="account__name">${esc(a.name)}</span>
      <span class="account__meta">${esc(a.email)} &middot; ${esc(a.note)}</span>
    </button>`).join('');

  return `<!doctype html>
<html lang="en"><head><meta charset="utf-8">
<title>Development sign-in</title>
<style>
  :root { --ink:#2D1B0E; --muted:#8A7663; --line:#E4DBD2; --accent:#6F7D56; }
  * { box-sizing: border-box; }
  body { margin:0; min-height:100vh; display:flex; align-items:center; justify-content:center;
         background:#F7F4F1; font:15px/1.5 system-ui,-apple-system,Segoe UI,sans-serif; color:var(--ink); padding:32px; }
  .card { width:100%; max-width:440px; background:#fff; border:1px solid var(--line); border-radius:14px; padding:28px; }
  .warn { background:#FDF3E3; border:1px solid #E8C99A; border-radius:8px; padding:10px 12px;
          font-size:13px; color:#7A5A1E; margin-bottom:20px; }
  h1 { font-size:19px; margin:0 0 4px; font-weight:600; }
  .sub { margin:0 0 20px; color:var(--muted); font-size:13px; }
  .google { display:flex; align-items:center; justify-content:center; gap:10px; width:100%;
            padding:11px; border:1px solid var(--line); border-radius:8px; background:#fff;
            font:inherit; font-weight:500; cursor:pointer; margin-bottom:8px; }
  .google:hover { background:#FAF8F6; }
  .google svg { width:18px; height:18px; }
  .hint { margin:0 0 18px; font-size:12px; color:var(--muted); text-align:center; }
  .rule { display:flex; align-items:center; gap:12px; color:var(--muted); font-size:12px; margin:18px 0; }
  .rule::before, .rule::after { content:""; flex:1; height:1px; background:var(--line); }
  .account { display:block; width:100%; text-align:left; padding:11px 13px; margin-bottom:8px;
             border:1px solid var(--line); border-radius:8px; background:#fff; font:inherit; cursor:pointer; }
  .account:hover { border-color:var(--accent); background:#FAFBF7; }
  .account__name { display:block; font-weight:600; }
  .account__meta { display:block; font-size:12px; color:var(--muted); }
  fieldset { border:1px solid var(--line); border-radius:8px; padding:14px; margin:0; }
  legend { font-size:12px; color:var(--muted); padding:0 6px; }
  input[type=email] { width:100%; padding:9px 11px; border:1px solid var(--line); border-radius:7px;
                      font:inherit; margin-bottom:10px; }
  label.check { display:flex; align-items:center; gap:8px; font-size:13px; color:var(--muted); margin-bottom:8px; }
  .submit { width:100%; padding:10px; border:0; border-radius:8px; background:var(--accent);
            color:#fff; font:inherit; font-weight:600; cursor:pointer; }
</style></head>
<body>
  <form class="card" method="post" action="/pick">
    ${hidden}
    <div class="warn"><strong>Development sign-in.</strong> This is a stub standing in for
      Authentik. It does not check any password &mdash; pick an account and you are signed in.</div>

    <h1>Sign in to Pro-Create</h1>
    <p class="sub">Fertility and OB-GYN Clinic</p>

    <button class="google" type="submit" name="email" value="${esc(ACCOUNTS[0].email)}">
      <svg viewBox="0 0 48 48" aria-hidden="true"><path fill="#EA4335" d="M24 9.5c3.5 0 6.6 1.2 9 3.6l6.7-6.7C35.6 2.6 30.2 0 24 0 14.6 0 6.5 5.4 2.6 13.2l7.8 6.1C12.3 13.2 17.7 9.5 24 9.5z"/><path fill="#4285F4" d="M46.1 24.6c0-1.6-.1-3.2-.4-4.6H24v9.1h12.4c-.5 2.9-2.2 5.3-4.7 7l7.6 5.9c4.4-4.1 6.8-10.1 6.8-17.4z"/><path fill="#FBBC05" d="M10.4 28.7c-.5-1.5-.8-3-.8-4.7s.3-3.2.8-4.7l-7.8-6.1C1 16.4 0 20.1 0 24s1 7.6 2.6 10.8l7.8-6.1z"/><path fill="#34A853" d="M24 48c6.5 0 11.9-2.1 15.9-5.8l-7.6-5.9c-2.1 1.4-4.8 2.3-8.3 2.3-6.3 0-11.7-3.7-13.6-9.8l-7.8 6.1C6.5 42.6 14.6 48 24 48z"/></svg>
      Sign in with Google
    </button>
    <p class="hint">In production this is Authentik's own button, and Google is the source behind it.
      Here it just signs in the first account.</p>

    <div class="rule">or pick a seeded account</div>
    ${rows}

    <div class="rule">or</div>

    <fieldset>
      <legend>Someone else</legend>
      <input type="email" name="email" placeholder="stranger@example.com">
      <label class="check"><input type="checkbox" name="unverified" value="1">
        Send the address as unverified</label>
      <label class="check"><input type="checkbox" name="nostaff" value="1">
        Leave out the <code>${esc(STAFF_GROUP)}</code> group</label>
      <button class="submit" type="submit">Sign in</button>
    </fieldset>
  </form>
</body></html>`;
}

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, ISSUER);
  const json = (body, status = 200) => {
    res.writeHead(status, { 'content-type': 'application/json' });
    res.end(JSON.stringify(body));
  };

  if (url.pathname === '/.well-known/openid-configuration') {
    return json({
      issuer: ISSUER,
      authorization_endpoint: `${ISSUER}/authorize`,
      token_endpoint: `${ISSUER}/token`,
      jwks_uri: `${ISSUER}/jwks`,
      response_types_supported: ['code'],
      subject_types_supported: ['public'],
      id_token_signing_alg_values_supported: ['RS256'],
      scopes_supported: ['openid', 'email', 'profile'],
      code_challenge_methods_supported: ['S256'],
    });
  }

  if (url.pathname === '/jwks') return json({ keys: [jwk] });

  if (url.pathname === '/authorize') {
    // The PKCE challenge is held here rather than round-tripped through the
    // form, so the page cannot be edited to defeat it.
    const state = url.searchParams.get('state');
    codes.set(`pending:${state}`, {
      challenge: url.searchParams.get('code_challenge'),
      clientId: url.searchParams.get('client_id'),
    });
    res.writeHead(200, { 'content-type': 'text/html; charset=utf-8' });
    return res.end(pickerPage(url.searchParams));
  }

  if (url.pathname === '/pick' && req.method === 'POST') {
    const form = new URLSearchParams(await readBody(req));
    // The picker posts one "email" per control; the last non-empty wins, so
    // the free-text box beats the account buttons when it is filled in.
    const email = form.getAll('email').filter(Boolean).pop();
    const state = form.get('state');
    const pending = codes.get(`pending:${state}`);
    codes.delete(`pending:${state}`);

    if (!email || !pending) {
      res.writeHead(400, { 'content-type': 'text/plain' });
      return res.end('Pick an account or type an address.');
    }

    const known = ACCOUNTS.find((a) => a.email === email);
    const code = crypto.randomBytes(16).toString('hex');
    codes.set(code, {
      challenge: pending.challenge,
      nonce: form.get('nonce'),
      email,
      emailVerified: !form.get('unverified'),
      name: known?.name ?? email,
      groups: form.get('nostaff') ? [] : [STAFF_GROUP],
    });

    const back = new URL(form.get('redirect_uri'));
    back.searchParams.set('code', code);
    back.searchParams.set('state', state);
    res.writeHead(302, { location: back.toString() });
    return res.end();
  }

  if (url.pathname === '/token' && req.method === 'POST') {
    const form = new URLSearchParams(await readBody(req));
    const entry = codes.get(form.get('code'));
    if (!entry) return json({ error: 'invalid_grant' }, 400);
    codes.delete(form.get('code'));

    if (form.get('client_id') !== CLIENT_ID || form.get('client_secret') !== CLIENT_SECRET)
      return json({ error: 'invalid_client' }, 401);

    // Checked for real: getting PKCE wrong in the API should fail here, not
    // quietly pass because the stub was lenient.
    const computed = crypto
      .createHash('sha256')
      .update(form.get('code_verifier') || '')
      .digest('base64url');
    if (computed !== entry.challenge)
      return json({ error: 'invalid_grant', error_description: 'PKCE verification failed' }, 400);

    return json({
      access_token: 'dev-access-token',
      token_type: 'Bearer',
      expires_in: 300,
      id_token: idToken(entry),
    });
  }

  json({ error: 'not_found' }, 404);
});

server.listen(PORT, () => {
  console.log(`Development identity provider on ${ISSUER}`);
  console.log(`  discovery  ${ISSUER}/.well-known/openid-configuration`);
  console.log(`  client id  ${CLIENT_ID}`);
  console.log('  NOT FOR PRODUCTION - it signs in whoever is clicked.');
});
