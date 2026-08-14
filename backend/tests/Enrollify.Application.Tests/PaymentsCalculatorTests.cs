using Enrollify.Application.Features.Payments.Queries;
using Enrollify.Domain.Entities;
using Xunit;

namespace Enrollify.Application.Tests;

public class PaymentsCalculatorTests
{
    private static PaymentTerm Term(decimal down = 0, decimal interest = 0, decimal discount = 0, int installments = 1) => new()
    {
        SchoolYear = "2025-2026",
        PlanType = "Any",
        DownPaymentPercent = down,
        InterestRatePercent = interest,
        DiscountPercent = discount,
        InstallmentCount = installments
    };

    [Fact]
    public void Full_WithDiscountTerm_PaysDiscountedTotal()
    {
        var result = PaymentsCalculator.EffectiveTotal("Full", 10000m, Term(discount: 5m));
        Assert.Equal(9500m, result);
    }

    [Fact]
    public void Full_WithoutTerm_PaysFullTotal()
    {
        var result = PaymentsCalculator.EffectiveTotal("Full", 10000m, null);
        Assert.Equal(10000m, result);
    }

    [Fact]
    public void Monthly_TermDriven_AddsInterestOnFinancedRemainder()
    {
        // Down 20% of 10000 = 2000; interest 5% of the remaining 8000 = 400.
        var result = PaymentsCalculator.EffectiveTotal("Monthly", 10000m, Term(down: 20m, interest: 5m, installments: 9));
        Assert.Equal(10400m, result);
    }

    [Fact]
    public void Quarterly_TermDriven_AddsInterestOnFinancedRemainder()
    {
        // Down 40% of 10000 = 4000; interest 3% of the remaining 6000 = 180.
        var result = PaymentsCalculator.EffectiveTotal("Quarterly", 10000m, Term(down: 40m, interest: 3m, installments: 3));
        Assert.Equal(10180m, result);
    }

    [Fact]
    public void Monthly_RoundsDownPaymentAndInterestToCentavos()
    {
        // Down = Round(9999.99 * 20%) = 2000.00; interest = Round(7999.99 * 5%) = 400.00.
        var result = PaymentsCalculator.EffectiveTotal("Monthly", 9999.99m, Term(down: 20m, interest: 5m));
        Assert.Equal(10399.99m, result);
    }

    [Fact]
    public void Monthly_NullTerm_FallsBackTo20PercentDown_NoInterest()
    {
        // Without a term the fallback down payment is 20% and there is no interest,
        // so the effective total equals the raw total.
        var result = PaymentsCalculator.EffectiveTotal("Monthly", 10000m, null);
        Assert.Equal(10000m, result);
    }

    [Fact]
    public void Quarterly_NullTerm_FallsBackTo30PercentDown_NoInterest()
    {
        var result = PaymentsCalculator.EffectiveTotal("Quarterly", 10000m, null);
        Assert.Equal(10000m, result);
    }

    [Fact]
    public void NullPlan_PaysRawTotal()
    {
        var result = PaymentsCalculator.EffectiveTotal(null, 12345.67m, null);
        Assert.Equal(12345.67m, result);
    }

    [Fact]
    public void UnknownPlan_PaysRawTotal()
    {
        var result = PaymentsCalculator.EffectiveTotal("Weekly", 5000m, Term(down: 50m, interest: 10m, discount: 10m));
        Assert.Equal(5000m, result);
    }
}
