namespace PaymentGateway.Api.Models.Requests;

/// <summary>
/// Merchant-facing request to process a card payment.
/// </summary>
public class PostPaymentRequest
{
    public required int CardNumber { get; init; }
    public required int ExpiryMonth { get; init; }
    public required int ExpiryYear { get; init; }
    public required string Currency { get; init; }
    public required int Amount { get; init; }
    public required int Cvv { get; init; }
}