using Wedding.Domain.Entities;
namespace Wedding.Tests;
public class RegionRulesTests
{
    [Theory]
    [InlineData("TP. Hồ Chí Minh", "Hồ Chí Minh", true)]
    [InlineData("HCM", "TP HCM", true)]
    [InlineData("hồ chí minh", "Ho Chi Minh", true)]
    [InlineData("TP. Hà Nội", "Hà Nội", true)]
    [InlineData("Hồ Chí Minh,Đà Nẵng", "Đà Nẵng", true)]
    [InlineData("TP. Hồ Chí Minh", "Hà Nội", false)]
    [InlineData("Hồ Chí Minh", "", false)]
    [InlineData("*", "Cần Thơ", true)]
    public void RegionAliasesDoNotRejectTheSameCityOrAllowAnother(string allowed, string requested, bool expected)
        => Assert.Equal(expected, RegionRules.Serves(allowed, requested));
}
