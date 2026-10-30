# SmarterASP trial deployment decision — 2026-10-22

## Confirmed follow-up — 2026-10-22

- User confirmed a **fresh hosted database**, with **no copy** of local customer/vendor data. This supersedes the unresolved database choice below.
- User provided distinct contact details for SuperAdmin, Moderator, and Finance accounts in chat and confirmed the Admin email domain correction. These details are intentionally not duplicated in this repository. Account creation is pending DB setup and secure password entry by the user; no accounts have been created.
- Public DNS/HTTPS verification, persistent uploads, backup/restore procedure, trial resource limits, and the separate SMTP provider needed for password recovery remain unverified.

Status: **CONFIRMED** for initial deployment/demo only. Source: user explicitly chose “Dùng trial 60 ngày trước” after the SmarterASP panel showed plan `W60-US` / “Expires in 60 days”.

## Decision

- Proceed using the existing SmarterASP 60-day trial for the initial demo and deployment work.
- This is not approval to purchase/upgrade a paid plan or incur another charge; payment remains user-controlled.
- Keep .NET 10 + React same-origin + SQL Server. Production startup must keep demo seeding and automatic migrations disabled.
- Do not use default/demo passwords. Bootstrap the first SuperAdmin interactively with `tools/Wedding.DeployAdmin`; create Moderator and Finance from the SuperAdmin user-management screen. Passwords are entered by the account owner in the local terminal/HTTPS site and never stored in Git, docs, or chat.

## Provider limits observed / remaining

- Control Panel showed plan `W60-US`, due in 60 days; one active website (`site1`) and quota for 0/1 MSSQL database with 1000 MB. `weddingmatch.website` is mapped to the site but its public DNS and custom-domain HTTPS are not verified.
- SmarterASP says the 60-day trial does not include SMTP; password-reset email therefore needs a separate SMTP provider or a user-approved paid upgrade. The provider plan comparison does not show automatic backups for trial; confirm backup availability and keep a manual backup outside the host.
- Do not collect real customer personal data until production HTTPS, SQL persistence, backup/restore, privacy notice, and required integrations have been checked. Confirm export/retention before trial expiry.

## Not decided at initial decision time — see confirmed follow-up above

- Whether to start with a fresh hosted SQL database or transfer existing local SQL Server data. No customer/vendor data may be copied to SmarterASP until the user explicitly confirms that transfer and a backup/verification plan is agreed.
- Actual full name, reachable email, and phone for each staff account. User selected separate emails but has not supplied values; do not invent mailbox addresses or passwords.
- Registrar DNS method. Prefer adding only A records for `@` and `www` to the host IP if preserving existing DNS/email records is necessary; switching nameservers can replace current DNS records and needs confirmation if email or other services exist.

## Provider references

- [SmarterASP 60-day free trial](https://www.smarterasp.net/free_trial)
- [SmarterASP FAQ: trial, SMTP, test URL and DNS](https://www.smarterasp.net/FAQ/)
- [SmarterASP hosting plans and limits](https://www.smarterasp.net/hosting_plans?r=hostingcharges)
- [SmarterASP ASP.NET Core hosting: .NET 10 and MSSQL support](https://www.smarterasp.net/asp.net_core_hosting/)
