using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

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
    (bool Found, PostPaymentResponse? Payment) Get(Guid id);
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

    /// <summary>
    /// Validates the payment request and if valid forwards it to the bank for processing.
    /// The resulting payment is stored in the repository on success.
    /// </summary>
    /// <param name="request">The payment request containing transaction request details.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A PaymentResult describing the processing outcome.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown if the bank client returns an unrecognised <see cref="BankResultStatus"/> value.</exception>
    public async Task<PaymentResult> ProcessAsync(PostPaymentRequest request, CancellationToken cancellationToken = default)
    {
        // Validate
        var validationResult = _paymentValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Payment validation failed for card ending in {CardNumberLastFour}. Errors: {Errors}", request.CardNumber[^4..] ?? "????", validationResult.Errors);
            return new PaymentResult
            {
                Outcome = PaymentOutcome.ValidationError,
                ValidationErrors = validationResult.Errors
            };
        }

        // Map payment request to bank request
        var bankRequest = BankPaymentRequest.From(request);

        // Call bank
        var bankResult = await _bankClient.SendPaymentAsync(bankRequest, cancellationToken);

        var outcome = bankResult.Status switch
        {
            BankResultStatus.Success => bankResult.Response!.Authorized ? PaymentOutcome.Authorized : PaymentOutcome.Declined,
            BankResultStatus.BankRejected => PaymentOutcome.BankRejected,
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
            return new PaymentResult { Outcome = PaymentOutcome.UnexpectedError, ErrorMessage = "Failed to add payment response to repository" };
        }

        return new PaymentResult
        {
            Outcome = outcome,
            Payment = paymentResponse
        };
    }

    /// <summary>
    /// Attempts to retrieve a previously processed payment from the repository by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the payment to retrieve.</param>
    /// <returns>
    /// A  tuple where Found is true and Payment contains the payment details when the ID exists;
    /// otherwise Found is false and Payment is null.
    /// </returns>
    public (bool Found, PostPaymentResponse? Payment) Get(Guid id)
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
