# Gmail password-recovery preparation — 28/10/2026

## Current state

User selected Wedding Match Gmail for recovery and then requested step-by-step setup. Existing SMTP/password-reset API, secure reset tokens and UI are reused. **Owner Gmail App Password still required; no real private output, SMTP message, provider/account security change, SQL connection or host deployment performed.**

User also clarified “cứ giữ y nguyên đi, ko cần làm tròn”. No new commission rounding/truncation or runtime calculation was implemented. [Decision](../decisions/2026-10-28-commission-scale-and-gmail-recovery.md) retains both the earlier precision answer and this latest clarification.

## Code

- New `tools/Set-StudyEmailConfiguration.ps1`: simple Gmail-specific CLI over the existing guarded configuration-copy writer. Default reset origin is the user's public Wedding Match HTTPS domain; an explicit alternate origin must match existing private AllowedHosts.
- Extended existing untracked `tools/Set-StudySocialConfiguration.ps1` with optional ConfigureGmail/address/reset-origin fields. Existing Google/Facebook callers remain compatible. Same private JSON path/reparse/startup/duplicate-key/no-overwrite/allowed-change verification is reused.
- Gmail SMTP uses smtp.gmail.com/587; Username and From are the owner-entered Gmail address. App Password is collected only as SecureString, never as a command argument/environment variable. Google grouped spaces are normalized; invalid length/characters rejected without echo. BSTR zeroed and managed secret references cleared on completion/error. Existing SQL/JWT/Google/Facebook/uploads/unknown JSON fields and original input remain preserved.
- SMTP Port is stored as string587, which the existing IConfiguration.GetValue<int> binding supports. TLS remains enforced by existing SmtpDelivery.EnableSsl=true; no TLS bypass or ordinary Google-account password handling added.
- New `tools/Test-StudyEmailConfiguration.ps1`: synthetic local copy tests; fake configs retained only in ignored unique `.deployment/private/EmailConfigQa_<GUID>` folders and results in ignored artifacts. No real secret/config was used.

## Verification

- Bundled PowerShell7 `-NoProfile -File tools/Test-StudyEmailConfiguration.ps1`: **24 checks PASS**,0failed, no SMTP/SQL/network. Covers hidden prompt/normalization, sender/account equality, reset origin validation and existing-host match, SQL/JWT/social/uploads/precise unknown JSON preservation, refusal to overwrite/escape/private same file, startup migration/ambiguous-key guards, invalid App Passwords and adding missing Auth/Smtp sections.
- Existing `artifacts/facebook-setup-20261027/social-config-smoke.ps1`: **17 checks PASS**, confirms old social helper compatibility/private guards.
- First new test attempt failed because its generic rejected-output assertion incorrectly expected the existing input file to disappear in the same-file refusal case. Corrected the test, preserved input, reran successfully. No production file was involved.
- PowerShell AST parse of all3scripts: **PASS**. Git diff/check clean apart from existing line-ending notices. No frontend or backend source changed in this continuation, so FE486/API build/backend1183 results remain earlier evidence, not newly rerun.

## Owner steps / exact continuation

Google requires2-Step Verification before App Passwords. Official instructions: [App Passwords](https://support.google.com/accounts/answer/185833), [2-Step Verification](https://support.google.com/accounts/answer/185839). Owner must sign into the intended Wedding Match Gmail and personally complete password/OTP/security changes/App Password creation; agent must not read/capture the generated secret.

Browser tab opened to App Passwords initially selected a different saved account. Agent navigated only through Switch account → Use another account; owner is at Google sign-in for the intended Gmail. No identifier/password/OTP was entered by agent and no App Password was created. Tab is marked for handoff. Never copy authenticated Google URL query parameters/account addresses/secrets into this report.

Once owner creates the App Password, from repository root run:

```powershell
& 'C:\Users\Admin\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe' -NoProfile -File .\tools\Set-StudyEmailConfiguration.ps1 -InputPath .\.deployment\private\profile-only-release-20261028\wedding.production.before.json -OutputPath .\.deployment\private\gmail-recovery-20261028\wedding.production.json
```

Input existence verified only; it is the ignored backup of the active config before the previous approved schema20 deployment, where provider/SQL settings remained unchanged. Output did not exist at handoff. Tool prompts for sender Gmail then the App Password invisibly. Do not paste either private JSON or secret into chat. Before applying on host, compare with the current active config and preserve any intervening settings; only SMTP/Auth intended changes, no schema migration needed. Live delivery and owner password-reset flow remain **UNVERIFIED** until actually tested. Existing hosted Google/Facebook/customer/financial data are untouched.
