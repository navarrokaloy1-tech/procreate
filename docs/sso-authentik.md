# Single sign-on with Authentik (Google behind it)

Staff and patients sign in through an OpenID Connect provider instead of a QR
code. The API is written against plain OIDC, so any provider works; this is
how to set up the one the clinic runs.

**Why Authentik rather than talking to Google directly:** Authentik brokers
Google as a *source*, so people still click "Sign in with Google", but
enrolment, group membership, MFA and policy stay in one place we control —
and swapping or adding a second source later is a change in Authentik, not in
this codebase.

## Cost

Everything below is free.

- **Authentik** — open source, self-hosted. The free build covers OIDC
  providers, Google as a login source, groups, policies and MFA. (The paid
  Enterprise tier is support and a few admin features; nothing used here.)
- **Google as a login source** — a Google Cloud OAuth client costs nothing,
  and there is no per-sign-in charge. Note that **Google Workspace** (the paid
  ~$7/user/month business suite) is a *different product*. "Sign in with
  Google" works with ordinary Gmail accounts and does not require it.

The only real cost is somewhere to run Authentik.

## Trying it first, without any of this

There is a stub provider in the repository that stands in for Authentik, so
the whole flow runs locally with no Docker and no accounts:

```bash
node tools/dev-idp/server.js
```

`appsettings.Development.json` already points at it. See
[../tools/dev-idp/README.md](../tools/dev-idp/README.md). Nothing in the API
is specific to it — moving to the real Authentik is a change to three
settings.

## 1. Run Authentik

```bash
wget https://goauthentik.io/docker-compose.yml
echo "PG_PASS=$(openssl rand -base64 36 | tr -d '\n')" >> .env
echo "AUTHENTIK_SECRET_KEY=$(openssl rand -base64 60 | tr -d '\n')" >> .env
docker compose up -d
```

Finish setup at `http://localhost:9000/if/flow/initial-setup/`.

## 2. Add Google as a source

In Google Cloud Console, create an **OAuth 2.0 Client ID** of type *Web
application* with the authorised redirect URI Authentik shows you (it is
`https://<authentik>/source/oauth/callback/google/`).

In Authentik: **Directory → Federation & Social login → Create → Google OAuth
Source**. Paste the client ID and secret. Set the slug to `google`.

To have it offered on the login page, add the source to the default
authentication flow's *Identification* stage.

## 3. Create the application and provider

**Applications → Create with provider**:

| Field | Value |
| --- | --- |
| Name | Pro-Create |
| Slug | `procreate` |
| Provider type | OAuth2 / OpenID |
| Client type | Confidential |
| Redirect URI | `http://localhost:5230/api/auth/sso/callback` |
| Scopes | `openid`, `email`, `profile` |
| Subject mode | Based on the user's email |

Copy the **Client ID** and **Client Secret**.

The issuer URL is shown on the provider page and looks like
`http://localhost:9000/application/o/procreate/` — that is what goes in
`Authority`. The API reads `{Authority}/.well-known/openid-configuration` to
find everything else.

### Optional: restrict the console to staff

Create a group (say `procreate-staff`), add the clinic's accounts to it, and
add a **Group Membership** policy binding on the application. Then set
`Sso:StaffGroup` to the group name so the API refuses a console session for
anyone outside it even if the provider let them through. This needs the
`groups` claim, which Authentik emits when the provider's scope mapping
includes it.

## 4. Point the API at it

`procreate-api/appsettings.json`:

```json
"Sso": {
  "Enabled": true,
  "Authority": "http://localhost:9000/application/o/procreate/",
  "ClientId": "<from Authentik>",
  "ClientSecret": "<from Authentik>",
  "Scopes": "openid email profile",
  "CallbackUrl": "http://localhost:5230/api/auth/sso/callback",
  "AppBaseUrl": "http://localhost:4200",
  "DisplayName": "SSO",
  "StaffGroup": ""
}
```

Keep the secret out of the repository in anything but a local demo:

```bash
dotnet user-secrets set "Sso:ClientSecret" "<from Authentik>"
```

`CallbackUrl` must match what is registered in Authentik character for
character. `AppBaseUrl` is where the browser is sent once a session exists, so
it points at the Angular app, not the API.

`DisplayName` is what the button says: "Log in with **SSO**". Naming the
provider instead ("Authentik", "Google") works too, but then the button has to
be re-explained to staff whenever the provider behind it changes.

With `Enabled` false — or any of the four required values blank — the API
reports SSO as off, the sign-in screens hide the button, and password sign-in
carries on unchanged.

## How a sign-in runs

1. The browser leaves for `GET /api/auth/sso/start?audience=staff|patient`.
2. The API redirects to Authentik with PKCE, `state` and a nonce, all held
   server side.
3. Authentik authenticates the person, via Google or otherwise.
4. Authentik returns to `GET /api/auth/sso/callback`. The API exchanges the
   code for an ID token, validates signature, issuer, audience, lifetime and
   nonce, and reads the verified email address.
5. The email is matched to a `User` (staff) or a portal-enabled `Patient`.
   **No account is created here** — the provider proves identity, but who gets
   in is still an administrator's decision.
6. The API redirects to `/auth/callback?ticket=…`; the app swaps the one-time
   ticket for the ordinary session token over a POST, so no token is ever left
   in a URL.

## Matching accounts

Staff are matched on `User.Email`; patients on `Patient.Email` with the portal
already enabled. An address the provider will not mark verified is refused.

Two consequences worth knowing:

- A staff account with a blank email cannot sign in with SSO. Fill the email
  in on the account, or use the password fallback.
- A patient who has never set up portal access cannot sign in with SSO either;
  they self-register first. This is deliberate — a matching email address on
  its own is not enough to hand over someone's chart, and clinic-entered
  addresses are typo-prone.

## What the QR is now

The patient card QR is no longer a credential. Every patient still gets one at
registration, and `GET /api/patients/by-code/{code}` accepts it — so doctors,
med-techs, nurses and the cashier scan it to pull up the right file for tests,
labs and billing, exactly as before.
