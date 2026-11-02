using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Tests;

public class CommissionAdjustmentRatePrecisionTests
{
    [Fact]
    public void AdjustmentRejectsRatesThatSqlServerWouldSilentlyRound()
    {
        var vendorId = Guid.NewGuid();
        var contract = BookingContract.Create(Guid.NewGuid(), null, vendorId, Guid.NewGuid(),
            1_000_000, 200_000, null, DateTime.UtcNow.AddDays(30));
        var commission = Commission.Create(contract.Id, vendorId, CommissionPeriod.Period1_Deposit,
            .08m, 80_000, DateTime.UtcNow.AddDays(30));
        foreach (var rate in new[] { .08001m, .07999m, .03001m, .11999m })
            Assert.Throws<DomainException>(() => CommissionAdjustmentRequest.Create(contract, commission,
                Guid.NewGuid(), rate, "Synthetic precision validation"));
    }

    [Fact]
    public void AdjustmentKeepsExactRepresentableRateIncludingBoundaries()
    {
        var vendorId = Guid.NewGuid();
        var contract = BookingContract.Create(Guid.NewGuid(), null, vendorId, Guid.NewGuid(),
            1_000_000, 200_000, null, DateTime.UtcNow.AddDays(30));
        var commission = Commission.Create(contract.Id, vendorId, CommissionPeriod.Period1_Deposit,
            .08m, 80_000, DateTime.UtcNow.AddDays(30));
        foreach (var rate in new[] { .03m, .0800m, .0801m, .12m })
            Assert.Equal(rate, CommissionAdjustmentRequest.Create(contract, commission,
                Guid.NewGuid(), rate, "Synthetic exact rate").ProposedRate);
    }
}
