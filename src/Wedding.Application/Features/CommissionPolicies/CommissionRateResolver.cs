using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;

namespace Wedding.Application.Features.CommissionPolicies;

public record ResolvedCommissionRate(
    decimal Rate,
    string RateSource,
    Guid? RuleId,
    string? PolicyVersion,
    CommissionBasisKind BasisKind
);

public static class CommissionRateResolver
{
    public static async Task<ResolvedCommissionRate> ResolveRateAsync(
        IApplicationDbContext db,
        Guid vendorId,
        Guid categoryId,
        Guid? serviceItemId,
        Guid? comboTemplateId,
        decimal legacyVendorRate,
        decimal defaultCategoryRate,
        DateTime transactionUtc,
        CancellationToken ct = default)
    {
        // Query active Approved rules whose effective range covers transactionUtc
        var rules = await db.CommissionPolicyRules.AsNoTracking()
            .Where(r => r.Status == CommissionPolicyStatus.Approved &&
                        r.EffectiveFrom <= transactionUtc &&
                        (!r.EffectiveTo.HasValue || transactionUtc <= r.EffectiveTo.Value))
            .ToListAsync(ct);

        // Priority 1: Service (or Combo)
        if (serviceItemId.HasValue && serviceItemId.Value != Guid.Empty)
        {
            var serviceRule = rules.FirstOrDefault(r => r.Scope == CommissionRuleScope.Service && r.ServiceItemId == serviceItemId.Value);
            if (serviceRule?.Rate != null)
            {
                return new(serviceRule.Rate.Value, "PolicyRule:Service", serviceRule.Id, serviceRule.PolicyVersion, serviceRule.BasisKind);
            }
        }
        else if (comboTemplateId.HasValue && comboTemplateId.Value != Guid.Empty)
        {
            var comboRule = rules.FirstOrDefault(r => r.Scope == CommissionRuleScope.Combo && r.ComboTemplateId == comboTemplateId.Value);
            if (comboRule != null)
            {
                return new(0m, "PolicyRule:Combo", comboRule.Id, comboRule.PolicyVersion, CommissionBasisKind.ComboComponents);
            }
        }

        // Priority 2: VendorCategory (Specific Vendor within Specific Category)
        var vendorCategoryRule = rules.FirstOrDefault(r => r.Scope == CommissionRuleScope.VendorCategory && r.VendorId == vendorId && r.CategoryId == categoryId);
        if (vendorCategoryRule?.Rate != null)
        {
            return new(vendorCategoryRule.Rate.Value, "PolicyRule:VendorCategory", vendorCategoryRule.Id, vendorCategoryRule.PolicyVersion, vendorCategoryRule.BasisKind);
        }

        // Priority 3: Vendor (All services of this Vendor)
        var vendorRule = rules.FirstOrDefault(r => r.Scope == CommissionRuleScope.Vendor && r.VendorId == vendorId);
        if (vendorRule?.Rate != null)
        {
            return new(vendorRule.Rate.Value, "PolicyRule:Vendor", vendorRule.Id, vendorRule.PolicyVersion, vendorRule.BasisKind);
        }

        // Priority 4: Category (Industry / Category policy)
        var categoryRule = rules.FirstOrDefault(r => r.Scope == CommissionRuleScope.Category && r.CategoryId == categoryId);
        if (categoryRule?.Rate != null)
        {
            return new(categoryRule.Rate.Value, "PolicyRule:Category", categoryRule.Id, categoryRule.PolicyVersion, categoryRule.BasisKind);
        }

        // Priority 5: System (Platform default policy)
        var systemRule = rules.FirstOrDefault(r => r.Scope == CommissionRuleScope.System);
        if (systemRule?.Rate != null)
        {
            return new(systemRule.Rate.Value, "PolicyRule:System", systemRule.Id, systemRule.PolicyVersion, systemRule.BasisKind);
        }

        // Fallback: Legacy vendor rate or Category default
        var fallbackSource = legacyVendorRate > 0m ? "LegacyVendor" : "CategoryDefault";
        var fallbackRate = CommissionRules.ResolveRate(defaultCategoryRate, legacyVendorRate);
        return new(fallbackRate, fallbackSource, null, null, CommissionBasisKind.StandardService);
    }
}

