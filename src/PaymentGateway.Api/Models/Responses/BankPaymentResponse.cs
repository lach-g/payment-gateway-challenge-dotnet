
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PaymentGateway.Api.Models.Responses;

public class BankPaymentResponse
{
    [Required]
    [JsonPropertyName("authorized")]
    public required bool Authorized { get; init; }

    [JsonPropertyName("authorization_code")]
    public string? AuthorizationCode { get; init; }
}