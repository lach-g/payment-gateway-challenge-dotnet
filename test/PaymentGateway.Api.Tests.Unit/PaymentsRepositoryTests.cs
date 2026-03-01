using Microsoft.Extensions.Logging;

using Moq;

using PaymentGateway.Api.Services;

using Xunit;

namespace PaymentGateway.Api.Tests.Unit;

public class PaymentsRepositoryTests
{
    private readonly Mock<ILogger<PaymentsRepository>> _loggerMock = new();
    private readonly PaymentsRepository _repository;

    public PaymentsRepositoryTests()
    {
        _repository = new PaymentsRepository(_loggerMock.Object);
    }

    [Fact]
    public void TryAdd_WithValidPayment_ReturnsTrue()
    {
        // Arrange
        var payment = ValuesGenerator.GenerateRandomValidPostPaymentResponse();

        // Act
        var result = _repository.TryAdd(payment);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TryAdd_WithNullPayment_ReturnsFalse()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _repository.TryAdd(null!));
    }

    [Fact]
    public void TryAdd_WithDuplicatePayment_ReturnsFalse()
    {
        // Arrange
        var payment = ValuesGenerator.GenerateRandomValidPostPaymentResponse();
        _repository.TryAdd(payment);

        // Act
        var result = _repository.TryAdd(payment);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryGet_WithExistingId_ReturnsTrueAndPayment()
    {
        // Arrange
        var payment = ValuesGenerator.GenerateRandomValidPostPaymentResponse();
        _repository.TryAdd(payment);

        // Act
        var result = _repository.TryGet(payment.Id, out var retrieved);

        // Assert
        Assert.True(result);
        Assert.NotNull(retrieved);
        Assert.Equal(payment.Id, retrieved.Id);
        Assert.Equal(payment.Status, retrieved.Status);
        Assert.Equal(payment.CardNumberLastFour, retrieved.CardNumberLastFour);
        Assert.Equal(payment.ExpiryMonth, retrieved.ExpiryMonth);
        Assert.Equal(payment.ExpiryYear, retrieved.ExpiryYear);
        Assert.Equal(payment.Currency, retrieved.Currency);
        Assert.Equal(payment.Amount, retrieved.Amount);
    }

    [Fact]
    public void TryGet_WithNonExistentId_ReturnsFalseAndNull()
    {
        // Act
        var result = _repository.TryGet(Guid.NewGuid(), out var retrieved);

        // Assert
        Assert.False(result);
        Assert.Null(retrieved);
    }

}
