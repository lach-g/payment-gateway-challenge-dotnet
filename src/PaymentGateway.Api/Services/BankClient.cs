using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Options;

using PaymentGateway.Api.Models;

using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public interface IBankClient
{
    Task<BankResult> SendPaymentAsync(BankPaymentRequest request, CancellationToken cancellationToken = default);
}

public class BankClient : IBankClient
{
    private readonly ILogger<BankClient> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _paymentEndpoint;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public BankClient(ILogger<BankClient> logger, HttpClient httpClient, IOptions<BankClientOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger;
        _httpClient = httpClient;
        _paymentEndpoint = options.Value.PaymentEndpoint;
    }

    /// <summary>
    /// Sends a payment request to the bank and returns the result.
    /// </summary>
    /// <param name="request">The bank-formatted payment request.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A BankResult. HTTP communication failures and unexpected responses are captured and 
    /// returned as BankResultStatus.UnexpectedError rather than thrown.
    /// </returns>
    public async Task<BankResult> SendPaymentAsync(BankPaymentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending payment request to bank. Card (last four): {CardNumberLastFour} Expiry: {Expiry} Amount: {Amount} Currency: {Currency}",
            request.CardNumber[^4..],
            request.ExpiryDate,
            request.Amount,
            request.Currency);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(_paymentEndpoint, request, JsonOptions, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to communicate with bank.");
            return BankResult.UnexpectedError("Failed to communicate with bank.");
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    try
                    {
                        var bankResponse = await response.Content.ReadFromJsonAsync<BankPaymentResponse>(JsonOptions, cancellationToken);
                        if (bankResponse == null)
                        {
                            _logger.LogError("Bank response content was null.");
                            return BankResult.UnexpectedError("Bank response content was null.");
                        }
                        return BankResult.Success(bankResponse);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Failed to deserialize bank response.");
                        return BankResult.UnexpectedError("Failed to deserialize bank response.");
                    }
                case HttpStatusCode.BadRequest:
                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Bank rejected request: {Error}", error);
                    return BankResult.BankRejected(error);

                case HttpStatusCode.ServiceUnavailable:
                    _logger.LogWarning("Bank returned 503");
                    return BankResult.BankUnavailable();

                default:
                    _logger.LogError("Unexpected status code from bank: {StatusCode}", response.StatusCode);
                    return BankResult.UnexpectedError($"Unexpected status: {response.StatusCode}");
            }
        }
    }
}
