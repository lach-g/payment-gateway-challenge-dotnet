using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public enum PaymentOutcome
{
    Authorized,
    Declined,
    ValidationError,
    BankUnavailable,
    UnexpectedError,
}

public class PaymentResult
{
    public required PaymentOutcome Outcome { get; init; }
    public PostPaymentResponse? Payment { get; init; }
    public Dictionary<string, string>? ValidationErrors { get; init; }
    public string? ErrorMessage { get; init; }
}

public interface IPaymentService
{
    Task<PaymentResult> ProcessAsync(PostPaymentRequest request, CancellationToken cancellationToken = default);
    Task<(bool Found, PostPaymentResponse? Payment)> GetAsync(Guid id, CancellationToken cancellationToken = default);
}

public class PaymentService : IPaymentService
{
    private readonly ILogger<PaymentService> _logger;
    private readonly IPaymentsRepository _paymentsRepository;
    private readonly IBankClient _bankClient;
    private readonly IPaymentValidator _paymentValidator;

    public PaymentService(ILogger<PaymentService> logger, IPaymentsRepository paymentsRepository, IBankClient bankClient, IPaymentValidator paymentValidator)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(paymentsRepository);
        ArgumentNullException.ThrowIfNull(bankClient);
        ArgumentNullException.ThrowIfNull(paymentValidator);

        _logger = logger;
        _paymentsRepository = paymentsRepository;
        _bankClient = bankClient;
        _paymentValidator = paymentValidator;
    }

    public async Task<PaymentResult> ProcessAsync(PostPaymentRequest request, CancellationToken cancellationToken = default)
    {
        // Validate
        var validationResult = _paymentValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Payment validation failed for card ending in {CardNumberLastFour}. Errors: {Errors}", request.CardNumber[^4..], validationResult.Errors);
            return new PaymentResult
            {
                Outcome = PaymentOutcome.ValidationError,
                ValidationErrors = validationResult.Errors
            };
        }

        // Map payment request to bank request
        var bankRequest = new BankPaymentRequest
        {
            CardNumber = request.CardNumber,
            // TODO: PostPaymentRequest to BankPaymentRequest mapping
            ExpiryDate = $"{request.ExpiryMonth:D2}/{request.ExpiryYear}",
            Amount = request.Amount,
            Currency = request.Currency,
            Cvv = request.Cvv
        };

        // Call bank
        var bankResult = await _bankClient.SendPaymentAsync(bankRequest, cancellationToken);

        var outcome = bankResult.Status switch
        {
            BankResultStatus.Success => bankResult.Response!.Authorized ? PaymentOutcome.Authorized : PaymentOutcome.Declined,
            BankResultStatus.BankRejected => PaymentOutcome.ValidationError,
            BankResultStatus.BankUnavailable => PaymentOutcome.BankUnavailable,
            BankResultStatus.UnexpectedError => PaymentOutcome.UnexpectedError,
            _ => throw new InvalidOperationException($"Unexpected bank result status: {bankResult.Status}")
        };

        if (bankResult.Status != BankResultStatus.Success)
        {
            _logger.LogError("Bank returned {Outcome} for card ending in {LastFour}: {ErrorMessage}", outcome, request.CardNumber[^4..], bankResult.ErrorMessage);
            return new PaymentResult { Outcome = outcome, ErrorMessage = bankResult.ErrorMessage };
        }

        var paymentResponse = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = outcome == PaymentOutcome.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            CardNumberLastFour = request.CardNumber[^4..],
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency,
            Amount = request.Amount,
        };

        if (!_paymentsRepository.TryAdd(paymentResponse))
        {
            _logger.LogError("Failed to add payment response to repository for card ending in {CardNumberLastFour}.", request.CardNumber[^4..]);
        }

        return new PaymentResult
        {
            Outcome = outcome,
            Payment = paymentResponse
        };
    }

    public async Task<(bool Found, PostPaymentResponse? Payment)> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_paymentsRepository.TryGet(id, out var payment))
        {
            _logger.LogInformation("Payment with ID {PaymentId} retrieved successfully.", id);
            return (true, payment);
        }
        else
        {
            _logger.LogWarning("Payment with ID {PaymentId} not found.", id);
            return (false, null);
        }
    }
}
