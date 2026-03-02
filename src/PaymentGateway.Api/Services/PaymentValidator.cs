using PaymentGateway.Api.Constants;
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

    public PaymentValidator(ILogger<PaymentValidator> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
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
            errors.Add(nameof(request.CardNumber), $"Card number must be between {PaymentValidationConstants.CardNumberMinLength} and {PaymentValidationConstants.CardNumberMaxLength} digits.");
        }

        if (!IsValidExpiryDate(request.ExpiryMonth, request.ExpiryYear))
        {
            _logger.LogWarning("Validation failed: Card has expired.");
            errors.Add($"{nameof(request.ExpiryMonth)}/{request.ExpiryYear}", "Card has expired.");
        }

        if (!IsValidAmount(request.Amount))
        {
            _logger.LogWarning("Validation failed: Amount {Amount} is not valid.", request.Amount);
            errors.Add(nameof(request.Amount), "Amount must be greater than zero.");
        }

        if (!IsValidCvv(request.Cvv))
        {
            _logger.LogWarning("Validation failed: CVV is invalid.");
            errors.Add(nameof(request.Cvv), $"CVV must be a {PaymentValidationConstants.CvvMinLength} or {PaymentValidationConstants.CvvMaxLength} digit number.");
        }

        _logger.LogInformation("Validation completed with {ErrorCount} error(s).", errors.Count);

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    private static bool IsValidCardNumber(string cardNumber)
    {
        return !string.IsNullOrWhiteSpace(cardNumber) &&
            cardNumber.Length >= PaymentValidationConstants.CardNumberMinLength &&
            cardNumber.Length <= PaymentValidationConstants.CardNumberMaxLength &&
            cardNumber.All(char.IsDigit);
    }

    private static bool IsValidExpiryDate(int month, int year)
    {
        var now = DateTime.UtcNow;
        return month >= 1 &&
            month <= 12 && 
            (year > now.Year || (year == now.Year && month >= now.Month));
    }

    private static bool IsValidAmount(int amount)
    {
        return amount > 0;
    }

    private static bool IsValidCvv(string cvv)
    {
        return !string.IsNullOrWhiteSpace(cvv) &&
            (cvv.Length == PaymentValidationConstants.CvvMinLength || cvv.Length == PaymentValidationConstants.CvvMaxLength) &&
            cvv.All(char.IsDigit);
    }
}