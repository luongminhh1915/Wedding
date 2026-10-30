using System.Text.Json;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Tests;

public class CommissionComponentBasisDomainGuardTests
{
    [Theory]
    [InlineData("0.08001")]
    [InlineData("0.11999")]
    public void RateRejectsPrecisionThatSqlServerWouldSilentlyRound(string value)
    {
        var rate = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Throws<DomainException>(() => CommissionComponentBasisApproval.Create(Guid.NewGuid(), Guid.NewGuid(), 1, rate,
            CommissionBasisKind.StandardService, "Synthetic standard basis", "[]", "Synthetic precision check",
            new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc), null, Guid.NewGuid()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[null]")]
    public void ExclusionsRejectMissingOrMalformedInputAsDomainValidation(string? json)
    {
        Assert.Throws<DomainException>(() => Create(json!));
    }

    [Fact]
    public void ExclusionsRejectNormalizedUnicodeJsonThatExceedsMappedStorageLimit()
    {
        var labels = Enumerable.Range(0, 50)
            .Select(index => new string('ắ', 198) + index.ToString("D2")).ToList();
        var json = JsonSerializer.Serialize(labels);
        Assert.True(json.Length > 12000);
        Assert.All(labels, label => Assert.Equal(200, label.Length));

        var error = Assert.Throws<DomainException>(() => Create(json));
        Assert.Contains("12.000", error.Message);
    }

    [Fact]
    public void ExclusionsNormalizeBeforeEnforcingPersistedSize()
    {
        var label = new string('ắ', 190);
        var raw = JsonSerializer.Serialize(Enumerable.Repeat($" {label} ", 50));
        Assert.True(raw.Length > 12000);

        var basis = Create(raw);
        Assert.Equal(JsonSerializer.Serialize(new[] { label }), basis.ExcludedCostsJson);
        Assert.True(basis.ExcludedCostsJson.Length <= 12000);
    }

    [Fact]
    public void ExclusionsAcceptFiftyDistinctAsciiLabelsWithinMappedStorageLimit()
    {
        var labels = Enumerable.Range(0, 50)
            .Select(index => new string('a', 198) + index.ToString("D2")).ToList();
        var normalized = JsonSerializer.Serialize(labels);
        Assert.True(normalized.Length <= 12000);
        Assert.Equal(normalized, Create(normalized).ExcludedCostsJson);
    }

    private static CommissionComponentBasisApproval Create(string json) =>
        CommissionComponentBasisApproval.Create(Guid.NewGuid(), Guid.NewGuid(), 1, .08m,
            CommissionBasisKind.StandardService, "Before VAT less approved external pass-through only", json,
            "Synthetic basis validation", new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc), null, Guid.NewGuid());
}
