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

    public static BankResult BankRejected(string message)
    {
        return new BankResult(BankResultStatus.BankRejected, errorMessage: message);
    }

    public static BankResult BankUnavailable()
    {
        return new BankResult(BankResultStatus.BankUnavailable, errorMessage: "Bank is unavailable.");
    }
    
    public static BankResult UnexpectedError(string message)
    {
        return new BankResult(BankResultStatus.UnexpectedError, errorMessage: message);
    }
}

public enum BankResultStatus
{
    Success,
    BankRejected,
    BankUnavailable,
    UnexpectedError
}