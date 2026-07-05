using FluentAssertions;
using Harrbor.Data.Entities;
using Harrbor.Services.Configuration;
using Harrbor.Tests.Integration.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Harrbor.Tests.Integration.Services.Configuration;

public class ConnectionStoreTests : IDisposable
{
    private readonly SqliteDbContextFactory _dbFactory = new();
    private readonly ConnectionStore _store;

    public ConnectionStoreTests()
    {
        _store = new ConnectionStore(_dbFactory);
    }

    public void Dispose() => _dbFactory.Dispose();

    private static Connection New(string providerKey = "sonarr", string name = "Sonarr", string json = "{}")
        => new() { ProviderKey = providerKey, Name = name, SettingsJson = json, Enabled = true };

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenAbsent()
        => (await _store.GetByIdAsync(999)).Should().BeNull();

    [Fact]
    public async Task CreateAsync_AssignsIdAndVersionOne()
    {
        var created = await _store.CreateAsync(New());

        created.Id.Should().BeGreaterThan(0);
        created.Version.Should().Be(1);
        (await _store.GetByIdAsync(created.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_AllowsManyConnectionsPerProvider()
    {
        var a = await _store.CreateAsync(New(name: "Main Sonarr"));
        var b = await _store.CreateAsync(New(name: "4K Sonarr"));

        a.Id.Should().NotBe(b.Id);
        (await _store.GetAllAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateAsync_BumpsVersionAndPersistsFields()
    {
        var created = await _store.CreateAsync(New(json: """{"baseUrl":"http://old"}"""));

        var updated = await _store.UpdateAsync(created.Id, "Renamed", enabled: false, """{"baseUrl":"http://new"}""");

        updated!.Version.Should().Be(2);
        updated.Name.Should().Be("Renamed");
        updated.Enabled.Should().BeFalse();
        updated.SettingsJson.Should().Contain("http://new");
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenAbsent()
        => (await _store.UpdateAsync(999, "x", true, "{}")).Should().BeNull();

    [Fact]
    public async Task DeleteAsync_RemovesRow()
    {
        var created = await _store.CreateAsync(New());

        (await _store.DeleteAsync(created.Id)).Should().BeTrue();
        (await _store.GetByIdAsync(created.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Schema_EnforcesUniqueNamePerProvider()
    {
        await _store.CreateAsync(New(name: "Dup"));

        var act = async () => await _store.CreateAsync(New(name: "Dup"));

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Schema_AllowsSameNameAcrossProviders()
    {
        await _store.CreateAsync(New(providerKey: "sonarr", name: "Main"));

        var act = async () => await _store.CreateAsync(New(providerKey: "radarr", name: "Main"));

        await act.Should().NotThrowAsync();
    }
}
