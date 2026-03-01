using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests.Integration;

public class PaymentsControllerTests
{
    private readonly Random _random = new();
    private readonly HttpClient _client;
    private readonly ILogger<PaymentsRepository> _logger = new LoggerFactory().CreateLogger<PaymentsRepository>();
    private readonly IPaymentsRepository _paymentsRepository;

    public PaymentsControllerTests()
    {
        _paymentsRepository = new PaymentsRepository(_logger);
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        _client = webApplicationFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => ((ServiceCollection)services)
                .AddSingleton(_paymentsRepository)))
            .CreateClient(new() { BaseAddress = new Uri("https://localhost/api/v1.0/") });
    }
    
    [Fact]
    public async Task GetPaymentAsync_RequestStoredPaymentId_RespondsWithPayment()
    {
        // Arrange
        var payment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            ExpiryYear = _random.Next(DateTime.Now.Year + 1, DateTime.Now.Year + 10),
            ExpiryMonth = _random.Next(1, 13),
            Amount = _random.Next(0, int.MaxValue) + 1,
            CardNumberLastFour = string.Concat(Enumerable.Range(4, 4).Select(_ => _random.Next(10)).ToArray()),
            Currency = AcceptedCurrency.USD
        };
        _paymentsRepository.TryAdd(payment);

        // Act
        var response = await _client.GetAsync($"Payments/{payment.Id}");
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();
        
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(payment.Id, paymentResponse!.Id);
        Assert.Equal(payment.Status, paymentResponse.Status);
        Assert.Equal(payment.ExpiryYear, paymentResponse.ExpiryYear);
        Assert.Equal(payment.ExpiryMonth, paymentResponse.ExpiryMonth);
        Assert.Equal(payment.Amount, paymentResponse.Amount);
        Assert.Equal(payment.CardNumberLastFour, paymentResponse.CardNumberLastFour);
        Assert.Equal(payment.Currency, paymentResponse.Currency);
    }

    [Fact]
    public async Task GetPaymentAsync_RequestNonExistentPaymentId_RespondsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"Payments/{Guid.NewGuid()}");
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPaymentAsync_RequestEmptyGuid_RespondsWithBadRequest()
    {
        // Act
        var response = await _client.GetAsync($"Payments/{Guid.Empty}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}