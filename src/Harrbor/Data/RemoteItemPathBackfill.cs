using Harrbor.Configuration;
using Harrbor.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Harrbor.Data;

/// <summary>
/// Sets <see cref="TrackedRelease.RemoteItemPath"/> on rows from before that column existed and whose transfer hasn't
/// completed. The <c>AddRemoteItemPath</c> migration already set it to <see cref="TrackedRelease.RemotePath"/> for
/// completed transfers, so their staged layout on disk stays as it is. This step needs the job definitions, which a
/// migration can't see, so it runs at startup.
/// </summary>
public static class RemoteItemPathBackfill
{
    public static async Task<int> ApplyAsync(
        HarrborDbContext dbContext,
        IReadOnlyList<JobDefinition> jobs,
        CancellationToken cancellationToken = default)
    {
        // Rows still waiting on the download get the path when it completes.
        var rows = await dbContext.TrackedReleases
            .Where(r => r.RemoteItemPath == null && r.DownloadStatus == DownloadStatus.Completed)
            .ToListAsync(cancellationToken);

        foreach (var release in rows)
        {
            var job = jobs.FirstOrDefault(j => j.Name == release.JobName);
            release.RemoteItemPath = Compute(release.RemotePath, job?.RemotePath);
        }

        if (rows.Count > 0)
            await dbContext.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }

    /// <summary>
    /// The first segment of <paramref name="remotePath"/> after <paramref name="remoteRoot"/>, or
    /// <paramref name="remotePath"/> itself (the old behaviour) when it isn't under the root.
    /// </summary>
    /// <remarks>
    /// Only <c>{root}/{item}</c> and <c>{root}/{folder}/{file}</c> are rewritten. A deeper path means the torrent's save
    /// path is below the job root (e.g. a category subfolder), where the first segment would be that whole subfolder,
    /// so it keeps the old behaviour.
    /// </remarks>
    public static string Compute(string remotePath, string? remoteRoot)
    {
        var root = remoteRoot?.TrimEnd('/');
        var path = remotePath.TrimEnd('/');
        if (string.IsNullOrEmpty(root) || !path.StartsWith(root + "/", StringComparison.Ordinal))
            return remotePath;

        var segments = path[(root.Length + 1)..].Split('/');
        return segments.Length <= 2 ? $"{root}/{segments[0]}" : remotePath;
    }
}
