using FluentAssertions;
using Harrbor.Data.Entities;
using Xunit;

namespace Harrbor.Tests.Unit;

public class JobEntityTests
{
    [Fact]
    public void ToDefinition_MapsAllPipelineFields()
    {
        var job = new Job
        {
            Id = 7,
            Name = "tv-shows",
            Enabled = true,
            MediaServiceConnectionId = 11,
            DownloadClientConnectionId = 22,
            RemoteStorageConnectionId = 33,
            PollingInterval = TimeSpan.FromSeconds(30),
            RemotePath = "/remote/downloads/tv",
            StagingPath = "/staging/tv",
            Category = "tv",
            Tags = ["harrbor", "auto"],
            TransferParallelism = 4,
            MaxTransferRetries = 5,
            TransferRetryDelay = TimeSpan.FromMinutes(2),
            CompletedCategory = "tv-done",
            ImportTimeout = TimeSpan.FromHours(12)
        };

        var definition = job.ToDefinition();

        definition.Name.Should().Be("tv-shows");
        definition.Enabled.Should().BeTrue();
        definition.MediaServiceConnectionId.Should().Be(11);
        definition.DownloadClientConnectionId.Should().Be(22);
        definition.RemoteStorageConnectionId.Should().Be(33);
        definition.PollingInterval.Should().Be(TimeSpan.FromSeconds(30));
        definition.RemotePath.Should().Be("/remote/downloads/tv");
        definition.StagingPath.Should().Be("/staging/tv");
        definition.Category.Should().Be("tv");
        definition.Tags.Should().Equal("harrbor", "auto");
        definition.TransferParallelism.Should().Be(4);
        definition.MaxTransferRetries.Should().Be(5);
        definition.TransferRetryDelay.Should().Be(TimeSpan.FromMinutes(2));
        definition.CompletedCategory.Should().Be("tv-done");
        definition.ImportTimeout.Should().Be(TimeSpan.FromHours(12));
    }
}
