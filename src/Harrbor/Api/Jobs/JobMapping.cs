using Harrbor.Data.Entities;

namespace Harrbor.Api.Jobs;

/// <summary>
/// Request validation, entity mapping, and response projection for jobs.
/// </summary>
public static class JobMapping
{
    public static ValidationErrors Validate(JobRequest request)
    {
        var errors = new ValidationErrors();

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
            errors.Add("name", "Name is required.");
        else if (name.Length > 100)
            errors.Add("name", "Must be at most 100 characters.");

        errors.RequireRange("pollingIntervalSeconds", request.PollingIntervalSeconds, 10, 86400);

        var remotePath = (request.RemotePath ?? string.Empty).Trim();
        if (remotePath.Length == 0)
            errors.Add("remotePath", "Remote path is required.");
        else if (!remotePath.StartsWith('/'))
            errors.Add("remotePath", "Must be an absolute path starting with '/'.");
        else if (remotePath.Length > 1000)
            errors.Add("remotePath", "Must be at most 1000 characters.");

        var stagingPath = (request.StagingPath ?? string.Empty).Trim();
        if (stagingPath.Length == 0)
            errors.Add("stagingPath", "Staging path is required.");
        else if (!Path.IsPathRooted(stagingPath))
            errors.Add("stagingPath", "Must be an absolute path.");
        else if (stagingPath.Length > 1000)
            errors.Add("stagingPath", "Must be at most 1000 characters.");

        // Field key must match the wire name (camelCase lowers only the first char)
        if ((request.QBittorrentCategory ?? string.Empty).Length > 100)
            errors.Add("qBittorrentCategory", "Must be at most 100 characters.");

        if (request.CompletedCategory is { Length: > 100 })
            errors.Add("completedCategory", "Must be at most 100 characters.");

        errors.RequireRange("transferParallelism", request.TransferParallelism, 1, 16);
        errors.RequireRange("maxTransferRetries", request.MaxTransferRetries, 0, 20);
        errors.RequireRange("transferRetryDelaySeconds", request.TransferRetryDelaySeconds, 0, 86400);
        errors.RequireRange("importTimeoutSeconds", request.ImportTimeoutSeconds, 60, 604800);

        return errors;
    }

    /// <summary>
    /// Probes that the staging path can be created and written to, adding a
    /// field error on failure. Called only when the path passed static validation.
    /// </summary>
    public static void ProbeStagingPath(string stagingPath, ValidationErrors errors)
    {
        try
        {
            Directory.CreateDirectory(stagingPath);
            var testFile = Path.Combine(stagingPath, ".harrbor-write-test");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
        }
        catch (Exception ex)
        {
            errors.Add("stagingPath", $"Staging path is not writable: {ex.Message}");
        }
    }

    public static void Apply(JobRequest request, Job job, DateTime nowUtc)
    {
        job.Name = (request.Name ?? string.Empty).Trim();
        job.Enabled = request.Enabled;
        job.ServiceType = request.ServiceType;
        job.PollingInterval = TimeSpan.FromSeconds(request.PollingIntervalSeconds);
        job.RemotePath = (request.RemotePath ?? string.Empty).Trim();
        job.StagingPath = (request.StagingPath ?? string.Empty).Trim();
        job.QBittorrentCategory = (request.QBittorrentCategory ?? string.Empty).Trim();
        job.QBittorrentTags = [.. request.QBittorrentTags ?? []];
        job.TransferParallelism = request.TransferParallelism;
        job.MaxTransferRetries = request.MaxTransferRetries;
        job.TransferRetryDelay = TimeSpan.FromSeconds(request.TransferRetryDelaySeconds);
        job.CompletedCategory = string.IsNullOrWhiteSpace(request.CompletedCategory) ? null : request.CompletedCategory.Trim();
        job.ImportTimeout = TimeSpan.FromSeconds(request.ImportTimeoutSeconds);
        job.UpdatedAtUtc = nowUtc;
    }

    public static JobResponse ToResponse(Job job, int activeReleaseCount, DateTime nowUtc) => new()
    {
        Id = job.Id,
        Name = job.Name,
        Enabled = job.Enabled,
        ServiceType = job.ServiceType,
        PollingIntervalSeconds = (int)job.PollingInterval.TotalSeconds,
        RemotePath = job.RemotePath,
        StagingPath = job.StagingPath,
        QBittorrentCategory = job.QBittorrentCategory,
        QBittorrentTags = [.. job.QBittorrentTags],
        TransferParallelism = job.TransferParallelism,
        MaxTransferRetries = job.MaxTransferRetries,
        TransferRetryDelaySeconds = (int)job.TransferRetryDelay.TotalSeconds,
        CompletedCategory = job.CompletedCategory,
        ImportTimeoutSeconds = (int)job.ImportTimeout.TotalSeconds,
        CreatedAtUtc = job.CreatedAtUtc,
        UpdatedAtUtc = job.UpdatedAtUtc,
        LastRunAtUtc = job.LastRunAtUtc,
        NextRunAtUtc = !job.Enabled
            ? null
            : job.LastRunAtUtc is null ? nowUtc : job.LastRunAtUtc + job.PollingInterval,
        ActiveReleaseCount = activeReleaseCount
    };
}
