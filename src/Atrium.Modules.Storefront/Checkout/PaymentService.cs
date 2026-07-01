namespace Atrium.Modules.Storefront.Checkout;

/// <summary>The card data captured by the checkout form for a single (simulated) authorization.</summary>
/// <remarks>
/// This record lives only in component state during the transaction and is discarded afterwards. The
/// PAN, CVC, and expiry are NEVER persisted to a database, written to <c>localStorage</c>, or logged.
/// </remarks>
public sealed record PaymentRequest(
    string CardholderName,
    string CardNumber,
    string Expiry,
    string Cvc,
    decimal Amount
);

/// <summary>The outcome of a simulated authorization: approved with a reference, or declined.</summary>
public sealed record PaymentResult(bool Approved, string? Reference, string? DeclineReason);

/// <summary>
/// A MOCK payment authorizer. It does not talk to any real gateway, network, or SDK — it simulates a
/// round-trip and returns an approve/decline decision so the checkout flow is demoable end to end.
/// Approves by default; declines a single documented test card so the decline path is reproducible.
/// It never reads the CVC and never logs or stores the card number.
/// </summary>
public sealed class PaymentService
{
    /// <summary>
    /// Documented decline test card. Any card number ending in these digits is declined (it still
    /// passes Luhn + length, so it exercises the post-validation decline path). Matches Stripe's
    /// well-known 4000 0000 0000 0002 "card declined" test PAN.
    /// </summary>
    public const string DeclineSuffix = "0002";

    public async Task<PaymentResult> AuthorizeAsync(
        PaymentRequest request,
        CancellationToken ct = default
    )
    {
        // Simulate a gateway round-trip so the UI shows a realistic pending state. No real network call.
        await Task.Delay(600, ct);

        var digits = new string(request.CardNumber.Where(char.IsDigit).ToArray());
        if (digits.EndsWith(DeclineSuffix, StringComparison.Ordinal))
        {
            return new PaymentResult(
                Approved: false,
                Reference: null,
                DeclineReason: "Your card was declined by the issuer. Please try a different card."
            );
        }

        var reference = "AUTH-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        return new PaymentResult(Approved: true, Reference: reference, DeclineReason: null);
    }
}
