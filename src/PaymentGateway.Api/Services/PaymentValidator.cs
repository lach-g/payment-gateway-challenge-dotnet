using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services;

public class ValidationResult
{
    public required bool IsValid { get; init; }
    public Dictionary<string, string> Errors { get; init; } = [];
}

public interface IPaymentValidator
{
    ValidationResult Validate(PostPaymentRequest request);
}

public class PaymentValidator : IPaymentValidator
{
    private readonly ILogger<PaymentValidator> _logger;
    private static readonly HashSet<string> _acceptedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "USD",
        "GBP",
        "AUD",
    };

    public PaymentValidator(ILogger<PaymentValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates the incoming payment request for required fields, correct formats, and business rules.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public ValidationResult Validate(PostPaymentRequest request)
    {
        var errors = new Dictionary<string, string>();

        if (request == null)
        {
            errors.Add(nameof(request), "Request cannot be null.");
            _logger.LogWarning("Validation failed: request is null.");
            return new ValidationResult { IsValid = false, Errors = errors };
        }

        if (!IsValidCardNumber(request.CardNumber))
        {
            _logger.LogWarning("Validation failed: Card number is invalid.");
            errors.Add(nameof(request.CardNumber), "Card number must be between 14 and 19 digits.");
        }

        if (!IsValidExpiryDate(request.ExpiryMonth, request.ExpiryYear))
        {
            _logger.LogWarning("Validation failed: Card has expired.");
            errors.Add(nameof(request.ExpiryMonth), "Card has expired.");
        }

        if (!IsValidCurrency(request.Currency))
        {
            _logger.LogWarning("Validation failed: Currency {Currency} is not supported.", request.Currency);
            errors.Add(nameof(request.Currency), "Currency is not supported.");
        }

        if (!IsValidAmount(request.Amount))
        {
            _logger.LogWarning("Validation failed: Amount {Amount} is not valid.", request.Amount);
            errors.Add(nameof(request.Amount), "Amount must be greater than zero.");
        }

        if (!IsValidCvv(request.Cvv))
        {
            _logger.LogWarning("Validation failed: CVV is invalid.");
            errors.Add(nameof(request.Cvv), "CVV must be a 3 or 4 digit number.");
        }

        _logger.LogInformation("Validation completed with {ErrorCount} error(s).", errors.Count);

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    private static bool IsValidCardNumber(int cardNumber)
    {
        var cardNumberString = cardNumber.ToString();
        return cardNumberString.Length >= 13 && cardNumberString.Length <= 19;
    }

    private static bool IsValidExpiryDate(int month, int year)
    {
        var now = DateTime.UtcNow;
        return year > now.Year || (year == now.Year && month > now.Month);
    }

    private static bool IsValidCurrency(string currency)
    {
        return _acceptedCurrencies.Contains(currency);
    }

    private static bool IsValidAmount(int amount)
    {
        return amount > 0;
    }

    private static bool IsValidCvv(int cvv)
    {
        var cvvString = cvv.ToString();
        return cvvString.Length == 3 || cvvString.Length == 4;
    }
}