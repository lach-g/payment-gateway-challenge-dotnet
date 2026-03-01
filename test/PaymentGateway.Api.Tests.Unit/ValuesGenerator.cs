using PaymentGateway.Api.Constants;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Tests.Unit;

public static class ValuesGenerator
{
    private static readonly Random Random = Random.Shared;

    public static string GenerateRandomValidCardNumber()
    {
        var length = Random.Next(PaymentValidationConstants.CardNumberMinLength, PaymentValidationConstants.CardNumberMaxLength + 1);
        return string.Concat(Enumerable.Range(0, length).Select(_ => Random.Next(10)).ToArray());
    }

    public static int GenerateRandomValidAmount()
    {
        return Random.Next(1, int.MaxValue);
    }

    public static AcceptedCurrency GenerateRandomValidCurrency()
    {
        var currencies = Enum.GetValues<AcceptedCurrency>();
        return currencies[Random.Next(currencies.Length)];
    }

    public static string GenerateRandomValidCvv()
    {
        var length = Random.Next(PaymentValidationConstants.CvvMinLength, PaymentValidationConstants.CvvMaxLength + 1);
        return string.Concat(Enumerable.Range(0, length).Select(_ => Random.Next(10)).ToArray());
    }

    public static (int ExpiryMonth, int ExpiryYear) GenerateRandomValidExpiryDates()
    {
        var date = DateTime.UtcNow.AddMonths(Random.Next(0, 61));
        return (date.Month, date.Year);
    }

    public static string GenerateValidExpiryDate()
    {
        var (month, year) = GenerateRandomValidExpiryDates();
        return $"{month:D2}/{year % 100:D2}";
    }

    public static BankPaymentRequest GenerateRandomValidBankPaymentRequest()
    {
        return new BankPaymentRequest
        {
            CardNumber = GenerateRandomValidCardNumber(),
            ExpiryDate = GenerateValidExpiryDate(),
            Currency = GenerateRandomValidCurrency(),
            Amount = GenerateRandomValidAmount(),
            Cvv = GenerateRandomValidCvv()
        };
    }

    public static PostPaymentRequest GenerateRandomValidPostPaymentRequest()
    {
        var (expiryMonth, expiryYear) = GenerateRandomValidExpiryDates();

        return new PostPaymentRequest
        {
            CardNumber = GenerateRandomValidCardNumber(),
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Currency = GenerateRandomValidCurrency(),
            Amount = GenerateRandomValidAmount(),
            Cvv = GenerateRandomValidCvv()
        };
    }

    public static PostPaymentRequest GeneratePostPaymentRequest(
        string? cardNumber = null,
        int? expiryMonth = null,
        int? expiryYear = null,
        AcceptedCurrency? currency = null,
        int? amount = null, 
        string? cvv = null
    )
    {
        var (randValidExpiryMonth, randValidExpiryYear) = GenerateRandomValidExpiryDates();

        return new PostPaymentRequest
        {
            CardNumber = cardNumber ?? GenerateRandomValidCardNumber(),
            ExpiryMonth = expiryMonth ?? randValidExpiryMonth,
            ExpiryYear = expiryYear ?? randValidExpiryYear,
            Currency = currency ?? GenerateRandomValidCurrency(),
            Amount = amount ?? GenerateRandomValidAmount(),
            Cvv = cvv ?? GenerateRandomValidCvv()
        };
    }

    public static PostPaymentResponse GenerateRandomValidPostPaymentResponse()
    {
        var (expiryMonth, expiryYear) = GenerateRandomValidExpiryDates();

        return new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = GenerateRandomValidCardNumber()[^4..],
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Currency = GenerateRandomValidCurrency(),
            Amount = GenerateRandomValidAmount()
        };
    }
}
