using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Xunit;

namespace PaymentGateway.Api.Tests.Acceptance;

public class PaymentsTests : IClassFixture<DockerEnvironmentFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public PaymentsTests(DockerEnvironmentFixture fixture)
    {
        _client = new HttpClient { BaseAddress = new Uri(fixture.GatewayUrl) };
    }

    [Fact]
    public async Task PostPayment_AuthorizedCard_ReturnsCreatedWithAuthorizedStatus()
    {
        // Arrange
        var future = DateTime.UtcNow.AddYears(1);
        var request = new
        {
            CardNumber = "11111111111111", // Bank simulator authorizes cards ending in an odd digit
            ExpiryMonth = future.Month,
            ExpiryYear = future.Year,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var response = await _client.PostAsJsonAsync("payments", request, JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result!.Id);
        Assert.Equal("Authorized", result.Status);
        Assert.Equal("1111", result.CardNumberLastFour);
        Assert.Equal(future.Month, result.ExpiryMonth);
        Assert.Equal(future.Year, result.ExpiryYear);
        Assert.Equal("GBP", result.Currency);
        Assert.Equal(100, result.Amount);
    }

    [Fact]
    public async Task PostPayment_DeclinedCard_ReturnsCreatedWithDeclinedStatus()
    {
        // Arrange
        var future = DateTime.UtcNow.AddYears(1);
        var request = new
        {
            CardNumber = "11111111111112", // Bank simulator declines cards ending in an even digit
            ExpiryMonth = future.Month,
            ExpiryYear = future.Year,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var response = await _client.PostAsJsonAsync("payments", request, JsonOptions);
        var result = await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result!.Id);
        Assert.Equal("Declined", result.Status);
        Assert.Equal("1112", result.CardNumberLastFour);
        Assert.Equal(future.Month, result.ExpiryMonth);
        Assert.Equal(future.Year, result.ExpiryYear);
        Assert.Equal("GBP", result.Currency);
        Assert.Equal(100, result.Amount);
    }

    [Fact]
    public async Task PostPayment_ThenGetById_ReturnsMatchingPayment()
    {
        // Arrange
        var future = DateTime.UtcNow.AddYears(1);
        var request = new
        {
            CardNumber = "11111111111111",
            ExpiryMonth = future.Month,
            ExpiryYear = future.Year,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act - create a payment
        var postResponse = await _client.PostAsJsonAsync("payments", request, JsonOptions);
        var created = await postResponse.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        // Act - retrieve the payment
        var getResponse = await _client.GetAsync($"payments/{created!.Id}");
        var retrieved = await getResponse.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal(request.CardNumber[^4..], created.CardNumberLastFour);
        Assert.Equal(request.ExpiryMonth, created.ExpiryMonth);
        Assert.Equal(request.ExpiryYear, created.ExpiryYear);
        Assert.Equal(request.Currency, created.Currency);
        Assert.Equal(request.Amount, created.Amount);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(retrieved);
        Assert.Equal(request.CardNumber[^4..], retrieved!.CardNumberLastFour);
        Assert.Equal(request.ExpiryMonth, retrieved.ExpiryMonth);
        Assert.Equal(request.ExpiryYear, retrieved.ExpiryYear);
        Assert.Equal(request.Currency, retrieved.Currency);
        Assert.Equal(request.Amount, retrieved.Amount);

        Assert.Equal(created.Id, retrieved.Id);
        Assert.Equal(created.Status, retrieved.Status);
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
    public async Task PostPayment_InvalidCardNumber_ReturnsBadRequest()
    {
        // Arrange
        var future = DateTime.UtcNow.AddYears(1);
        var request = new
        {
            CardNumber = "invalid card number",
            ExpiryMonth = future.Month,
            ExpiryYear = future.Year,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };

        // Act
        var response = await _client.PostAsJsonAsync("payments", request, JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public class PaymentResponse
{
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public string CardNumberLastFour { get; init; } = string.Empty;
    public int ExpiryMonth { get; init; }
    public int ExpiryYear { get; init; }
    public string Currency { get; init; } = string.Empty;
    public int Amount { get; init; }
}
