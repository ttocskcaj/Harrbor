using System.Net;
using FluentAssertions;
using Harrbor.Services.Clients;
using QBittorrent.Client;
using Xunit;

namespace Harrbor.Tests.Services.Clients;

/// <summary>
/// Tests for <see cref="QBittorrentRetryPolicy"/>, which retries transient qBittorrent
/// failures (session-expiry auth errors and raw transport drops) exactly once.
/// </summary>
public class QBittorrentRetryPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_OperationSucceeds_ReturnsResultWithoutReauthenticating()
    {
        var reauthCount = 0;

        var result = await QBittorrentRetryPolicy.ExecuteAsync(
            () => Task.FromResult(42),
            () => { reauthCount++; return Task.CompletedTask; });

        result.Should().Be(42);
        reauthCount.Should().Be(0);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task ExecuteAsync_AuthError_ReauthenticatesAndRetriesOnce(HttpStatusCode status)
    {
        var attempts = 0;
        var reauthCount = 0;

        var result = await QBittorrentRetryPolicy.ExecuteAsync(
            () =>
            {
                attempts++;
                if (attempts == 1)
                    throw new QBittorrentClientRequestException("session rejected", status);
                return Task.FromResult("ok");
            },
            () => { reauthCount++; return Task.CompletedTask; });

        result.Should().Be("ok");
        attempts.Should().Be(2);
        reauthCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_TransientTransportError_RetriesOnceWithoutReauthenticating()
    {
        var attempts = 0;
        var reauthCount = 0;

        var result = await QBittorrentRetryPolicy.ExecuteAsync(
            () =>
            {
                attempts++;
                if (attempts == 1)
                    throw new HttpRequestException("Connection reset by peer");
                return Task.FromResult("ok");
            },
            () => { reauthCount++; return Task.CompletedTask; });

        result.Should().Be("ok");
        attempts.Should().Be(2);
        reauthCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_NonAuthApiError_DoesNotRetryAndPropagates()
    {
        var attempts = 0;
        var reauthCount = 0;

        Func<Task> act = () => QBittorrentRetryPolicy.ExecuteAsync<string>(
            () =>
            {
                attempts++;
                throw new QBittorrentClientRequestException("category missing", HttpStatusCode.Conflict);
            },
            () => { reauthCount++; return Task.CompletedTask; });

        await act.Should().ThrowAsync<QBittorrentClientRequestException>();
        attempts.Should().Be(1);
        reauthCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_AuthErrorPersists_RetriesOnceThenPropagates()
    {
        var attempts = 0;
        var reauthCount = 0;

        Func<Task> act = () => QBittorrentRetryPolicy.ExecuteAsync<string>(
            () =>
            {
                attempts++;
                throw new QBittorrentClientRequestException("still forbidden", HttpStatusCode.Forbidden);
            },
            () => { reauthCount++; return Task.CompletedTask; });

        await act.Should().ThrowAsync<QBittorrentClientRequestException>();
        attempts.Should().Be(2);
        reauthCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_TransientErrorPersists_RetriesOnceThenPropagates()
    {
        var attempts = 0;

        Func<Task> act = () => QBittorrentRetryPolicy.ExecuteAsync<string>(
            () =>
            {
                attempts++;
                throw new HttpRequestException("Connection reset by peer");
            },
            () => Task.CompletedTask);

        await act.Should().ThrowAsync<HttpRequestException>();
        attempts.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_VoidOverload_AuthError_ReauthenticatesAndRetriesOnce()
    {
        var attempts = 0;
        var reauthCount = 0;

        await QBittorrentRetryPolicy.ExecuteAsync(
            () =>
            {
                attempts++;
                if (attempts == 1)
                    throw new QBittorrentClientRequestException("session rejected", HttpStatusCode.Forbidden);
                return Task.CompletedTask;
            },
            () => { reauthCount++; return Task.CompletedTask; });

        attempts.Should().Be(2);
        reauthCount.Should().Be(1);
    }
}
