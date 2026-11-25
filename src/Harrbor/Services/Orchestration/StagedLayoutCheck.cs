namespace Harrbor.Services.Orchestration;

/// <summary>
/// Checks a release's final staging path against the path Sonarr/Radarr reports for the download
/// (the queue record's <c>outputPath</c>, already translated by the *arr's remote path mapping).
/// </summary>
public static class StagedLayoutCheck
{
    /// <param name="Matches">
    /// True if the output path is the final item or inside it, false if it isn't, null if the output path
    /// can't be related to staging (so nothing is known).
    /// </param>
    /// <param name="ExpectedPath">The final item path as the *arr should see it, when it could be worked out.</param>
    public record Result(bool? Matches, string? ExpectedPath);

    /// <param name="outputPath">The *arr's output path for the download.</param>
    /// <param name="stagingPath">The job's staging path (Harrbor's view).</param>
    /// <param name="finalItemPath">Where Harrbor publishes the release (Harrbor's view).</param>
    /// <param name="remoteRoot">The job's remote path on the seedbox: the "Remote Path" of the *arr's mapping.</param>
    /// <param name="remoteContentPath">qBittorrent's content path for the torrent.</param>
    public static Result Evaluate(
        string outputPath,
        string stagingPath,
        string finalItemPath,
        string remoteRoot,
        string remoteContentPath)
    {
        var output = Normalize(outputPath);
        var staging = Normalize(stagingPath);
        var final = Normalize(finalItemPath);

        // Same path namespace: the *arr sees staging at the same path Harrbor does.
        if (IsSameOrUnder(output, staging))
            return new Result(IsSameOrUnder(output, final), final);

        // Different mount points: the *arr reports {its staging root}/{content path relative to the remote root}.
        // Strip that suffix to find its staging root, then place the final item under it.
        var root = Normalize(remoteRoot);
        var content = Normalize(remoteContentPath);
        if (root.Length == 0 || !IsUnder(content, root))
            return new Result(null, null);

        var contentRelative = content[(root.Length + 1)..];
        if (!output.EndsWith("/" + contentRelative, StringComparison.Ordinal))
            return new Result(null, null);

        var arrStagingRoot = output[..^(contentRelative.Length + 1)];
        if (!IsUnder(final, staging))
            return new Result(null, null);

        var expected = arrStagingRoot + final[staging.Length..];
        return new Result(IsSameOrUnder(output, expected), expected);
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimEnd('/');

    private static bool IsUnder(string path, string root) =>
        root.Length > 0 && path.Length > root.Length && path.StartsWith(root + "/", StringComparison.Ordinal);

    private static bool IsSameOrUnder(string path, string root) =>
        string.Equals(path, root, StringComparison.Ordinal) || IsUnder(path, root);
}
