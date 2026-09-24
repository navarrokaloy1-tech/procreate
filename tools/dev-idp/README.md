# Development identity provider

A stub that stands in for Authentik so single sign-on can be run locally
without Docker, a Google Cloud project, or any account at all.

```bash
node tools/dev-idp/server.js
```

No install step and no dependencies — just Node.

Then start the API and the app as usual. `appsettings.Development.json`
already points at it, so **Log in with SSO** appears on both the staff and
patient sign-in screens.

## What it is honest about

It speaks real OpenID Connect: discovery document, JWKS, authorization code
with PKCE (verified — a broken verifier is rejected), and an RS256-signed ID
token the API validates for signature, issuer, audience, lifetime and nonce.
The API runs exactly the code path it will run against Authentik.

## What it fakes

There is no password. The picker signs you in as whoever you click.

The signing key is generated fresh on every start and never written to disk,
so a token cannot outlive the process.

**Do not run this anywhere but a developer's machine.** It is wired up in
`appsettings.Development.json` only; `appsettings.json` ships with SSO off.

## What you can try

The picker offers the seeded accounts — `admin`, `cashier`, `Dr. Maria Lopez`,
and patient `Maria Santos` — plus a free-text box with two switches for
exercising the refusal paths:

| Try | What should happen |
| --- | --- |
| `admin@procreate.ai`, staff sign-in | Lands on the dashboard |
| `cashier@procreate.ai`, staff sign-in | Lands on Patient Orders |
| `maria.santos@example.com`, patient sign-in | Lands on the portal |
| An address with no record | "That account is not set up for staff access here." |
| "Send the address as unverified" | "That account has no confirmed email address." |
| "Leave out the procreate-staff group" | Refused on the staff screen |
| `juan.delacruz@example.com`, patient sign-in | Refused — registered, but portal never enabled |

The **Sign in with Google** button on the picker is cosmetic. It is there to
show where Google sits in the real arrangement: our app never shows a Google
button, Authentik does, because Google is a *source* behind Authentik.

## Moving to the real thing

Nothing in the API is specific to this stub. Follow
[../../docs/sso-authentik.md](../../docs/sso-authentik.md), then change
`Authority`, `ClientId` and `ClientSecret`. That is the whole migration.
