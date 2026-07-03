using System.Net;
using Microsoft.Extensions.Logging;
using QBittorrent.Client;

namespace Harrbor.Services.Clients;

/// <summary>
/// Resilience policy for transient qBittorrent failures.
///
/// qBittorrent's WebUI session cookie expires server-side, after which calls fail with
/// 401/403 until a fresh login. The remote seedbox connection can also drop mid-request
/// (e.g. "connection reset by peer") surfacing as a raw <see cref="HttpRequestException"/>.
///
/// This policy retries such failures <b>once</b>:
/// <list type="bullet">
///   <item>401/403 (<see cref="QBittorrentClientRequestException"/>): re-authenticate, then retry.</item>
///   <item>Raw transport errors (<see cref="HttpRequestException"/> that is not a
///         <see cref="QBittorrentClientRequestException"/>): retry without re-authenticating.</item>
/// </list>
/// Other API errors (404, 409, ...) are surfaced immediately and never retried.
/// </summary>
public static class QBittorrentRetryPolicy
{
    public static async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        Func<Task> reauthenticateAsync,
        ILogger? logger = null)
    {
        try
        {
            return await operation();
        }
        catch (QBittorrentClientRequestException ex) when (
            ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            // Session cookie likely expired server-side - re-authenticate and retry once.
            logger?.LogWarning(
                "qBittorrent rejected the session ({StatusCode}); re-authenticating and retrying once",
                ex.StatusCode);
            await reauthenticateAsync();
            return await operation();
        }
        catch (HttpRequestException ex) when (ex is not QBittorrentClientRequestException)
        {
            // Raw transport failure (e.g. connection reset by peer) - retry once. A non-auth
            // API error is a QBittorrentClientRequestException and is deliberately excluded.
            logger?.LogWarning(
                "qBittorrent request hit a transient transport error ({Message}); retrying once",
                ex.Message);
            return await operation();
        }
    }

    public static Task ExecuteAsync(
        Func<Task> operation,
        Func<Task> reauthenticateAsync,
        ILogger? logger = null)
    {
        return ExecuteAsync(
            async () => { await operation(); return true; },
            reauthenticateAsync,
            logger);
    }
}
