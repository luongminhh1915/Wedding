# Social sign-in without mandatory contact email — 27/10/2026

Status: **CONFIRMED user scope; IMPLEMENTED and DEPLOYED28/10; live integration VERIFIED, actual owner OAuth E2E UNVERIFIED**.

Deployment authorization28/10: owner explicitly approved optional-contact schema20 on `db_ad0009_weddingmatch` and deployment of the verified matching release, preserving existing email/password/data. This supersedes earlier pending-approval checkpoints; production actions must still use the exact reviewed/scoped artifact and verified target/backup.

Source: the user supplied the live Facebook missing-email rejection and requested Facebook blue styling, then explicitly said Google/Facebook should sign in without email permission, account-email linking or an extra application confirmation step. Provider-owned first-use account selection/consent cannot be disabled by Wedding Match.

## Behavior

- Facebook requests only public_profile and retrieves only id/name; a valid app-bound, non-expired token and matching provider subject remain mandatory. No email permission or fabricated contact address.
- A verified provider subject identifies the Customer. Existing subject logins return the existing User ID/data. First use can create a Customer without contact email and continue through existing onboarding.
- A matching email alone never transfers another account's data. Google retains verified-email requirements of its ID token; when that email belongs to a different Customer without this subject, create an independent subject account with no duplicate contact email rather than blocking on manual linking. Existing staff/vendor email collision remains password-only and no social endpoint grants internal roles.
- This independent-account behavior is an implementation safety choice, **not authorization to merge accounts or financial histories**. User-requested account consolidation, if later needed, requires a separately verified ownership workflow. The normal sign-in path does not require the optional account-settings linking UI.
- Optional Facebook linking by an already authenticated Customer proves control of both the current session and the provider identity; it no longer requires a Facebook email claim. A subject already belonging to another account is rejected. Legacy supplied mismatching email remains rejected. Existing provider bindings are not replaced.
- Contact email can be absent only for a Customer with a provider identity. Password/email registration and staff provisioning still require email. Existing email values, password hashes, roles and business/financial data remain unchanged.
- API email remains an empty string when no contact exists; persistence uses NULL and a filtered unique index. JWT omits an absent email claim. No fake email, SMTP delivery or password-reset destination is invented. Notifications remain private InApp for users without email.

## Schema/deployment boundary

Migration `20261027152038_OptionalSocialContactEmail` makes Users.Email nullable, rebuilds its unique index filtered to non-null values, and adds CK_Users_SocialEmail. No existing row is updated/deleted. Down refuses while email-less accounts exist. After scoped unique-local SQL preservation/restore tests and explicit authorization, schema20 and matching exact gated code deployed together28/10. Existing row-count and identity-field aggregates preserved; maintenance ended; live readiness/assets/button/negative-auth checks passed. Do not repeat upgrade or downgrade to19 once email-less accounts may exist. See [deployment evidence/remaining provider checks](../reports/social-login-without-email-2026-10-27.md).

This supersedes the mandatory Facebook email-permission/claim/manual-link gate in older authentication notes. It does not change finance, commission, contract snapshots, provider token validation, staff roles or provider platform consent.
