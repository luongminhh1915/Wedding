using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Tests;

public class CommissionPolicyRuleTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid AdminId = Guid.NewGuid();

    [Fact]
    public void Create_RequiresTargetAndBasisToMatchScope()
    {
        Assert.Throws<DomainException>(() => Create(CommissionRuleScope.Service, categoryId: Guid.NewGuid()));
        Assert.Throws<DomainException>(() => Create(CommissionRuleScope.Combo, comboTemplateId: Guid.NewGuid(),
            comboTemplateRevisionId: Guid.NewGuid(), rate: null, basis: CommissionBasisKind.StandardService));

        var service = Create(CommissionRuleScope.Service, serviceItemId: Guid.NewGuid());
        Assert.Throws<DomainException>(() => Create(CommissionRuleScope.Combo, comboTemplateId: Guid.NewGuid(),
            comboTemplateRevisionId: Guid.NewGuid(), basis: CommissionBasisKind.ComboComponents));
        var combo = Create(CommissionRuleScope.Combo, comboTemplateId: Guid.NewGuid(),
            comboTemplateRevisionId: Guid.NewGuid(),
            rate: null,
            basis: CommissionBasisKind.ComboComponents);

        Assert.Equal(CommissionRuleScope.Service, service.Scope);
        Assert.Equal(CommissionRuleScope.Combo, combo.Scope);
    }

    [Fact]
    public void Create_RejectsInvalidRatesAndNonUtcOrInvertedDates()
    {
        Assert.Throws<DomainException>(() => Create(CommissionRuleScope.System, rate: .02m));
        Assert.Throws<DomainException>(() => Create(CommissionRuleScope.System, rate: .13m));
        Assert.Throws<DomainException>(() => Create(CommissionRuleScope.System,
            effectiveFrom: DateTime.SpecifyKind(Start, DateTimeKind.Local)));
        Assert.Throws<DomainException>(() => Create(CommissionRuleScope.System, effectiveTo: Start));
    }

    [Fact]
    public void Overlaps_RejectsIntersectingWindowsOnlyForSameTarget()
    {
        var first = Create(CommissionRuleScope.Vendor, vendorId: Guid.NewGuid(),
            effectiveFrom: Start, effectiveTo: Start.AddDays(10));
        var sameTarget = Create(CommissionRuleScope.Vendor, vendorId: first.VendorId,
            effectiveFrom: Start.AddDays(10), effectiveTo: Start.AddDays(20));
        var differentTarget = Create(CommissionRuleScope.Vendor, vendorId: Guid.NewGuid(),
            effectiveFrom: Start.AddDays(2), effectiveTo: Start.AddDays(5));
        var adjacent = Create(CommissionRuleScope.Vendor, vendorId: first.VendorId,
            effectiveFrom: Start.AddDays(11), effectiveTo: Start.AddDays(20));

        Assert.True(first.Overlaps(sameTarget));
        Assert.False(first.Overlaps(differentTarget));
        Assert.False(first.Overlaps(adjacent));
    }

    [Fact]
    public void StateTransitions_RequireSubmitAndRecordDecision()
    {
        var rule = Create(CommissionRuleScope.System);
        Assert.Throws<DomainException>(() => rule.Approve(AdminId));

        rule.Submit();
        Assert.Equal(CommissionPolicyStatus.Submitted, rule.Status);
        rule.Approve(AdminId);
        Assert.Equal(CommissionPolicyStatus.Approved, rule.Status);
        Assert.Equal(AdminId, rule.ApprovedById);
        Assert.Throws<DomainException>(() => rule.Reject(AdminId, "too late"));
    }

    [Fact]
    public void ComboPolicyHasNoParentRate_AndRequiresPinnedRevision()
    {
        Assert.Throws<DomainException>(() => Create(CommissionRuleScope.Combo, comboTemplateId: Guid.NewGuid(),
            comboTemplateRevisionId: Guid.NewGuid(), rate: .08m, basis: CommissionBasisKind.ComboComponents));
        var combo = Create(CommissionRuleScope.Combo, comboTemplateId: Guid.NewGuid(),
            comboTemplateRevisionId: Guid.NewGuid(), rate: null, basis: CommissionBasisKind.ComboComponents);
        Assert.Null(combo.Rate);
        Assert.NotNull(combo.ComboTemplateRevisionId);
    }

    [Fact]
    public void ComponentBasisRequiresExplicitDefinitionAndValidExclusionList()
    {
        Assert.Throws<DomainException>(() => CreateBasis(excludedCostsJson: "{invalid"));
        Assert.Throws<DomainException>(() => CreateBasis(basisKind: CommissionBasisKind.ComboComponents));
        Assert.Throws<DomainException>(() => CreateBasis(basisDefinition: " "));

        var basis = CreateBasis(excludedCostsJson: "[\"Verified pass-through\"]");
        Assert.Equal(CommissionBasisKind.StandardService, basis.BasisKind);
        Assert.Equal("[\"Verified pass-through\"]", basis.ExcludedCostsJson);
    }

    [Fact]
    public void ComponentBasisApprovalTransitionsAreVersionedAndImmutableByDomain()
    {
        var basis = CreateBasis();
        var initialVersion = basis.Version;
        Assert.Throws<DomainException>(() => basis.Approve(AdminId));
        basis.Submit();
        Assert.NotEqual(initialVersion, basis.Version);
        basis.Approve(AdminId);
        Assert.Equal(CommissionPolicyStatus.Approved, basis.Status);
        Assert.Equal(AdminId, basis.ApprovedById);
    }

    [Fact]
    public void AdjustmentRequestSnapshotsOutstandingObligationAndAllowsOneDecision()
    {
        var vendorId = Guid.NewGuid();
        var contract = BookingContract.Create(Guid.NewGuid(), null, vendorId, Guid.NewGuid(), 1_000_000, 200_000, null, Start);
        var commission = Commission.Create(contract.Id, vendorId, CommissionPeriod.Period1_Deposit, .08m, 80_000, Start);
        commission.ApplyPayment(30_000, "synthetic-test");

        var adjustment = CommissionAdjustmentRequest.Create(contract, commission, AdminId, .07m, "Test reason");
        Assert.Equal(50_000, adjustment.OutstandingAmountSnapshot);
        Assert.Equal(.08m, adjustment.OriginalRateSnapshot);
        Assert.Equal(CommissionAdjustmentStatus.Submitted, adjustment.Status);

        adjustment.Approve(Guid.NewGuid(), "Append-only decision");
        Assert.Equal(CommissionAdjustmentStatus.Approved, adjustment.Status);
        Assert.Equal(50_000, commission.OutstandingAmount);
        Assert.Throws<DomainException>(() => adjustment.Reject(Guid.NewGuid(), "second decision"));
    }

    private static CommissionPolicyRule Create(
        CommissionRuleScope scope,
        Guid? categoryId = null,
        Guid? vendorId = null,
        Guid? serviceItemId = null,
        Guid? comboTemplateId = null,
        Guid? comboTemplateRevisionId = null,
        decimal? rate = .08m,
        CommissionBasisKind basis = CommissionBasisKind.StandardService,
        DateTime? effectiveFrom = null,
        DateTime? effectiveTo = null) =>
        CommissionPolicyRule.Create(scope, categoryId, vendorId, serviceItemId, comboTemplateId, comboTemplateRevisionId, rate, basis,
            "Phase 5 v1", "Approved business policy", effectiveFrom ?? Start, effectiveTo, AdminId);

    private static CommissionComponentBasisApproval CreateBasis(
        CommissionBasisKind basisKind = CommissionBasisKind.StandardService,
        string basisDefinition = "Contract service value before VAT less vendor-funded discounts",
        string excludedCostsJson = "[]") =>
        CommissionComponentBasisApproval.Create(Guid.NewGuid(), Guid.NewGuid(), 1, .08m, basisKind, basisDefinition,
            excludedCostsJson, "Admin-approved component basis", Start, null, AdminId);
}
