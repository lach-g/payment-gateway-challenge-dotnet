namespace PaymentGateway.Api.Models.Requests;

/// <summary>
/// Merchant-facing request to process a card payment.
/// </summary>
public class PostPaymentRequest
{
    public required string CardNumber { get; init; }
    public required int ExpiryMonth { get; init; }
    public required int ExpiryYear { get; init; }
    public required AcceptedCurrency Currency { get; init; }
    public required int Amount { get; init; }
    public required string Cvv { get; init; }
}