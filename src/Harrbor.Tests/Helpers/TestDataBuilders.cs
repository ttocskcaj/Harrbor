using Harrbor.Configuration;
using Harrbor.Data.Entities;
using Harrbor.Services.Clients.Models;

namespace Harrbor.Tests.Helpers;

public class TrackedReleaseBuilder
{
    private int _id;
    private string _downloadId = "ABC123DEF456";
    private string _name = "Test Release";
    private string _jobName = "test-job";
    private string _remotePath = "/downloads/test";
    private string _stagingPath = "/staging/test";
    private DownloadStatus _downloadStatus = DownloadStatus.Pending;
    private TransferStatus _transferStatus = TransferStatus.Pending;
    private ImportStatus _importStatus = ImportStatus.Pending;
    private CleanupStatus _cleanupStatus = CleanupStatus.Pending;
    private ArchivalStatus _archivalStatus = ArchivalStatus.Pending;
    private int _errorCount;
    private string? _lastError;
    private DateTime? _lastErrorAtUtc;
    private DateTime _createdAtUtc = DateTime.UtcNow;
    private DateTime? _downloadCompletedAtUtc;
    private DateTime? _transferStartedAtUtc;
    private DateTime? _transferCompletedAtUtc;
    private DateTime? _importCompletedAtUtc;
    private DateTime? _cleanupCompletedAtUtc;
    private DateTime? _archivedAtUtc;

    public TrackedReleaseBuilder WithId(int id)
    {
        _id = id;
        return this;
    }

    public TrackedReleaseBuilder WithDownloadId(string downloadId)
    {
        _downloadId = downloadId;
        return this;
    }

    public TrackedReleaseBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public TrackedReleaseBuilder WithJobName(string jobName)
    {
        _jobName = jobName;
        return this;
    }

    public TrackedReleaseBuilder WithRemotePath(string remotePath)
    {
        _remotePath = remotePath;
        return this;
    }

    public TrackedReleaseBuilder WithStagingPath(string stagingPath)
    {
        _stagingPath = stagingPath;
        return this;
    }

    public TrackedReleaseBuilder WithDownloadStatus(DownloadStatus status)
    {
        _downloadStatus = status;
        return this;
    }

    public TrackedReleaseBuilder WithTransferStatus(TransferStatus status)
    {
        _transferStatus = status;
        return this;
    }

    public TrackedReleaseBuilder WithImportStatus(ImportStatus status)
    {
        _importStatus = status;
        return this;
    }

    public TrackedReleaseBuilder WithCleanupStatus(CleanupStatus status)
    {
        _cleanupStatus = status;
        return this;
    }

    public TrackedReleaseBuilder WithArchivalStatus(ArchivalStatus status)
    {
        _archivalStatus = status;
        return this;
    }

    public TrackedReleaseBuilder WithErrorCount(int errorCount)
    {
        _errorCount = errorCount;
        return this;
    }

    public TrackedReleaseBuilder WithLastError(string? lastError)
    {
        _lastError = lastError;
        return this;
    }

    public TrackedReleaseBuilder WithLastErrorAtUtc(DateTime? lastErrorAtUtc)
    {
        _lastErrorAtUtc = lastErrorAtUtc;
        return this;
    }

    public TrackedReleaseBuilder WithCreatedAtUtc(DateTime createdAtUtc)
    {
        _createdAtUtc = createdAtUtc;
        return this;
    }

    public TrackedReleaseBuilder WithDownloadCompletedAtUtc(DateTime? downloadCompletedAtUtc)
    {
        _downloadCompletedAtUtc = downloadCompletedAtUtc;
        return this;
    }

    public TrackedReleaseBuilder WithTransferStartedAtUtc(DateTime? transferStartedAtUtc)
    {
        _transferStartedAtUtc = transferStartedAtUtc;
        return this;
    }

    public TrackedReleaseBuilder WithTransferCompletedAtUtc(DateTime? transferCompletedAtUtc)
    {
        _transferCompletedAtUtc = transferCompletedAtUtc;
        return this;
    }

    public TrackedReleaseBuilder WithImportCompletedAtUtc(DateTime? importCompletedAtUtc)
    {
        _importCompletedAtUtc = importCompletedAtUtc;
        return this;
    }

    public TrackedReleaseBuilder WithCleanupCompletedAtUtc(DateTime? cleanupCompletedAtUtc)
    {
        _cleanupCompletedAtUtc = cleanupCompletedAtUtc;
        return this;
    }

    public TrackedReleaseBuilder WithArchivedAtUtc(DateTime? archivedAtUtc)
    {
        _archivedAtUtc = archivedAtUtc;
        return this;
    }

