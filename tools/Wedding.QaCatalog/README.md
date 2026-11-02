# Isolated QA combo catalog (not production seed)

This manually invoked utility is hard-locked to **WeddingPlanUi_0161333**, a QA database created for this implementation. It refuses every other database, does not create/migrate/delete databases, and never runs during API startup. It appends only fictional `[DEMO]` records; existing users, vendors, categories, and data are never updated. Rerunning after successful seeding is a no-op.

Dry run:

```powershell
# Requires the project's .NET 10 SDK/runtime; no major roll-forward override.
dotnet run --project tools/Wedding.QaCatalog --artifacts-path "$env:TEMP/wedding-qa-catalog" -- WeddingPlanUi_0161333
```

Explicit append:

```powershell
dotnet run --project tools/Wedding.QaCatalog --artifacts-path "$env:TEMP/wedding-qa-catalog" -- WeddingPlanUi_0161333 --apply
```

Current fixture definition: three fictional vendors (one full-service + photo/decor specialists), 27 approved service items across the existing seven categories, variants, add-ons, Table/Guest/Package units, and nine single-vendor presets (three tiers for each vendor). Every label, listing description and policy says DEMO/QA; no real bank details or payments are seeded. QA-only vendor accounts use reserved `.example` emails, password `DemoWeddingOnly!2026`. Do not use these fixtures as real provider claims or migrate them into a deployment.

Earlier QA runs created three mixed-vendor presets. They are historical fixture data: this utility never deletes or rewrites them. The public API now filters them out, rejects mixed-vendor orders/drafts, and the current fixture definition never creates new mixed presets.

## One explicitly known QA wizard cohort

After applying `AddBudgetProvenance`, the optional `--stamp-known-qa-plan --apply` flags stamp only the 14 pristine wizard-generated rows belonging to `journey-cart-qa-20261016@example.invalid` in this exact QA database. Their origin was established during the same manual QA session. The utility verifies the complete original names/amounts, zero actuals, null notes/contracts/combo links, and creation close to onboarding; it refuses changed cohorts. It changes only provenance/category links, never financial values. There is deliberately no inference or backfill path for real accounts or any other database.
