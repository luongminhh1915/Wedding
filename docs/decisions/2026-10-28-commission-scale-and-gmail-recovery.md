# Commission precision and Gmail recovery — 28/10/2026

Status: **CONFIRMED latest no-new-rounding instruction and Gmail recovery choice**.

Source: user answered the continuation questions in this chat, then requested continuing implementation.

- **Historical answer, limited/superseded:** user first selected two decimal places rather than whole VND. When asked about halfway handling, the latest reply was “cứ giữ y nguyên đi, ko cần làm tròn”. Do not implement the offered midpoint rule or infer truncation to force two digits.
- **CONFIRMED latest instruction:** preserve current calculations/data, no new rounding/truncation step and no retrospective rewriting of original snapshots, payments, commissions or statements. This does not authorize a new runtime calculator/storage policy/rollout. Mixed-combo commission allocation, same-level Service/Combo conflicts, unsettled allocation and manual reconciliation remain under existing decisions. If a future engine cannot represent an exact result in its storage, fail closed/report that dependency rather than silently rounding.
- **CONFIRMED:** use Wedding Match's Gmail account for password-recovery email. Owner supplies the actual Gmail account/App Password through local prompts only. No secret in chat/Git/docs/logs. Use the existing SMTP delivery/reset-token workflow, not a parallel recovery system or Gmail inbox-reading integration.
- Gmail setup/preflight tools may prepare a separate private configuration copy while retaining SQL/JWT/Google/Facebook/uploads/unknown settings. This decision is not proof SMTP credentials are present, a message was delivered, or host configuration was deployed.

Earlier confirmed ordinary/A–Z basis principles, old Vendor/original contract preservation and three activation conditions remain in force. See [basis/activation](2026-10-19-commission-basis-and-activation.md) and [Phase5–7 guardrails](2026-10-21-phase5-7-financial-workflow-guards.md).