    public TrackedRelease Build()
    {
        return new TrackedRelease
        {
            Id = _id,
            DownloadId = _downloadId,
            Name = _name,
            JobName = _jobName,
            RemotePath = _remotePath,
            StagingPath = _stagingPath,
            DownloadStatus = _downloadStatus,
            TransferStatus = _transferStatus,
            ImportStatus = _importStatus,
            CleanupStatus = _cleanupStatus,
            ArchivalStatus = _archivalStatus,
            ErrorCount = _errorCount,
            LastError = _lastError,
            LastErrorAtUtc = _lastErrorAtUtc,
            CreatedAtUtc = _createdAtUtc,
            DownloadCompletedAtUtc = _downloadCompletedAtUtc,
            TransferStartedAtUtc = _transferStartedAtUtc,
            TransferCompletedAtUtc = _transferCompletedAtUtc,
            ImportCompletedAtUtc = _importCompletedAtUtc,
            CleanupCompletedAtUtc = _cleanupCompletedAtUtc,
            ArchivedAtUtc = _archivedAtUtc
        };
    }
}

public class JobDefinitionBuilder
{
    private string _name = "test-job";
    private TimeSpan _pollingInterval = TimeSpan.FromMinutes(1);
    private ServiceType _serviceType = ServiceType.Sonarr;
    private string _remotePath = "/downloads";
    private string _stagingPath = "/staging";
    private string _qBittorrentCategory = "sonarr";
    private List<string> _qBittorrentTags = [];
    private int _transferParallelism = 2;
    private int _maxTransferRetries = 3;
    private TimeSpan _transferRetryDelay = TimeSpan.FromMinutes(5);
    private string? _completedCategory = "completed";
    private TimeSpan _importTimeout = TimeSpan.FromHours(24);
    private bool _enabled = true;

    public JobDefinitionBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public JobDefinitionBuilder WithPollingInterval(TimeSpan pollingInterval)
    {
        _pollingInterval = pollingInterval;
        return this;
    }

    public JobDefinitionBuilder WithServiceType(ServiceType serviceType)
    {
        _serviceType = serviceType;
        return this;
    }

    public JobDefinitionBuilder WithRemotePath(string remotePath)
    {
        _remotePath = remotePath;
        return this;
    }

    public JobDefinitionBuilder WithStagingPath(string stagingPath)
    {
        _stagingPath = stagingPath;
        return this;
    }

    public JobDefinitionBuilder WithQBittorrentCategory(string category)
    {
        _qBittorrentCategory = category;
        return this;
    }

    public JobDefinitionBuilder WithQBittorrentTags(List<string> tags)
    {
        _qBittorrentTags = tags;
        return this;
    }

    public JobDefinitionBuilder WithTransferParallelism(int parallelism)
    {
        _transferParallelism = parallelism;
        return this;
    }

    public JobDefinitionBuilder WithMaxTransferRetries(int maxRetries)
    {
        _maxTransferRetries = maxRetries;
        return this;
    }

    public JobDefinitionBuilder WithTransferRetryDelay(TimeSpan delay)
    {
        _transferRetryDelay = delay;
        return this;
    }

    public JobDefinitionBuilder WithCompletedCategory(string? category)
    {
        _completedCategory = category;
        return this;
    }

    public JobDefinitionBuilder WithImportTimeout(TimeSpan timeout)
    {
        _importTimeout = timeout;
        return this;
    }

    public JobDefinitionBuilder WithEnabled(bool enabled)
    {
        _enabled = enabled;
        return this;
    }

    public JobDefinition Build()
    {
        return new JobDefinition
        {
            Name = _name,
            PollingInterval = _pollingInterval,
            ServiceType = _serviceType,
            RemotePath = _remotePath,
            StagingPath = _stagingPath,
            QBittorrentCategory = _qBittorrentCategory,
            QBittorrentTags = _qBittorrentTags,
            TransferParallelism = _transferParallelism,
            MaxTransferRetries = _maxTransferRetries,
            TransferRetryDelay = _transferRetryDelay,
            CompletedCategory = _completedCategory,
            ImportTimeout = _importTimeout,
            Enabled = _enabled
        };
    }
}

public class QueueItemBuilder
{
    private string _downloadId = "ABC123DEF456";
    private string _title = "Test.Release.S01E01.1080p";
    private string _status = "downloading";
    private string? _trackedDownloadState = "downloading";
    private string? _outputPath = "/downloads/Test.Release.S01E01.1080p";

    public QueueItemBuilder WithDownloadId(string downloadId)
    {
        _downloadId = downloadId;
        return this;
    }

    public QueueItemBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public QueueItemBuilder WithStatus(string status)
    {
        _status = status;
        return this;
    }

    public QueueItemBuilder WithTrackedDownloadState(string? trackedDownloadState)
    {
        _trackedDownloadState = trackedDownloadState;
        return this;
    }

    public QueueItemBuilder WithOutputPath(string? outputPath)
    {
        _outputPath = outputPath;
        return this;
    }

    public QueueItem Build()
    {
        return new QueueItem(_downloadId, _title, _status, _trackedDownloadState, _outputPath);
    }
}

