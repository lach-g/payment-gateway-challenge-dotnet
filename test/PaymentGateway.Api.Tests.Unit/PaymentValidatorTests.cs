using PaymentGateway.Api.Services;
using PaymentGateway.Api.Models.Requests;
using Moq;
using Microsoft.Extensions.Logging;
using Xunit;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Constants;

namespace PaymentGateway.Api.Tests.Unit;

public class PaymentValidatorTests
{
    private readonly Random _random = new();
    private readonly PaymentValidator _validator;

    public PaymentValidatorTests()
    {
        var loggerMock = new Mock<ILogger<PaymentValidator>>();
        _validator = new PaymentValidator(loggerMock.Object);
    }

    [Fact]
    public void Validate_ValidRequest_ReturnsValidResult()
    {
        // Arrange
        var request = ValuesGenerator.GenerateRandomValidPostPaymentRequest();

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    #region Card Number Validation Tests

    [Theory]
    [InlineData("11111111111111")]
    [InlineData("1111111111111111111")]
    public void Validate_CardNumberBounds_ReturnsValidResult(string cardNumber)
    {
        // Arrange
        var request = ValuesGenerator.GeneratePostPaymentRequest(cardNumber: cardNumber);

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1111111111111")]
    [InlineData("11111111111111111111")]
    [InlineData("aaaaaaaaaaaaaa")]
    public void Validate_InvalidCardNumber_ReturnsInvalidResult(string? invalidCardNumber)
    {
        // Arrange
        var (expiryMonth, expiryYear) = ValuesGenerator.GenerateRandomValidExpiryDates();
        var request = new PostPaymentRequest
        {
            CardNumber = invalidCardNumber!,
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Currency = ValuesGenerator.GenerateRandomValidCurrency(),
            Amount = ValuesGenerator.GenerateRandomValidAmount(),
            Cvv = ValuesGenerator.GenerateRandomValidCvv(),
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains(nameof(request.CardNumber), result.Errors.Keys);
    }

    #endregion

    #region Expiry Date Validation Tests

    [Fact]
    public void Validate_ExpiryDateLowerBound_ReturnsValidResult()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var request = ValuesGenerator.GeneratePostPaymentRequest(expiryMonth: now.Month, expiryYear: now.Year);

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Validate_InvalidExpiryMonth_ReturnsInvalidResult(int invalidExpiryMonth)
    {
        // Arrange
        var request = ValuesGenerator.GeneratePostPaymentRequest(expiryMonth: invalidExpiryMonth);

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains($"{nameof(request.ExpiryMonth)}/{request.ExpiryYear}", result.Errors.Keys);
    }

    [Fact]
    public void Validate_InvalidExpiryYear_ReturnsInvalidResult()
    {
        // Arrange
        var invalidExpiryYear = DateTime.UtcNow.Year - 1;
        var request = ValuesGenerator.GeneratePostPaymentRequest(expiryYear: invalidExpiryYear);

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains($"{nameof(request.ExpiryMonth)}/{request.ExpiryYear}", result.Errors.Keys);
    }

    #endregion

    #region Amount Validation Tests

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Validate_AmountBounds_ReturnsValidResult(int amount)
    {
        // Arrange
        var request = ValuesGenerator.GeneratePostPaymentRequest(amount: amount);

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_ZeroAmount_ReturnsInvalidResult()
    {
        // Arrange
        var request = ValuesGenerator.GeneratePostPaymentRequest(amount: 0);

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains(nameof(request.Amount), result.Errors.Keys);
    }

    #endregion

    #region CVV Validation Tests

    [Theory]
    [InlineData("123")]
    [InlineData("1234")]
    public void Validate_CvvBounds_ReturnsValidResult(string cvv)
    {
        // Arrange
        var request = ValuesGenerator.GeneratePostPaymentRequest(cvv: cvv);

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("12345")]
    [InlineData("abc")]
    public void Validate_InvalidCvv_ReturnsInvalidResult(string? invalidCvv)
    {
        // Arrange
        var (expiryMonth, expiryYear) = ValuesGenerator.GenerateRandomValidExpiryDates();
        var request = new PostPaymentRequest
        {
            CardNumber = ValuesGenerator.GenerateRandomValidCardNumber(),
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Currency = ValuesGenerator.GenerateRandomValidCurrency(),
            Amount = ValuesGenerator.GenerateRandomValidAmount(),
            Cvv = invalidCvv!,
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains(nameof(request.Cvv), result.Errors.Keys);
    }

    #endregion
}