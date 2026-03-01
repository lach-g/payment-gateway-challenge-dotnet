using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public interface IPaymentsRepository
{
    bool TryAdd(PostPaymentResponse payment);
    bool TryGet(Guid id, [NotNullWhen(true)] out PostPaymentResponse? payment);
}

public class PaymentsRepository : IPaymentsRepository
{
    private readonly ConcurrentDictionary<Guid, PostPaymentResponse> _payments = new();
    private readonly ILogger<PaymentsRepository> _logger;

    public PaymentsRepository(ILogger<PaymentsRepository> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Adds a card payment to the repository. The payment is stored in-memory and can be retrieved later by its ID.
    /// </summary>
    /// <param name="payment"></param>
    /// <returns>True if the payment was added successfully, false if a payment with the same ID already exists or if an error occurred.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the payment is null.</exception>
    public bool TryAdd(PostPaymentResponse payment)
    {
        ArgumentNullException.ThrowIfNull(payment, nameof(payment));

        try
        {
            return _payments.TryAdd(payment.Id, payment);
        }
        catch (OverflowException ex)
        {
            _logger.LogError(ex, "Failed to add payment with ID {PaymentId} to repository due to overflow.", payment.Id);
            return false;
        }
    }

    /// <summary>
    /// Tries to retrieve a card payment from the repository by its ID.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="payment"></param>
    /// <returns>True if a payment with the given ID exists in the repository, false otherwise.</returns>
    public bool TryGet(Guid id, [NotNullWhen(true)] out PostPaymentResponse? payment)
    {
        return _payments.TryGetValue(id, out payment);
    }
}