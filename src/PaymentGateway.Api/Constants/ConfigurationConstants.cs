namespace PaymentGateway.Api.Constants;

public static class ConfigurationDefaults
{
    public static class BankApi
    {
        public const string BaseUrl = "http://localhost:8080";
        public const string PaymentEndpoint = "/payments";
        public const int TimeoutSeconds = 30;

        public static class RetryPolicy
        {
            public const int MaxRetries = 3;
            public const int BaseDelayMilliseconds = 200;
        }
    }
}