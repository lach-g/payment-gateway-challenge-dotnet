using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Tests.Common;

namespace PaymentGateway.Api.Tests.Integration;

public class PaymentsControllerTests
{
    private readonly Random _random = new();
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true
    };

    private readonly Mock<IBankClient> _bankClientMock = new();
    private readonly IPaymentsRepository _paymentsRepository;

    public PaymentsControllerTests()
    {
        _paymentsRepository = new PaymentsRepository(
            new LoggerFactory().CreateLogger<PaymentsRepository>());

        _client = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Override the real bank client and repository with test doubles
                    services.AddSingleton(_paymentsRepository);
                    services.AddSingleton(_bankClientMock.Object);
                });
            })
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost/api/v1.0/")
            });
    }

    #region GET /Payments/{id}

    [Fact]
    public async Task GetPayment_StoredPayment_ReturnsOk()
    {
        // Arrange
        var payment = ValuesGenerator.GenerateRandomValidPostPaymentResponse();
        _paymentsRepository.TryAdd(payment);
        Assert.True(_paymentsRepository.TryGet(payment.Id, out _), "Failed to store payment in repository for test setup.");

        // Act
        var response = await _client.GetAsync($"payments/{payment.Id}");
        var result = await response.Content.ReadFromJsonAsync<PostPaymentResponse>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(payment.Id, result!.Id);
        Assert.Equal(payment.Status, result.Status);
        Assert.Equal(payment.CardNumberLastFour, result.CardNumberLastFour);
        Assert.Equal(payment.ExpiryMonth, result.ExpiryMonth);
        Assert.Equal(payment.ExpiryYear, result.ExpiryYear);
        Assert.Equal(payment.Currency, result.Currency);
        Assert.Equal(payment.Amount, result.Amount);
    }

    [Fact]
    public async Task GetPayment_UnknownId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"payments/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPayment_EmptyGuid_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync($"payments/{Guid.Empty}");

        // Assert   
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region POST /Payments

    [Fact]
    public async Task PostPayment_BankAuthorizes_ReturnsCreatedWithAuthorizedStatus()
    {
        // Arrange
        var request = ValuesGenerator.GenerateRandomValidPostPaymentRequest();
        _bankClientMock
            .Setup(x => x.SendPaymentAsync(It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BankResult.Success(new BankPaymentResponse
            {
                Authorized = true,
                AuthorizationCode = Guid.NewGuid().ToString(),

            }));

        // Act
        var response = await _client.PostAsJsonAsync("payments", request, JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<PostPaymentResponse>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(PaymentStatus.Authorized, result!.Status);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(request.CardNumber[^4..], result.CardNumberLastFour);
        Assert.Equal(request.ExpiryMonth, result.ExpiryMonth);
        Assert.Equal(request.ExpiryYear, result.ExpiryYear);
        Assert.Equal(request.Currency, result.Currency);
        Assert.Equal(request.Amount, result.Amount);
    }

    [Fact]
    public async Task PostPayment_BankDeclines_ReturnsCreatedWithDeclinedStatus()
    {
        // Arrange
        var request = ValuesGenerator.GenerateRandomValidPostPaymentRequest();
        _bankClientMock
            .Setup(x => x.SendPaymentAsync(It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BankResult.Success(new BankPaymentResponse { Authorized = false }));

        // Act
        var response = await _client.PostAsJsonAsync("payments", request, JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<PostPaymentResponse>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(PaymentStatus.Declined, result!.Status);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(request.CardNumber[^4..], result.CardNumberLastFour);
        Assert.Equal(request.ExpiryMonth, result.ExpiryMonth);
        Assert.Equal(request.ExpiryYear, result.ExpiryYear);
        Assert.Equal(request.Currency, result.Currency);
        Assert.Equal(request.Amount, result.Amount);
    }

    [Fact]
    public async Task PostPayment_InvalidRequest_ReturnsBadRequestAndDoesNotCallBank()
    {
        // Arrange
        var request = ValuesGenerator.GeneratePostPaymentRequest(cardNumber: "invalid card number");

        // Act
        var response = await _client.PostAsJsonAsync("payments", request, JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(result);
        Assert.NotNull(result!.KeyValuePairs);
        Assert.False(string.IsNullOrWhiteSpace(result!.KeyValuePairs!["CardNumber"]));
        _bankClientMock.Verify(
            x => x.SendPaymentAsync(It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PostPayment_BankRejected_Returns400()
    {
        // Arrange
        var request = ValuesGenerator.GenerateRandomValidPostPaymentRequest();
        _bankClientMock
            .Setup(x => x.SendPaymentAsync(It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BankResult.BankRejected("Payment request was rejected by the bank"));

        // Act
        var response = await _client.PostAsJsonAsync("payments", request, JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(result);
        Assert.NotNull(result!.KeyValuePairs);
        Assert.Equal("BankRejected", result!.KeyValuePairs!["Outcome"]);
    }


    [Fact]
    public async Task PostPayment_BankUnavailable_Returns502()
    {
        // Arrange
        var request = ValuesGenerator.GenerateRandomValidPostPaymentRequest();
        _bankClientMock
            .Setup(x => x.SendPaymentAsync(It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BankResult.BankUnavailable());

        // Act
        var response = await _client.PostAsJsonAsync("payments", request, JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.NotNull(result);
        Assert.NotNull(result!.KeyValuePairs);
        Assert.Equal("BankUnavailable", result!.KeyValuePairs!["Outcome"]);
    }

    [Fact]
    public async Task PostPayment_BankUnexpectedError_Returns500()
    {
        // Arrange
        var request = ValuesGenerator.GenerateRandomValidPostPaymentRequest();
        _bankClientMock
            .Setup(x => x.SendPaymentAsync(It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BankResult.UnexpectedError("Unexpected error occurred"));

        // Act
        var response = await _client.PostAsJsonAsync("payments", request, JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.NotNull(result);
        Assert.NotNull(result!.KeyValuePairs);
        Assert.Equal("UnexpectedError", result!.KeyValuePairs!["Outcome"]);
    }

    [Fact]
    public async Task PostPayment_AuthorizedPayment_IsRetrievableViaGet()
    {
        // Arrange
        var request = ValuesGenerator.GenerateRandomValidPostPaymentRequest();
        _bankClientMock
            .Setup(x => x.SendPaymentAsync(It.IsAny<BankPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BankResult.Success(new BankPaymentResponse
            {
                Authorized = true,
                AuthorizationCode = Guid.NewGuid().ToString()
            }));

        // Act
        var postResponse = await _client.PostAsJsonAsync("payments", request, JsonOptions);
        var postResult = await postResponse.Content.ReadFromJsonAsync<PostPaymentResponse>(JsonOptions);

        var getResponse = await _client.GetAsync($"Payments/{postResult!.Id}");
        var getResult = await getResponse.Content.ReadFromJsonAsync<PostPaymentResponse>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        Assert.NotNull(postResult);
        Assert.Equal(PaymentStatus.Authorized, postResult!.Status);
        Assert.NotEqual(Guid.Empty, postResult.Id);
        Assert.Equal(request.CardNumber[^4..], postResult.CardNumberLastFour);
        Assert.Equal(request.ExpiryMonth, postResult.ExpiryMonth);
        Assert.Equal(request.ExpiryYear, postResult.ExpiryYear);
        Assert.Equal(request.Currency, postResult.Currency);
        Assert.Equal(request.Amount, postResult.Amount);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(getResult);
        Assert.Equal(postResult.Id, getResult!.Id);
        Assert.Equal(PaymentStatus.Authorized, getResult.Status);
        Assert.Equal(request.CardNumber[^4..], getResult.CardNumberLastFour);
        Assert.Equal(request.ExpiryMonth, getResult.ExpiryMonth);
        Assert.Equal(request.Currency, getResult.Currency);
        Assert.Equal(request.ExpiryYear, getResult.ExpiryYear);
        Assert.Equal(request.Currency, getResult.Currency);
        Assert.Equal(request.Amount, getResult.Amount);
    }

    #endregion 
}