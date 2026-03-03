namespace PaymentGateway.Api.Models.Responses;

/// <summary>
/// Merchant-facing response after processing a card payment that resulted in an error.
/// </summary>
public class ErrorResponse
{
    public required string Message { get; set; }
    public Dictionary<string, string>? KeyValuePairs { get; set; }
}