using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public interface IPaymentsRepository
{
    void Add(PostPaymentResponse payment);
    bool TryGet(Guid id, [NotNullWhen(true)] out PostPaymentResponse? payment);
}

public class PaymentsRepository : IPaymentsRepository
{
    private readonly ConcurrentDictionary<Guid, PostPaymentResponse> _payments = new();
    
    /// <summary>
    /// Adds a card payment to the repository.
    /// </summary>
    /// <param name="payment"></param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public void Add(PostPaymentResponse payment)
    {
        if (payment == null)
        {
            throw new ArgumentNullException(nameof(payment), "Payment cannot be null.");
        }

        if (payment.Id == Guid.Empty)
        {
            throw new ArgumentException("Payment ID cannot be empty.", nameof(payment.Id));
        } 

        _payments[payment.Id] = payment;
    }

    /// <summary>
    /// Tries to retrieve a card payment from the repository by its ID.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="payment"></param>
    /// <returns></returns>
    public bool TryGet(Guid id, [NotNullWhen(true)] out PostPaymentResponse? payment)
    {
        return _payments.TryGetValue(id, out payment);
    }
}