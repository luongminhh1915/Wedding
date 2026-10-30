# CONFIRMED — Phase 5–7 implementation guardrails

Date: 2026-10-21, Asia/Ho_Chi_Minh. Source: user authorized continuing Phase 5–7 and confirmed the choices below in this conversation.

## Confirmed decisions

1. **Scope and order:** implement backend/domain functionality for Phase 5 Commission, Phase 6 Negotiation, then Phase 7 Platform–Vendor Partnership Contracts. Defer interface/UI work. Existing Customer–Vendor `BookingContract` and content-only `PrivateServiceAgreement` remain separate concepts.
2. **Conflicting commission rules:** reject activation when rules for the same exact scope/target have overlapping effective periods. Do not resolve conflicts by last-write-wins or implicit version order.
3. **Combo commission basis:** fail closed until every component has its own approved commission basis and rate. Do not infer a common parent rate or derive commission allocation from customer discount shares.
4. **Completed contract eligibility:** an adjustment request may include a Completed contract only when at least one applicable commission obligation has `OutstandingAmount > 0`. This includes unpaid/partially paid obligations already assigned to a settlement, or in a Closed/Overdue statement. Paid/settled or cancelled zero-outstanding obligations alone are not eligible.
5. **Blocked financial states:** do not approve or activate an adjustment while the affected contract/settlement is disputed, or the affected commission is Frozen or EscrowLocked. Resolve the hold first; do not bypass existing dispute, freeze, or escrow controls.
6. **Settlement differences:** adjustment approval records an append-only decision/delta only. Do not automatically offset a future settlement, refund money, or rewrite Commission, CommissionSettlement, PaidAmount, payment references, webhook history, or statements. Any manual reconciliation remains a distinct Finance-controlled action with evidence.

## Existing confirmed basis and activation rules remain in force

- Ordinary service commission base is value before VAT, after valid Vendor-funded discounts, less only documented pass-through expenses verified/approved by Admin. Platform-funded discount does not reduce the base when the Vendor is paid in full.
- A–Z commission uses only the Admin-approved organization/management/coordination fee; never the full wedding budget or all-in contract amount by default.
- Combo parent and children must never be charged twice. New Combo contracts fail closed without per-component basis/rate approval.
- A new policy is effective only when Admin approval, its effective date, and the related externally signed agreement/appendix are all present. Approval, conversation, viewing or acknowledgement alone does not activate it.
- Existing confirmed contract snapshots and legacy Vendor rates remain unchanged; no guessed backfill.

This decision does not authorize applying migrations to an application/production database, changing historical financial data, or starting deployment. Additive schema and verification must be reported separately from application/activation.
