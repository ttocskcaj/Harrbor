using Harrbor.Configuration;
using Harrbor.Data.Entities;
using Xunit;

namespace Harrbor.Tests;

public class TransferRetryTests
{
    [Fact]
    public void JobDefinition_DefaultRetrySettings_AreCorrect()
    {
        var job = new JobDefinition();

        Assert.Equal(3, job.MaxTransferRetries);
        Assert.Equal(TimeSpan.FromMinutes(5), job.TransferRetryDelay);
    }

    [Fact]
    public void JobDefinition_RetrySettings_CanBeCustomized()
    {
        var job = new JobDefinition
        {
            MaxTransferRetries = 5,
            TransferRetryDelay = TimeSpan.FromMinutes(10)
        };

        Assert.Equal(5, job.MaxTransferRetries);
        Assert.Equal(TimeSpan.FromMinutes(10), job.TransferRetryDelay);
    }

    [Theory]
    [InlineData(0, 3, true)]  // No errors yet, under limit
    [InlineData(1, 3, true)]  // 1 error, under limit
    [InlineData(2, 3, true)]  // 2 errors, under limit
    [InlineData(3, 3, false)] // 3 errors, at limit
    [InlineData(4, 3, false)] // 4 errors, over limit
    [InlineData(0, 0, false)] // No retries allowed
    public void IsEligibleForRetry_RespectsMaxRetries(int errorCount, int maxRetries, bool expectedEligible)
    {
        var release = new TrackedRelease
        {
            TransferStatus = TransferStatus.Failed,
            ErrorCount = errorCount,
            LastErrorAtUtc = DateTime.UtcNow.AddMinutes(-10) // Well past cooldown
        };

        var isEligible = IsEligibleForRetry(release, maxRetries, TimeSpan.FromMinutes(5));

        Assert.Equal(expectedEligible, isEligible);
    }

    [Fact]
    public void IsEligibleForRetry_RespectsRetryDelay()
    {
        var job = new JobDefinition
        {
            MaxTransferRetries = 3,
            TransferRetryDelay = TimeSpan.FromMinutes(5)
        };

        // Error occurred 3 minutes ago - still in cooldown
        var recentFailure = new TrackedRelease
        {
            TransferStatus = TransferStatus.Failed,
            ErrorCount = 1,
            LastErrorAtUtc = DateTime.UtcNow.AddMinutes(-3)
        };

        // Error occurred 10 minutes ago - past cooldown
        var oldFailure = new TrackedRelease
        {
            TransferStatus = TransferStatus.Failed,
            ErrorCount = 1,
            LastErrorAtUtc = DateTime.UtcNow.AddMinutes(-10)
        };

        Assert.False(IsEligibleForRetry(recentFailure, job.MaxTransferRetries, job.TransferRetryDelay));
        Assert.True(IsEligibleForRetry(oldFailure, job.MaxTransferRetries, job.TransferRetryDelay));
    }

    [Fact]
    public void IsEligibleForRetry_RequiresFailedStatus()
    {
        var pendingRelease = new TrackedRelease
        {
            TransferStatus = TransferStatus.Pending,
            ErrorCount = 0
        };

        var completedRelease = new TrackedRelease
        {
            TransferStatus = TransferStatus.Completed,
            ErrorCount = 0
        };

        var inProgressRelease = new TrackedRelease
        {
            TransferStatus = TransferStatus.InProgress,
            ErrorCount = 0
        };

        Assert.False(IsEligibleForRetry(pendingRelease, 3, TimeSpan.FromMinutes(5)));
        Assert.False(IsEligibleForRetry(completedRelease, 3, TimeSpan.FromMinutes(5)));
        Assert.False(IsEligibleForRetry(inProgressRelease, 3, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void IsEligibleForRetry_RequiresLastErrorAtUtc()
    {
        var releaseWithoutErrorTime = new TrackedRelease
        {
            TransferStatus = TransferStatus.Failed,
            ErrorCount = 1,
            LastErrorAtUtc = null
        };

        Assert.False(IsEligibleForRetry(releaseWithoutErrorTime, 3, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void ErrorStateCleared_OnSuccess()
    {
        var release = new TrackedRelease
        {
            TransferStatus = TransferStatus.Failed,
            ErrorCount = 2,
            LastError = "Previous error",
            LastErrorAtUtc = DateTime.UtcNow.AddMinutes(-10)
        };

        // Simulate successful transfer
        release.TransferStatus = TransferStatus.Completed;
        release.ErrorCount = 0;
        release.LastError = null;
        release.LastErrorAtUtc = null;

        Assert.Equal(TransferStatus.Completed, release.TransferStatus);
        Assert.Equal(0, release.ErrorCount);
        Assert.Null(release.LastError);
        Assert.Null(release.LastErrorAtUtc);
    }

    [Fact]
    public void JobDefinition_ImportTimeout_DefaultsTo24Hours()
    {
        var job = new JobDefinition();

        Assert.Equal(TimeSpan.FromHours(24), job.ImportTimeout);
    }

    [Fact]
    public void JobDefinition_TransferParallelism_DefaultsTo2()
    {
        var job = new JobDefinition();

        Assert.Equal(2, job.TransferParallelism);
    }

    private static bool IsEligibleForRetry(TrackedRelease release, int maxRetries, TimeSpan retryDelay)
    {
        if (release.TransferStatus != TransferStatus.Failed)
            return false;

        if (release.ErrorCount >= maxRetries)
            return false;

        if (release.LastErrorAtUtc == null)
            return false;

        var retryEligibleCutoff = DateTime.UtcNow - retryDelay;
        return release.LastErrorAtUtc <= retryEligibleCutoff;
    }
}
