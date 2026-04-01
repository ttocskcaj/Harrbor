using FakeItEasy;
using FluentAssertions;
using Harrbor.Configuration;
using Harrbor.Services;
using Harrbor.Services.Clients;
using Harrbor.Tests.Helpers;
using Xunit;

namespace Harrbor.Tests.Services;

public class MediaServiceResolverTests
{
    private readonly ISonarrClient _sonarrClient;
    private readonly IRadarrClient _radarrClient;
    private readonly MediaServiceResolver _resolver;

    public MediaServiceResolverTests()
    {
        _sonarrClient = A.Fake<ISonarrClient>();
        _radarrClient = A.Fake<IRadarrClient>();
        _resolver = new MediaServiceResolver(_sonarrClient, _radarrClient);
    }

    [Fact]
    public void GetServiceForJob_WhenServiceTypeSonarr_ReturnsSonarrClient()
    {
        // Arrange
        var job = new JobDefinitionBuilder()
            .WithServiceType(ServiceType.Sonarr)
            .Build();

        // Act
        var result = _resolver.GetServiceForJob(job);

        // Assert
        result.Should().BeSameAs(_sonarrClient);
    }

    [Fact]
    public void GetServiceForJob_WhenServiceTypeRadarr_ReturnsRadarrClient()
    {
        // Arrange
        var job = new JobDefinitionBuilder()
            .WithServiceType(ServiceType.Radarr)
            .Build();

        // Act
        var result = _resolver.GetServiceForJob(job);

        // Assert
        result.Should().BeSameAs(_radarrClient);
    }

    [Fact]
    public void GetServiceForJob_WhenUnknownServiceType_ThrowsArgumentException()
    {
        // Arrange
        var job = new JobDefinitionBuilder().Build();
        // Force an invalid service type using reflection or a cast
        var invalidJob = new JobDefinition
        {
            Name = "test",
            ServiceType = (ServiceType)999
        };

        // Act
        var act = () => _resolver.GetServiceForJob(invalidJob);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Unknown service type*");
    }

    [Fact]
    public void GetServiceForJob_ReturnsSameInstanceForMultipleCalls()
    {
        // Arrange
        var job = new JobDefinitionBuilder()
            .WithServiceType(ServiceType.Sonarr)
            .Build();

        // Act
        var result1 = _resolver.GetServiceForJob(job);
        var result2 = _resolver.GetServiceForJob(job);

        // Assert
        result1.Should().BeSameAs(result2);
    }

    [Fact]
    public void GetServiceForJob_DifferentJobs_CanReturnDifferentServices()
    {
        // Arrange
        var sonarrJob = new JobDefinitionBuilder()
            .WithName("sonarr-job")
            .WithServiceType(ServiceType.Sonarr)
            .Build();

        var radarrJob = new JobDefinitionBuilder()
            .WithName("radarr-job")
            .WithServiceType(ServiceType.Radarr)
            .Build();

        // Act
        var sonarrResult = _resolver.GetServiceForJob(sonarrJob);
        var radarrResult = _resolver.GetServiceForJob(radarrJob);

        // Assert
        sonarrResult.Should().BeSameAs(_sonarrClient);
        radarrResult.Should().BeSameAs(_radarrClient);
        sonarrResult.Should().NotBeSameAs(radarrResult);
    }
}
