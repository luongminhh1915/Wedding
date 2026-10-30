# Gmail recovery delivery diagnosis — 29/10/2026

**Resolved later29/10 01:00VN:** replacement owner-entered credential authenticated235, corrected private host configuration was applied/recycled, and owner confirmed receipt of one newly authorized recovery email. The535 finding below applies to the old credential. Historical diagnostic/continuation instructions are not current next tasks. See [corrective rollout](gmail-recovery-corrected-2026-10-29.md); do not recreate or retry the old secret. Full owner password-reset confirmation remains UNVERIFIED.

## Finding and scope

Owner reports no test email received after [configuration rollout](gmail-host-configuration-2026-10-28.md). **Current SMTP credentials are rejected by Gmail; recovery is BLOCKED.** Public `emailOtp/passwordRecovery=true` only means configuration keys are loaded, not that SMTP authenticated or mail arrived.

No second email/reset request, user password change, host mutation/recycle, application release, migration or direct SQL write was performed in this diagnosis. Existing source/untracked work is preserved. No App Password, reset link/token/hash, user identifier or credential hash was printed or placed in shared docs.

## Fresh evidence

- Git before-state unchanged: `weddy-journey`, HEAD `3596ae832faeac95fb15cc44bc0e8e62351c788b`,173tracked changes/190normal untracked entries(196expanded)/0staged/1worktree; diff-check0. Latest handoff, report and actual recovery/SMTP source re-read.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File artifacts/gmail-delivery-diagnostics-20261029/read-reset-metadata.ps1` executed only two parameterized aggregate SELECTs against the exact configured hosted server/database, encryption retained. Result at `2026-10-28T17:17:46.8556617Z`: one matching active SuperAdmin; exactly one reset row in the original request window, one used/revoked, zero unused/currently usable. The user exists and the request reached persistence. UsedAt is also used for consumption/supersession and cleanup errors can be swallowed; this is supporting lifecycle evidence, not a dedicated SMTP status or inbox-delivery proof.
- First metadata runner attempt stopped before connection with an ArgumentException because PowerShell dictionary adaptation treated builder property assignment as an unsupported connection-string key. Changed the ignored runner to explicit typed setters/getters; rerun succeeded. No production configuration was changed.
- Bundled Python ran `artifacts/gmail-delivery-diagnostics-20261029/check-gmail-login.py`. One bounded, mail-free explicit **AUTH LOGIN** attempt after certificate-validated STARTTLS on Gmail587 returned **535**, enhanced status **5.7.8**, at `2026-10-28T17:18:37.834850Z`. TLSv1.3 and advertised LOGIN mechanism observed. No SMTP debug transcript/credential/error-text payload output. This establishes rejection of the current pair without diagnosing whether the wrong Gmail, incorrect App Password or a revoked credential caused it. It is a local authentication probe, not hosted egress verification.
- Previous `smtplib.login()` probes disconnected without a status. The explicit challenge/response diagnostic supplied new evidence; no further attempts should use the rejected credentials.

Sanitized evidence/scripts are ignored under `artifacts/gmail-delivery-diagnostics-20261029/`; secrets are read internally only from ignored host-ready private JSON and never printed. No source feature changed, so frontend/backend suites were not rerun for this diagnostic task.

## Relevant verified primary documentation

- [Google SMTP errors](https://support.google.com/mail/answer/3726730?hl=en):535 authentication failure; do not treat it as a successful send or merely Spam delay.
- [Google App Passwords](https://support.google.com/accounts/answer/185833?hl=en): App Passwords require2-Step Verification and are revoked after the Google account password changes. The owner must verify the account and supply credentials directly through hidden local input.
- [Microsoft SmtpClient.EnableSsl](https://learn.microsoft.com/en-us/dotnet/api/system.net.mail.smtpclient.enablessl?view=net-10.0): current SmtpClient uses STARTTLS; implicit TLS465 is unsupported. Do not fix rejection by changing only the port or disabling certificate checks.
- SmarterASP [trial FAQ](https://www.smarterasp.net/faq) excludes its SMTP service, while its [outgoing ports page](https://www.smarterasp.net/support/kb/a2044/how-to-enable-outgoing-port-from-your-hosting-control-panel.aspx) lists465/587 enabled by default. This does not prove external Gmail is blocked on this particular account. Do not blame hosting without host evidence.

## Exact continuation

1. **Owner has confirmed the App Password was created under the intended Gmail.** Do not assume an account mismatch; the observed pair is still rejected. Owner must supply a valid replacement through local hidden input.
2. Owner creates/copies a valid App Password in the intended Gmail account and enters it only into the existing helper's hidden prompt, using a **new output path** so prior configs remain preserved. Suggested corrected output: `.deployment/private/gmail-recovery-20261029-corrected/wedding.production.json`; input is the latest known uploaded config `.deployment/private/gmail-recovery-20261028/host-apply/wedding.production.remote-after.json`. Always re-check active remote config again before applying.
3. Validate the corrected private pair with one mail-free, certificate-validated AUTH attempt before another host update. Stop on explicit auth/temporary failure; do not brute-force or repeatedly submit the same secret.
4. Only after credential acceptance, back up fresh active remote config, merge approved SMTP/reset keys preserving all other values, upload private-only, recycle existing app and verify flags/HTTPS. No code release/migration required.
5. Sending another real test message requires owner authorization for that retry; the earlier approval covered the one completed request. Inbox receipt and any full reset remain owner verified, with no password entered/reset by the agent.

Do not upload private files inside `/site1`, automatically revoke/regenerate Google credentials, bypass TLS, send secrets into chat, change finance/contracts or reset/reseed any database. Current host config remains as last applied; no silent rollback or new egress/hosting changes were performed.

Owner command (checked input exists, new output absent; original files remain preserved):

```powershell
& 'C:\Users\Admin\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe' -NoProfile -File .\tools\Set-StudyEmailConfiguration.ps1 -InputPath .\.deployment\private\gmail-recovery-20261028\host-apply\wedding.production.remote-after.json -OutputPath .\.deployment\private\gmail-recovery-20261029-corrected\wedding.production.json -GmailAddress weddingmatch.official123@gmail.com
```

After owner reports success, the next agent may use the ignored mail-free diagnostic explicitly against the corrected file. The diagnostic now requires `--config` rather than defaulting to the rejected config; do not run it until the new file exists:

```powershell
& 'C:\Users\Admin\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' .\artifacts\gmail-delivery-diagnostics-20261029\check-gmail-login.py --config .deployment/private/gmail-recovery-20261029-corrected/wedding.production.json
```

Final docs check:5files/144local links,0broken, diff-check0. Git173tracked changed/191normal untracked entries(197expanded)/0staged/1worktree, branch/HEAD unchanged. New report is untracked and must be preserved. No active SQL/SMTP/API/helper process remains. Source/build suites were not rerun for this diagnostic/docs-only work.
