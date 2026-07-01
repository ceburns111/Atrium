namespace Atrium.Modules.Storefront.Checkout;

/// <summary>
/// Pure, client-side validation for the (simulated) checkout card form. No card data ever leaves the
/// browser circuit or is persisted/logged — these helpers only shape inline field errors. Kept as
/// static, side-effect-free functions so they are trivially unit-testable without a component host.
/// </summary>
public static class PaymentValidation
{
    public static bool IsValidCardholder(string? name) => !string.IsNullOrWhiteSpace(name);

    /// <summary>13–19 digits that pass the Luhn checksum (spaces are ignored).</summary>
    public static bool IsValidCardNumber(string? cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
        {
            return false;
        }

        var digits = cardNumber.Where(char.IsDigit).ToArray();
        if (digits.Length is < 13 or > 19)
        {
            return false;
        }

        var sum = 0;
        var doubleNext = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (doubleNext)
            {
                d *= 2;
                if (d > 9)
                {
                    d -= 9;
                }
            }
            sum += d;
            doubleNext = !doubleNext;
        }
        return sum % 10 == 0;
    }

    /// <summary>An <c>MM/YY</c> string whose month is 01–12 and whose end-of-month is not in the past.</summary>
    public static bool IsValidExpiry(string? expiry, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(expiry))
        {
            return false;
        }

        var parts = expiry.Split('/');
        if (
            parts.Length != 2
            || !int.TryParse(parts[0].Trim(), out var month)
            || !int.TryParse(parts[1].Trim(), out var shortYear)
        )
        {
            return false;
        }

        if (month is < 1 or > 12)
        {
            return false;
        }

        var year = 2000 + shortYear;
        var endOfMonth = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        return endOfMonth >= today;
    }

    /// <summary>A 3- or 4-digit security code.</summary>
    public static bool IsValidCvc(string? cvc)
    {
        if (string.IsNullOrWhiteSpace(cvc))
        {
            return false;
        }

        var trimmed = cvc.Trim();
        return trimmed.Length is 3 or 4 && trimmed.All(char.IsDigit);
    }
}
