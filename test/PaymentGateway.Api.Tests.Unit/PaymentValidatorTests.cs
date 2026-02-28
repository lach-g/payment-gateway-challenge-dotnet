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
        var request = CreateValidRequest();

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
        var request = CreateValidRequest(cardNumber: cardNumber);

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
        var (expiryMonth, expiryYear) = GenerateValidExpiryDate();
        var request = new PostPaymentRequest
        {
            CardNumber = invalidCardNumber!,
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Currency = GenerateRandomValidCurrency(),
            Amount = GenerateRandomValidAmount(),
            Cvv = GenerateRandomValidCvv(),
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
        var request = CreateValidRequest(expiryMonth: now.Month, expiryYear: now.Year);

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
        var (_, expiryYear) = GenerateValidExpiryDate();
        var request = new PostPaymentRequest
        {
            CardNumber = GenerateRandomValidCardNumber(),
            ExpiryMonth = invalidExpiryMonth,
            ExpiryYear = expiryYear,
            Currency = GenerateRandomValidCurrency(),
            Amount = GenerateRandomValidAmount(),
            Cvv = GenerateRandomValidCvv(),
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains($"{nameof(request.ExpiryMonth)}/{expiryYear}", result.Errors.Keys);
    }

    [Fact]
    public void Validate_InvalidExpiryYear_ReturnsInvalidResult()
    {
        // Arrange
        var invalidExpiryYear = DateTime.UtcNow.Year - 1;
        var (expiryMonth, _) = GenerateValidExpiryDate();
        var request = new PostPaymentRequest
        {
            CardNumber = GenerateRandomValidCardNumber(),
            ExpiryMonth = expiryMonth,
            ExpiryYear = invalidExpiryYear,
            Currency = GenerateRandomValidCurrency(),
            Amount = GenerateRandomValidAmount(),
            Cvv = GenerateRandomValidCvv(),
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains($"{nameof(request.ExpiryMonth)}/{invalidExpiryYear}", result.Errors.Keys);
    }

    #endregion

    #region Amount Validation Tests

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Validate_AmountBounds_ReturnsValidResult(int amount)
    {
        // Arrange
        var request = CreateValidRequest(amount: amount);

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
        var request = CreateValidRequest(amount: 0);

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
        var request = CreateValidRequest(cvv: cvv);

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
        var (expiryMonth, expiryYear) = GenerateValidExpiryDate();
        var request = new PostPaymentRequest
        {
            CardNumber = GenerateRandomValidCardNumber(),
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Currency = GenerateRandomValidCurrency(),
            Amount = GenerateRandomValidAmount(),
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

    #region helper methods

    private PostPaymentRequest CreateValidRequest(
        string? cardNumber = null,
        int? expiryMonth = null,
        int? expiryYear = null,
        AcceptedCurrency? currency = null,
        int? amount = null,
        string? cvv = null)
    {
        var (expiryMonthRand, expiryYearRand) = GenerateValidExpiryDate();

        return new PostPaymentRequest
        {
            CardNumber = cardNumber ?? GenerateRandomValidCardNumber(),
            ExpiryMonth = expiryMonth ?? expiryMonthRand,
            ExpiryYear = expiryYear ?? expiryYearRand,
            Currency = currency ?? GenerateRandomValidCurrency(),
            Amount = amount ?? _random.Next(0, int.MaxValue) + 1,
            Cvv = cvv ?? GenerateRandomValidCvv(),
        };
    }

    private int GenerateRandomValidAmount()
    {
        return _random.Next(0, int.MaxValue) + 1;
    }

    private AcceptedCurrency GenerateRandomValidCurrency()
    {
        var currencies = Enum.GetValues<AcceptedCurrency>();
        return currencies[_random.Next(currencies.Length)];
    }

    private string GenerateRandomValidCardNumber()
    {
        var length = _random.Next(PaymentValidationConstants.CardNumberMinLength, PaymentValidationConstants.CardNumberMaxLength + 1);
        return string.Concat(Enumerable.Range(0, length).Select(_ => _random.Next(10)).ToArray());
    }

    private string GenerateRandomValidCvv()
    {
        var length = _random.Next(PaymentValidationConstants.CvvMinLength, PaymentValidationConstants.CvvMaxLength + 1);
        return string.Concat(Enumerable.Range(0, length).Select(_ => _random.Next(10)).ToArray());
    }

    private (int ExpiryMonth, int ExpiryYear) GenerateValidExpiryDate()
    {
        var date = DateTime.UtcNow.AddMonths(_random.Next(0, 61));
        return (date.Month, date.Year);
    }

    #endregion

}
