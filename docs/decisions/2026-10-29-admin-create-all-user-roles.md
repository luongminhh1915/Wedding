# Admin provisioning of all user roles — 29/10/2026

Status: **CONFIRMED**.

Source: user asked Admin to create customers, vendors and all other users, then explicitly answered **“Có, tạo được cả SuperAdmin”** to whether this includes another full administrator.

## Confirmed capability

- Only an existing active SuperAdmin may create new Customer, VendorOwner, Moderator, Finance, CustomerCare or SuperAdmin accounts from user management. This does not grant account provisioning to Moderator/Finance/CustomerCare, public registration or an unauthenticated actor.
- The new account remains separate from the creating administrator's session. No JWT/password/hash is returned and no existing email/account is promoted, reassigned or overwritten.
- Customer creation follows existing new-customer onboarding. VendorOwner creation includes its required one-to-one shop profile, canonical brand/location validation and existing category-based registration defaults.
- Creating an account does not approve a Vendor's shop/listings/combos, record the Vendor accepting terms, sign a partnership agreement or activate a financial exception. No old vendor/contract/commission snapshot changes.

## Implementation safeguards

Use a canonical authenticated POST `/api/users` while retaining legacy staff-only POST `/api/users/staff`. Validate the current administrator's active role inside the creation transaction; enforce unique normalized email, atomic account/shop/audit creation and redacted audit actor/time.

Require an explicit confirmation when creating another SuperAdmin and apply the existing bootstrap's12-character minimum for that new privileged account; other roles retain existing password rules. Do not change existing users' credentials or the public registration flow. UI confirmations/draft guards are implementation safeguards, not a new financial policy.

This supersedes earlier descriptions that the Admin UI may create only three staff roles and cannot create another SuperAdmin. The first-administrator deployment bootstrap still creates only the first account; it must not be relaxed or reused to bypass authenticated provisioning. No real accounts are created merely to test this capability; use owned isolated QA fixtures.
