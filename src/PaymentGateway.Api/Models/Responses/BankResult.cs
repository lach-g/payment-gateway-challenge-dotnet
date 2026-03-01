namespace PaymentGateway.Api.Models.Responses;

public class BankResult
{
    public BankPaymentResponse? Response { get; }
    public BankResultStatus Status { get; }
    public string? ErrorMessage { get; }

    private BankResult(BankResultStatus status, BankPaymentResponse? response = null, string? errorMessage = null)
    {
        Status = status;
        Response = response;
        ErrorMessage = errorMessage;
    }

    public static BankResult Success(BankPaymentResponse response)
    {
        return new BankResult(BankResultStatus.Success, response);
    }

    public static BankResult ValidationError(string message)
    {
        return new BankResult(BankResultStatus.ValidationError, errorMessage: message);
    }

    public static BankResult ServiceUnavailable()
    {
        return new BankResult(BankResultStatus.ServiceUnavailable, errorMessage: "Bank is unavailable.");
    }
    
    public static BankResult UnexpectedError(string message)
    {
        return new BankResult(BankResultStatus.UnexpectedError, errorMessage: message);
    }
}

public enum BankResultStatus
{
    Success,
    ValidationError,
    ServiceUnavailable,
    UnexpectedError
}