public class QueueResponseBuilder
{
    private int _page = 1;
    private int _pageSize = 100;
    private int _totalRecords;
    private List<QueueRecord> _records = [];

    public QueueResponseBuilder WithPage(int page)
    {
        _page = page;
        return this;
    }

    public QueueResponseBuilder WithPageSize(int pageSize)
    {
        _pageSize = pageSize;
        return this;
    }

    public QueueResponseBuilder WithTotalRecords(int totalRecords)
    {
        _totalRecords = totalRecords;
        return this;
    }

    public QueueResponseBuilder WithRecords(List<QueueRecord> records)
    {
        _records = records;
        return this;
    }

    public QueueResponseBuilder AddRecord(QueueRecord record)
    {
        _records.Add(record);
        return this;
    }

    public QueueResponse Build()
    {
        return new QueueResponse
        {
            Page = _page,
            PageSize = _pageSize,
            TotalRecords = _totalRecords == 0 ? _records.Count : _totalRecords,
            Records = _records
        };
    }
}

public class QueueRecordBuilder
{
    private int _id = 1;
    private string? _downloadId = "ABC123DEF456";
    private string _title = "Test.Release.S01E01.1080p";
    private string _status = "downloading";
    private string? _trackedDownloadState = "downloading";
    private string? _trackedDownloadStatus;
    private string? _downloadClient;
    private string? _outputPath = "/downloads/Test.Release.S01E01.1080p";
    private string? _errorMessage;

    public QueueRecordBuilder WithId(int id)
    {
        _id = id;
        return this;
    }

    public QueueRecordBuilder WithDownloadId(string? downloadId)
    {
        _downloadId = downloadId;
        return this;
    }

    public QueueRecordBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public QueueRecordBuilder WithStatus(string status)
    {
        _status = status;
        return this;
    }

    public QueueRecordBuilder WithTrackedDownloadState(string? trackedDownloadState)
    {
        _trackedDownloadState = trackedDownloadState;
        return this;
    }

    public QueueRecordBuilder WithTrackedDownloadStatus(string? trackedDownloadStatus)
    {
        _trackedDownloadStatus = trackedDownloadStatus;
        return this;
    }

    public QueueRecordBuilder WithDownloadClient(string? downloadClient)
    {
        _downloadClient = downloadClient;
        return this;
    }

    public QueueRecordBuilder WithOutputPath(string? outputPath)
    {
        _outputPath = outputPath;
        return this;
    }

    public QueueRecordBuilder WithErrorMessage(string? errorMessage)
    {
        _errorMessage = errorMessage;
        return this;
    }

    public QueueRecord Build()
    {
        return new QueueRecord
        {
            Id = _id,
            DownloadId = _downloadId,
            Title = _title,
            Status = _status,
            TrackedDownloadState = _trackedDownloadState,
            TrackedDownloadStatus = _trackedDownloadStatus,
            DownloadClient = _downloadClient,
            OutputPath = _outputPath,
            ErrorMessage = _errorMessage
        };
    }
}

public class HistoryResponseBuilder
{
    private int _page = 1;
    private int _pageSize = 50;
    private int _totalRecords;
    private List<HistoryRecord> _records = [];

    public HistoryResponseBuilder WithPage(int page)
    {
        _page = page;
        return this;
    }

    public HistoryResponseBuilder WithPageSize(int pageSize)
    {
        _pageSize = pageSize;
        return this;
    }

    public HistoryResponseBuilder WithTotalRecords(int totalRecords)
    {
        _totalRecords = totalRecords;
        return this;
    }

    public HistoryResponseBuilder WithRecords(List<HistoryRecord> records)
    {
        _records = records;
        return this;
    }

    public HistoryResponseBuilder AddRecord(HistoryRecord record)
    {
        _records.Add(record);
        return this;
    }

    public HistoryResponse Build()
    {
        return new HistoryResponse
        {
            Page = _page,
            PageSize = _pageSize,
            TotalRecords = _totalRecords == 0 ? _records.Count : _totalRecords,
            Records = _records
        };
    }
}

public class HistoryRecordBuilder
{
    private int _id = 1;
    private string _downloadId = "ABC123DEF456";
    private string _eventType = "grabbed";
    private DateTime _date = DateTime.UtcNow;

    public HistoryRecordBuilder WithId(int id)
    {
        _id = id;
        return this;
    }

    public HistoryRecordBuilder WithDownloadId(string downloadId)
    {
        _downloadId = downloadId;
        return this;
    }

    public HistoryRecordBuilder WithEventType(string eventType)
    {
        _eventType = eventType;
        return this;
    }

    public HistoryRecordBuilder WithDate(DateTime date)
    {
        _date = date;
        return this;
    }

    public HistoryRecord Build()
    {
        return new HistoryRecord
        {
            Id = _id,
            DownloadId = _downloadId,
            EventType = _eventType,
            Date = _date
        };
    }
}
