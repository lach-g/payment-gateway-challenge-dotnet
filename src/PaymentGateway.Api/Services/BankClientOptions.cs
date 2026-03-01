using System.ComponentModel.DataAnnotations;

using PaymentGateway.Api.Constants;

namespace PaymentGateway.Api.Services;

public class BankClientOptions
{
    public const string SectionName = "BankApi";

    [Required]
    public string BaseUrl { get; set; } = ConfigurationDefaults.BankApi.BaseUrl;

    [Required]
    public string PaymentEndpoint { get; set; } = ConfigurationDefaults.BankApi.PaymentEndpoint;

    [Required]
    public int TimeoutSeconds { get; set; } = ConfigurationDefaults.BankApi.TimeoutSeconds;

    [Required]
    public RetryPolicyOptions RetryPolicy { get; set; } = new();

    public class RetryPolicyOptions
    {
        [Required]
        public int MaxRetries { get; set; } = ConfigurationDefaults.BankApi.RetryPolicy.MaxRetries;

        [Required]
        public int BaseDelayMilliseconds { get; set; } = ConfigurationDefaults.BankApi.RetryPolicy.BaseDelayMilliseconds;
    }
}
