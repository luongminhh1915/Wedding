using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;

namespace Wedding.Tests;

public sealed class OperatingAreaRulesTests
{
    [Fact]
    public void NormalizeNationwideReturnsWildcardAndEmptyList()
    {
        var result = OperatingAreaRules.Normalize(null, null, true);
        Assert.True(result.Nationwide);
        Assert.Empty(result.Regions);
        Assert.Equal("*", result.StorageValue);
    }

    [Fact]
    public void NormalizeNationwideRejectsNonEmptyRegions()
    {
        var ex = Assert.Throws<DomainException>(() => OperatingAreaRules.Normalize(null, ["Hà Nội"], true));
        Assert.Contains("không chọn đồng thời", ex.Message);
    }

    [Fact]
    public void NormalizeDeduplicatesAndPreservesCanonicalProvinces()
    {
        var result = OperatingAreaRules.Normalize(null, ["Hà Nội", "TP. Hồ Chí Minh", "Hà Nội", "Đà Nẵng"], false);
        Assert.False(result.Nationwide);
        Assert.Equal(3, result.Regions.Count);
        Assert.Contains("Hà Nội", result.Regions);
        Assert.Contains("Hồ Chí Minh", result.Regions);
        Assert.Contains("Đà Nẵng", result.Regions);
        Assert.Equal("Hà Nội,Hồ Chí Minh,Đà Nẵng", result.StorageValue);
    }

    [Fact]
    public void NormalizeFallsBackToCityWhenRegionsNull()
    {
        var result = OperatingAreaRules.Normalize("Hải Phòng", null, false);
        Assert.False(result.Nationwide);
        Assert.Single(result.Regions);
        Assert.Equal("Hải Phòng", result.Regions[0]);
        Assert.Equal("Hải Phòng", result.StorageValue);
    }

    [Fact]
    public void NormalizeAllowsEmptyAreaWhenNotRequired()
    {
        var result = OperatingAreaRules.Normalize(null, null, false);
        Assert.False(result.Nationwide);
        Assert.Empty(result.Regions);
        Assert.Empty(result.StorageValue);
        Assert.True(OperatingAreaRules.IsValid(null, null, false, requireArea: false));
        Assert.False(OperatingAreaRules.IsValid(null, null, false, requireArea: true));
    }

    [Fact]
    public void NormalizeRejectsUnknownProvince()
    {
        var ex = Assert.Throws<DomainException>(() => OperatingAreaRules.Normalize(null, ["DiaPhuongKhongTonTai"], false));
        Assert.Contains("34 tỉnh / thành phố", ex.Message);
    }

    [Fact]
    public void ReadParsesWildcardAndCommaSeparatedList()
    {
        var nationwide = OperatingAreaRules.Read("*", null);
        Assert.True(nationwide.Nationwide);
        Assert.Empty(nationwide.Regions);

        var list = OperatingAreaRules.Read("Hà Nội,Hồ Chí Minh", null);
        Assert.False(list.Nationwide);
        Assert.Equal(["Hà Nội", "Hồ Chí Minh"], list.Regions);

        var legacy = OperatingAreaRules.Read(null, "Cần Thơ");
        Assert.False(legacy.Nationwide);
        Assert.Equal(["Cần Thơ"], legacy.Regions);
    }

    [Fact]
    public void ServesMatchesNationwideOrSpecificRegions()
    {
        Assert.True(OperatingAreaRules.Serves("*", null, "Hà Nội"));
        Assert.True(OperatingAreaRules.Serves("*", null, "TP. Hồ Chí Minh"));
        Assert.False(OperatingAreaRules.Serves("*", null, "UnknownProvince"));

        Assert.True(OperatingAreaRules.Serves("Hà Nội,Đà Nẵng", null, "Hà Nội"));
        Assert.False(OperatingAreaRules.Serves("Hà Nội,Đà Nẵng", null, "Hồ Chí Minh"));
    }

    [Fact]
    public void VendorEntitySetsOperatingAreasCorrectly()
    {
        var vendor = Vendor.CreateForAdministration(Guid.NewGuid(), "Brand", "brand-slug", "Contact", "0901234567", "Hà Nội");
        Assert.Null(vendor.OperatingRegions);

        vendor.SetOperatingAreas(["Hà Nội", "Hồ Chí Minh"], false);
        Assert.Equal("Hà Nội,Hồ Chí Minh", vendor.OperatingRegions);

        vendor.SetOperatingAreas(null, true);
        Assert.Equal("*", vendor.OperatingRegions);
    }
}

