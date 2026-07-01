using Atrium.Modules.Storefront.Checkout;

namespace Atrium.UnitTests;

/// <summary>
/// U6 — the (simulated) checkout guards: pure client-side card validation and the mock authorizer's
/// approve/decline decision. Concept on show: pinning payment logic — the Luhn/expiry/CVC rules and the
/// documented decline test card — without a component host, a browser, or any real gateway.
/// </summary>
public class PaymentTests
{
    private static readonly DateOnly Today = new(2026, 7, 1);

    // ---- Card number (Luhn + length) ----

    [Theory]
    [InlineData("4242 4242 4242 4242")] // Visa test approve card
    [InlineData("4000000000000002")] // Visa test decline card — still a valid PAN
    [InlineData("5555555555554444")] // Mastercard test card
    [InlineData("378282246310005")] // Amex test card (15 digits)
    public void Valid_card_numbers_pass_luhn(string number) =>
        Assert.True(PaymentValidation.IsValidCardNumber(number));

    [Theory]
    [InlineData("4242 4242 4242 4241")] // fails Luhn
    [InlineData("1234567890123456")] // fails Luhn
    [InlineData("4242")] // too short
    [InlineData("42424242424242424242")] // too long (20 digits)
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not-a-card")]
    public void Invalid_card_numbers_fail(string? number) =>
        Assert.False(PaymentValidation.IsValidCardNumber(number));

    // ---- Expiry (MM/YY, valid future month) ----

    [Theory]
    [InlineData("07/26")] // current month is still valid
    [InlineData("12/30")]
    [InlineData("1/27")] // single-digit month tolerated
    public void Valid_future_expiries_pass(string expiry) =>
        Assert.True(PaymentValidation.IsValidExpiry(expiry, Today));

    [Theory]
    [InlineData("06/26")] // last month — expired
    [InlineData("13/30")] // no month 13
    [InlineData("00/30")] // no month 0
    [InlineData("07-26")] // wrong separator
    [InlineData("0726")]
    [InlineData("")]
    [InlineData(null)]
    public void Invalid_or_past_expiries_fail(string? expiry) =>
        Assert.False(PaymentValidation.IsValidExpiry(expiry, Today));

    // ---- CVC ----

    [Theory]
    [InlineData("123")]
    [InlineData("1234")]
    public void Valid_cvcs_pass(string cvc) => Assert.True(PaymentValidation.IsValidCvc(cvc));

    [Theory]
    [InlineData("12")]
    [InlineData("12345")]
    [InlineData("12a")]
    [InlineData("")]
    [InlineData(null)]
    public void Invalid_cvcs_fail(string? cvc) => Assert.False(PaymentValidation.IsValidCvc(cvc));

    [Fact]
    public void Cardholder_must_be_non_empty()
    {
        Assert.True(PaymentValidation.IsValidCardholder("Ada Lovelace"));
        Assert.False(PaymentValidation.IsValidCardholder("   "));
        Assert.False(PaymentValidation.IsValidCardholder(null));
    }

    // ---- Mock authorizer approve/decline ----

    [Fact]
    public async Task Default_card_is_approved_with_a_reference()
    {
        var result = await new PaymentService().AuthorizeAsync(
            new PaymentRequest("Ada Lovelace", "4242 4242 4242 4242", "12/30", "123", 50m),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.Approved);
        Assert.False(string.IsNullOrWhiteSpace(result.Reference));
        Assert.Null(result.DeclineReason);
    }

    [Fact]
    public async Task Documented_test_card_is_declined_without_a_reference()
    {
        var result = await new PaymentService().AuthorizeAsync(
            new PaymentRequest("Ada Lovelace", "4000 0000 0000 0002", "12/30", "123", 50m),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.Approved);
        Assert.Null(result.Reference);
        Assert.False(string.IsNullOrWhiteSpace(result.DeclineReason));
    }
}
