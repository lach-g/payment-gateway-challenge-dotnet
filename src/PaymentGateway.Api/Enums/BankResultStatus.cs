namespace PaymentGateway.Api.Enums;

public enum BankResultStatus
{
    Success,
    BankRejected,
    BankUnavailable,
    UnexpectedError
}