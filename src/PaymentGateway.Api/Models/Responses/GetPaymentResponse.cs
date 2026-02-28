namespace PaymentGateway.Api.Models.Responses;

/// <summary>
/// Merchant-facing response after retrieving a card payment's details.
/// </summary>
public class GetPaymentResponse
{
    public required Guid Id { get; init; }
    public required PaymentStatus Status { get; init; }
    public required string CardNumberLastFour { get; init; }
    public required int ExpiryMonth { get; init; }
    public required int ExpiryYear { get; init; }
    public required AcceptedCurrency Currency { get; init; }
    public required int Amount { get; init; }
}