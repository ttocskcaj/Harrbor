namespace Harrbor.Abstractions.Clients;

/// <summary>
/// Result of probing a connection with an explicit payload. Shared by <see cref="IMediaService"/>
/// and <see cref="ITorrentClient"/>.
/// </summary>
public record ConnectionTestResult(bool Success, string? Version = null, string? Error = null);
