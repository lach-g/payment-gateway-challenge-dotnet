using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;

using Xunit;

namespace PaymentGateway.Api.Tests.Acceptance;

public class DockerEnvironmentFixture : IAsyncLifetime
{
    private readonly INetwork _network;
    private readonly IContainer _bankSimulator;
    private readonly IContainer _paymentGateway;
    private readonly IFutureDockerImage _gatewayImage;

    public string GatewayUrl { get; private set; } = string.Empty;

    public DockerEnvironmentFixture()
    {
        var solutionDir = CommonDirectoryPath.GetSolutionDirectory().DirectoryPath;
        var impostersPath = Path.Combine(solutionDir, "imposters");

        _network = new NetworkBuilder()
            .WithName(Guid.NewGuid().ToString("D"))
            .Build();

        _bankSimulator = new ContainerBuilder()
            .WithImage("bbyars/mountebank:2.8.1")
            .WithNetwork(_network)
            .WithNetworkAliases("bank_simulator")
            .WithCommand("--configfile", "/imposters/bank_simulator.ejs", "--allowInjection")
            .WithBindMount(impostersPath, "/imposters", AccessMode.ReadOnly)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(2525))
            .Build();

        _gatewayImage = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(solutionDir)
            .WithDockerfile("Dockerfile")
            .Build();

        _paymentGateway = new ContainerBuilder()
            .WithImage(_gatewayImage)
            .WithNetwork(_network)
            .WithPortBinding(8080, true)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production")
            .WithEnvironment("BankApi__BaseUrl", "http://bank_simulator:8080")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(8080))
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _network.CreateAsync();

        await _gatewayImage.CreateAsync();

        await _bankSimulator.StartAsync();
        await _paymentGateway.StartAsync();

        var port = _paymentGateway.GetMappedPublicPort(8080);
        GatewayUrl = $"http://localhost:{port}/api/v1.0/";
    }

    public async Task DisposeAsync()
    {
        await _paymentGateway.DisposeAsync();
        await _bankSimulator.DisposeAsync();
        await _network.DeleteAsync();
        await _gatewayImage.DisposeAsync();
    }
}