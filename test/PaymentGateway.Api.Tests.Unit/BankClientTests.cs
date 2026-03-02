using System.Net;
using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using PaymentGateway.Api.Constants;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

using PaymentGateway.Api.Tests.Common;

using Xunit;

namespace PaymentGateway.Api.Tests.Unit;

public class BankClientTests
{
    private readonly Mock<ILogger<BankClient>> _loggerMock = new();
    private readonly IOptions<BankClientOptions> _options = Options.Create(new BankClientOptions());

    [Fact]
    public async Task ProcessPaymentAsync_BankReturnsAuthorized_ReturnsAuthorizedResult()
    {
        // Arrange
        var content = $$"""{"authorized": true, "authorization_code": "{{Guid.NewGuid()}}"}""";
        var response = CreateHttpResponse(HttpStatusCode.OK, content);
        var sut = CreateSut(response);

        // Act
        var result = await sut.SendPaymentAsync(ValuesGenerator.GenerateRandomValidBankPaymentRequest());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BankResultStatus.Success, result.Status);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.Response);
        Assert.True(result.Response.Authorized);
        Assert.False(string.IsNullOrEmpty(result.Response.AuthorizationCode));
    }

    [Fact]
    public async Task ProcessPaymentAsync_BankReturnsUnauthorized_ReturnsSuccessResultUnauthorizedResponse()
    {
        // Arrange
        var content = $$"""{"authorized": false}""";
        var response = CreateHttpResponse(HttpStatusCode.OK, content);
        var sut = CreateSut(response);

        // Act
        var result = await sut.SendPaymentAsync(ValuesGenerator.GenerateRandomValidBankPaymentRequest());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BankResultStatus.Success, result.Status);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.Response);
        Assert.False(result.Response.Authorized);
        Assert.Null(result.Response.AuthorizationCode);
    }

    [Fact]
    public async Task ProcessPaymentAsync_Http200InvalidJson_ReturnsErrorResult()
    {
        // Arrange
        var content = "invalid json";
        var response = CreateHttpResponse(HttpStatusCode.OK, content);
        var sut = CreateSut(response);

        // Act
        var result = await sut.SendPaymentAsync(ValuesGenerator.GenerateRandomValidBankPaymentRequest());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BankResultStatus.UnexpectedError, result.Status);
        Assert.Null(result.Response);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessPaymentAsync_Http200NullContent_ReturnsErrorResult()
    {
        // Arrange
        var content = "null";
        var response = CreateHttpResponse(HttpStatusCode.OK, content);
        var sut = CreateSut(response);

        // Act
        var result = await sut.SendPaymentAsync(ValuesGenerator.GenerateRandomValidBankPaymentRequest());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BankResultStatus.UnexpectedError, result.Status);
        Assert.Null(result.Response);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessPaymentAsync_BadRequest_ReturnsBankRejectedStatus()
    {
        // Arrange
        var response = CreateHttpResponse(HttpStatusCode.BadRequest);
        var sut = CreateSut(response);

        // Act
        var result = await sut.SendPaymentAsync(ValuesGenerator.GenerateRandomValidBankPaymentRequest());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BankResultStatus.BankRejected, result.Status);
        Assert.Null(result.Response);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessPaymentAsync_BankReturnsBankUnavailable_ReturnsUnavailableResult()
    {
        // Arrange
        var response = CreateHttpResponse(HttpStatusCode.ServiceUnavailable);
        var sut = CreateSut(response);

        // Act
        var result = await sut.SendPaymentAsync(ValuesGenerator.GenerateRandomValidBankPaymentRequest());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BankResultStatus.BankUnavailable, result.Status);
        Assert.NotNull(result.ErrorMessage);
    }

    public IBankClient CreateSut(HttpResponseMessage response)
    {
        var handler = new StubHttpMessageHandler(response);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(ConfigurationDefaults.BankApi.BaseUrl) };
        return new BankClient(_loggerMock.Object, httpClient, _options);
    }

    public HttpResponseMessage CreateHttpResponse(HttpStatusCode statusCode, string? content = null)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content ?? "", Encoding.UTF8, "application/json")
        };
    }

    public class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public StubHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }
}
