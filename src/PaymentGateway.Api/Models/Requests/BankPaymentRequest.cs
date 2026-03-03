using System.Text.Json.Serialization;

using PaymentGateway.Api.Enums;

namespace PaymentGateway.Api.Models.Requests;

public class BankPaymentRequest
{
    [JsonPropertyName("card_number")]
    public required string CardNumber { get; init; }

    [JsonPropertyName("expiry_date")]
    public required string ExpiryDate { get; init; }

    [JsonPropertyName("currency")]
    public required AcceptedCurrency Currency { get; init; }

    [JsonPropertyName("amount")]
    public required int Amount { get; init; }

    [JsonPropertyName("cvv")]
    public required string Cvv { get; init; }

    /// <summary>
    /// Creates a BankPaymentRequest from a PostPaymentRequest,
    /// </summary>
    /// <param name="request">The payment request to map from.</param>
    /// <returns>A BankPaymentRequest populated with the payment request details.</returns>
    public static BankPaymentRequest From(PostPaymentRequest request)
    {
        return new BankPaymentRequest
        {
            CardNumber = request.CardNumber,
            ExpiryDate = $"{request.ExpiryMonth:D2}/{request.ExpiryYear}",
            Currency = request.Currency,
            Amount = request.Amount,
            Cvv = request.Cvv
        };
    }
}
