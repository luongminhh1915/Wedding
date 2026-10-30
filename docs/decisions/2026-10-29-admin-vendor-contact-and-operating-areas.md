# Admin-created Vendors: optional contacts and operating areas — 29/10/2026

Status: **CONFIRMED** for the requested behavior; implementation details below are technical choices.

Source: owner asks Admin-created Vendors to require only a name and password, make email/phone optional, and replace the single operating-province choice with nationwide or multiple areas.

## Confirmed scope

- Only the authenticated active SuperAdmin provisioning path may create a VendorOwner without email/phone. Other roles and public customer/vendor registration keep their existing contact requirements.
- A supplied email/phone must still be valid. Do not invent contact details, fake email addresses or claimed phone verification.
- A Vendor may state nationwide coverage or select multiple canonical Vietnamese provinces. Missing coverage means not updated, never implicitly nationwide. Existing physical city/address and old data remain intact.
- Account creation does not verify a shop, accept its cooperation terms, approve public services/combos or activate a financial policy.

## Implementation choices

- Generate a stable unique login name from the account name, return it to Admin, and allow it in the existing password-login identifier field. Display names need not be unique. No extra required username entry is added. A generated login name is a credential identifier, not proof of verified contact ownership.
- Store an optional unique User.LoginName and a separate Vendor operating-area value. Existing accounts retain their email sign-in; existing Vendors with no area value fall back to their saved city. Service-specific area restrictions remain authoritative for that service.
- Brand name may default to the supplied name. An optional brand/contact/area can be completed later. The administrator's session is never replaced by the new account.
- Password recovery through email still requires an actual usable email; this change does not add an insecure recovery bypass or change an existing password.

This narrowly supersedes the mandatory Vendor contact/brand/location provisioning details in [the earlier all-role provisioning decision](2026-10-29-admin-create-all-user-roles.md). Its authorization, atomicity, audit, no-fabricated-consent and SuperAdmin-confirmation rules remain in force. No finance/contract snapshot rule is changed. Schema rollout requires reviewed additive SQL and isolated migration tests; creating this decision is not authorization to migrate a real database.

## Rollout authorization and status — 30/10/2026

**CONFIRMED:** owner explicitly answered “Đồng ý cập nhật DB và deploy” to the scoped migration21 (`LoginName`, `OperatingRegions`, related constraints) and final Studio release on the existing hosted database/site. This is specific rollout authorization; it does not authorize unrelated schema/financial changes or future destructive operations.

Migration21 was applied in the preceding checkpoint and independently verified before the resumed site replacement. It was not reapplied. Studio and the associated provisioning changes are deployed; verification limits and backup evidence are recorded in [the release report](../reports/vendor-studio-release-2026-10-30.md).
