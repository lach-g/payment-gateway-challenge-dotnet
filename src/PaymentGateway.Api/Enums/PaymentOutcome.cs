namespace PaymentGateway.Api.Enums;

public enum PaymentOutcome
{
    Authorized,
    Declined,
    ValidationError,
    BankRejected,
    BankUnavailable,
    UnexpectedError,
}