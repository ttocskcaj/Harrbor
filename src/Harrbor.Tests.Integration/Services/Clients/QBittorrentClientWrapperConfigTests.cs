using FakeItEasy;
using FluentAssertions;
using Harrbor.Services;
using Harrbor.Services.Clients;
using Harrbor.Services.Configuration;
using Harrbor.Tests.Integration.Helpers;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Harrbor.Tests.Integration.Services.Clients;

/// <summary>
/// Tests the real wrapper's behavior when connection config is absent from the store.
/// </summary>
public class QBittorrentClientWrapperConfigTests : IDisposable
{
    private readonly SqliteDbContextFactory _dbFactory = new();
    private readonly QBittorrentClientWrapper _wrapper;

    public QBittorrentClientWrapperConfigTests()
    {
        var store = new ConnectionStore(_dbFactory);
        _wrapper = new QBittorrentClientWrapper(store, A.Fake<ILogger<QBittorrentClientWrapper>>());
    }

    public void Dispose()
    {
        _wrapper.Dispose();
        _dbFactory.Dispose();
    }

    [Fact]
    public async Task GetTorrentListAsync_Unconfigured_ThrowsServiceNotConfigured()
    {
        var act = async () => await _wrapper.GetTorrentListAsync();

        await act.Should().ThrowAsync<ServiceNotConfiguredException>();
    }

    [Fact]
    public async Task TestConnectionAsync_Unconfigured_ReturnsFailureInsteadOfThrowing()
    {
        var result = await _wrapper.TestConnectionAsync();

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not configured");
    }
}
