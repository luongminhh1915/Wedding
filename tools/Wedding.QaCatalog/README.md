# Isolated QA combo catalog (not production seed)

This manually invoked utility is hard-locked to **WeddingPlanUi_0161333**, a QA database created for this implementation. It refuses every other database, does not create/migrate/delete databases, and never runs during API startup. It appends only fictional `[DEMO]` records; existing users, vendors, categories, and data are never updated. Rerunning after successful seeding is a no-op.

Dry run:

```powershell
$env:DOTNET_ROLL_FORWARD = 'Major' # Only needed on this machine, which lacks the .NET 8 runtime.
dotnet run --project tools/Wedding.QaCatalog --artifacts-path "$env:TEMP/wedding-qa-catalog" -- WeddingPlanUi_0161333
```

Explicit append:

```powershell
dotnet run --project tools/Wedding.QaCatalog --artifacts-path "$env:TEMP/wedding-qa-catalog" -- WeddingPlanUi_0161333 --apply
```

Fixture: three fictional vendors (one full-service + photo/decor specialists), 27 approved service items across the existing seven categories, variants, add-ons, Table/Guest/Package units, and six presets (three tiers × single/mixed-vendor). Every label, listing description and policy says DEMO/QA; no real bank details or payments are seeded. QA-only vendor accounts use reserved `.example` emails, password `DemoWeddingOnly!2026`. Do not use these fixtures as real provider claims or migrate them into a deployment.
