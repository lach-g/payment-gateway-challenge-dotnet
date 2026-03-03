using System.Net;

using Microsoft.Extensions.Http.Resilience;

using PaymentGateway.Api.Services;

using Polly;

namespace PaymentGateway.Api;

public static class BankClientExtensions
{
    /// <summary>
    /// Registers the IBankClient implementation and its HTTP client with resilience policies
    /// sourced from configuration.
    /// </summary>
    /// <param name="services">The service collection to add the bank client to.</param>
    /// <param name="configuration">The application configuration used to bind.</param>
    /// <returns>The services instance to allow method chaining.</returns>
    public static IServiceCollection AddBankClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var bankClientOptions = configuration
            .GetSection(BankClientOptions.SectionName)
            .Get<BankClientOptions>() ?? new BankClientOptions();

        services.AddOptions<BankClientOptions>()
            .Bind(configuration.GetSection(BankClientOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<IBankClient, BankClient>(client =>
        {
            client.BaseAddress = new Uri(bankClientOptions.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(bankClientOptions.TimeoutSeconds);
        })
        .AddResilienceHandler("bank-retry", pipeline =>
        {
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = bankClientOptions.RetryPolicy.MaxRetries,
                Delay = TimeSpan.FromMilliseconds(bankClientOptions.RetryPolicy.BaseDelayMilliseconds),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = args => ValueTask.FromResult(
                    args.Outcome.Result?.StatusCode == HttpStatusCode.ServiceUnavailable)
            });
        });

        return services;
    }
}