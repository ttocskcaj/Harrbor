using FakeItEasy;
using FluentAssertions;
using Harrbor.Configuration;
using Harrbor.Services.Conversion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Harrbor.Tests.Services.Conversion;

public class ConversionServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"harrbor-conv-{Guid.NewGuid():N}");
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), $"harrbor-scratch-{Guid.NewGuid():N}");
    private readonly ConversionOptions _options = new();
    private readonly FakeDiskSpace _disk = new();
    private readonly FakeProbeService _probe = new();
    private static readonly ConversionRunOptions Run = new(DryRun: false, MinFileSizeBytes: 0);

    public ConversionServiceTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
        if (Directory.Exists(_scratch))
            Directory.Delete(_scratch, recursive: true);
    }

    private ConversionService CreateService() =>
        new(_probe, _disk, Options.Create(_options), A.Fake<ILogger<ConversionService>>());

    private string Write(string relative, string content)
    {
        var path = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Directory listing relative to the test dir, to assert no leftovers.</summary>
    private IEnumerable<string> AllEntries() =>
        Directory.EnumerateFileSystemEntries(_dir, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(_dir, p));

    [Fact]
    public async Task ProcessAsync_MatchingFile_ConvertsAndReplacesAtomically()
    {
        var file = Write("Movie/movie.mkv", "orig");
        var converter = new FakeConverter("A", from: "orig", to: "A");

        var result = await CreateService().ProcessAsync(Path.Combine(_dir, "Movie"), [converter], Run, TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.Converted.Should().Be(1);
        result.Summary.Should().Be("A: 1 converted");
        File.ReadAllText(file).Should().Be("A");
        AllEntries().Should().BeEquivalentTo("Movie", Path.Combine("Movie", "movie.mkv"));
        converter.LastScratchDir.Should().NotBeNull();
        Path.GetFileName(converter.LastScratchDir).Should().StartWith(ConversionArtifacts.ScratchDirPrefix);
        converter.LastOutputPath.Should().Be(Path.Combine(_dir, "Movie", ".movie.harrbor-tmp.mkv"));
    }

    [Fact]
    public async Task ProcessAsync_SingleFileItem_IsProcessed()
    {
        var file = Write("movie.mkv", "orig");

        var result = await CreateService().ProcessAsync(file, [new FakeConverter("A", "orig", "A")], Run, TestContext.Current.CancellationToken);

        result.Converted.Should().Be(1);
        File.ReadAllText(file).Should().Be("A");
    }

    [Fact]
    public async Task ProcessAsync_FiltersByExtension()
    {
        Write("movie.mp4", "orig");
        var converter = new FakeConverter("A", "orig", "A");

        var result = await CreateService().ProcessAsync(_dir, [converter], Run, TestContext.Current.CancellationToken);

        result.Outcomes.Should().BeEmpty();
        result.Summary.Should().Be("no eligible files");
        converter.EvaluateCalls.Should().Be(0);
        _probe.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ProcessAsync_ChainsConvertersInOrderWithReprobe()
    {
        var file = Write("movie.mkv", "orig");
        var a = new FakeConverter("A", from: "orig", to: "A");
        var b = new FakeConverter("B", from: "A", to: "B"); // only converts if it sees A's output

        var result = await CreateService().ProcessAsync(_dir, [a, b], Run, TestContext.Current.CancellationToken);

        result.Converted.Should().Be(2);
        File.ReadAllText(file).Should().Be("B");
        b.LastInputContent.Should().Be("A");
    }

    [Fact]
    public async Task ProcessAsync_OrderMatters_SecondConverterSkipsWhenFirstRunsLater()
    {
        var file = Write("movie.mkv", "orig");
        var a = new FakeConverter("A", from: "orig", to: "A");
        var b = new FakeConverter("B", from: "A", to: "B");

        var result = await CreateService().ProcessAsync(_dir, [b, a], Run, TestContext.Current.CancellationToken);

        File.ReadAllText(file).Should().Be("A");
        result.Outcomes.Select(o => (o.Converter, o.Kind)).Should().Equal(
            ("B", ConversionOutcomeKind.Skipped), ("A", ConversionOutcomeKind.Converted));
    }

    [Fact]
    public async Task ProcessAsync_ConvertThrows_CleansUpAndLeavesOriginal()
    {
        var file = Write("movie.mkv", "orig");
        var converter = new FakeConverter("A", "orig", "A") { ThrowAfterWriting = true };

        var result = await CreateService().ProcessAsync(_dir, [converter], Run, TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Failed.Should().Be(1);
        result.FirstError.Should().Contain("boom");
        File.ReadAllText(file).Should().Be("orig");
        AllEntries().Should().BeEquivalentTo("movie.mkv");
    }

    [Fact]
    public async Task ProcessAsync_VerificationFails_LeavesOriginal()
    {
        var file = Write("movie.mkv", "orig");
        var converter = new FakeConverter("A", "orig", "A") { FailVerification = true };

        var result = await CreateService().ProcessAsync(_dir, [converter], Run, TestContext.Current.CancellationToken);

        result.Outcomes.Should().ContainSingle()
            .Which.Should().Match<ConversionOutcome>(o => o.Kind == ConversionOutcomeKind.Failed && o.Detail!.Contains("Verification failed"));
        File.ReadAllText(file).Should().Be("orig");
        AllEntries().Should().BeEquivalentTo("movie.mkv");
    }

    [Fact]
    public async Task ProcessAsync_FailureInFirstConverter_SecondStillRunsOnOriginal()
    {
        var file = Write("movie.mkv", "orig");
        var a = new FakeConverter("A", "orig", "A") { ThrowAfterWriting = true };
        var b = new FakeConverter("B", "orig", "B");

        var result = await CreateService().ProcessAsync(_dir, [a, b], Run, TestContext.Current.CancellationToken);

        result.Failed.Should().Be(1);
        result.Converted.Should().Be(1);
        File.ReadAllText(file).Should().Be("B");
    }

    [Fact]
    public async Task ProcessAsync_DryRun_NeverWrites()
    {
        var file = Write("movie.mkv", "orig");
        var converter = new FakeConverter("A", "orig", "A");

        var result = await CreateService().ProcessAsync(_dir, [converter], Run with { DryRun = true }, TestContext.Current.CancellationToken);

        converter.ConvertCalls.Should().Be(0);
        result.Outcomes.Should().ContainSingle().Which.Kind.Should().Be(ConversionOutcomeKind.WouldConvert);
        File.ReadAllText(file).Should().Be("orig");
        AllEntries().Should().BeEquivalentTo("movie.mkv");
    }

    [Fact]
    public async Task ProcessAsync_MinFileSize_SkipsSmallFiles()
    {
        Write("sample.mkv", "orig");
        var big = Write("movie.mkv", "orig" + new string(' ', 100));
        var converter = new FakeConverter("A", from: null, to: "A");

        var result = await CreateService().ProcessAsync(_dir, [converter], Run with { MinFileSizeBytes = 50 }, TestContext.Current.CancellationToken);

        result.Outcomes.Should().ContainSingle().Which.FilePath.Should().Be(big);
        File.ReadAllText(Path.Combine(_dir, "sample.mkv")).Should().Be("orig");
    }

    [Fact]
    public async Task ProcessAsync_IgnoresHiddenFiles()
    {
        Write(".movie.harrbor-tmp.mkv", "orig");

        var result = await CreateService().ProcessAsync(_dir, [new FakeConverter("A", "orig", "A")], Run, TestContext.Current.CancellationToken);

        result.Outcomes.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_Unsupported_RecordedButNotFailure()
    {
        var file = Write("movie.mkv", "orig");
        var converter = new FakeConverter("A", "orig", "A") { Unsupported = true };

        var result = await CreateService().ProcessAsync(_dir, [converter], Run, TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.Outcomes.Should().ContainSingle().Which.Kind.Should().Be(ConversionOutcomeKind.Unsupported);
        File.ReadAllText(file).Should().Be("orig");
    }

    [Fact]
    public async Task ProcessAsync_ProbeFails_RecordsFailure()
    {
        Write("movie.mkv", "orig");
        _probe.Throw = true;

        var result = await CreateService().ProcessAsync(_dir, [new FakeConverter("A", "orig", "A")], Run, TestContext.Current.CancellationToken);

        result.Outcomes.Should().ContainSingle().Which.Should().Match<ConversionOutcome>(
            o => o.Converter == "probe" && o.Kind == ConversionOutcomeKind.Failed);
    }

    [Fact]
    public async Task ProcessAsync_MissingItem_Throws()
    {
        var act = () => CreateService().ProcessAsync(Path.Combine(_dir, "nope"), [], Run, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public void CleanupLeftovers_RemovesTempFilesAndScratchDirs()
    {
        Write("Movie/movie.mkv", "keep");
        Write("Movie/.movie.harrbor-tmp.mkv", "tmp");
        Write($"Movie/{ConversionArtifacts.ScratchDirPrefix}abc/video.hevc", "scratch");
        Write("Movie/.hidden-but-not-ours", "keep");

        var removed = ConversionArtifacts.CleanupLeftovers(_dir);

        removed.Should().Be(2);
        AllEntries().Should().BeEquivalentTo(
            "Movie", Path.Combine("Movie", "movie.mkv"), Path.Combine("Movie", ".hidden-but-not-ours"));
    }

    [Fact]
    public async Task ScratchPath_Set_ScratchUnderScratchPathAndTempOutputNextToFile()
    {
        _options.ScratchPath = _scratch;
        var file = Write("Movie/movie.mkv", "orig");
        var converter = new FakeConverter("A", "orig", "A");

        var result = await CreateService().ProcessAsync(file, [converter], Run, TestContext.Current.CancellationToken);

        result.Converted.Should().Be(1);
        File.ReadAllText(file).Should().Be("A");
        Path.GetDirectoryName(converter.LastScratchDir).Should().Be(_scratch);
        Path.GetFileName(converter.LastScratchDir).Should().StartWith(ConversionArtifacts.ScratchDirPrefix);
        converter.LastOutputPath.Should().Be(Path.Combine(_dir, "Movie", ".movie.harrbor-tmp.mkv"));
        Directory.EnumerateFileSystemEntries(_scratch).Should().BeEmpty("the scratch dir is removed on success");
        AllEntries().Should().BeEquivalentTo("Movie", Path.Combine("Movie", "movie.mkv"));
    }

    [Fact]
    public async Task ScratchPath_Set_ScratchRemovedOnFailure()
    {
        _options.ScratchPath = _scratch;
        var file = Write("movie.mkv", "orig");
        var converter = new FakeConverter("A", "orig", "A") { ThrowAfterWriting = true };

        var result = await CreateService().ProcessAsync(file, [converter], Run, TestContext.Current.CancellationToken);

        result.Failed.Should().Be(1);
        File.ReadAllText(file).Should().Be("orig");
        Directory.EnumerateFileSystemEntries(_scratch).Should().BeEmpty();
        AllEntries().Should().BeEquivalentTo("movie.mkv");
    }

    [Fact]
    public async Task ScratchPath_Set_ScratchRemovedOnCancellation()
    {
        _options.ScratchPath = _scratch;
        var file = Write("movie.mkv", "orig");
        var converter = new FakeConverter("A", "orig", "A") { CancelAfterWriting = true };

        var act = () => CreateService().ProcessAsync(file, [converter], Run, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<OperationCanceledException>();
        File.ReadAllText(file).Should().Be("orig");
        Directory.EnumerateFileSystemEntries(_scratch).Should().BeEmpty();
        AllEntries().Should().BeEquivalentTo("movie.mkv");
    }

    [Fact]
    public async Task ScratchPath_Null_ScratchNextToFile()
    {
        var file = Write("Movie/movie.mkv", "orig");
        var converter = new FakeConverter("A", "orig", "A");

        await CreateService().ProcessAsync(file, [converter], Run, TestContext.Current.CancellationToken);

        Path.GetDirectoryName(converter.LastScratchDir).Should().Be(Path.Combine(_dir, "Movie"));
        Directory.Exists(_scratch).Should().BeFalse();
    }

    // The test file is 100 bytes, so 2x = 200.
    [Theory]
    [InlineData(false, 199, 0, false)]    // no scratch path: media fs needs 2x
    [InlineData(false, 200, 0, true)]
    [InlineData(true, 100, 100, true)]    // separate filesystems: 1x on each
    [InlineData(true, 99, 1000, false)]   // media fs short of the temp output
    [InlineData(true, 1000, 99, false)]   // scratch fs short of the intermediates
    public async Task FreeSpaceCheck_UsesTheRightFilesystemForEachPart(bool useScratch, long mediaFree, long scratchFree, bool converts)
    {
        if (useScratch)
            _options.ScratchPath = _scratch;
        var file = Write("movie.mkv", new string('x', 100));
        _disk.Space = path => path.StartsWith(_scratch)
            ? new DiskSpace("scratch-fs", scratchFree)
            : new DiskSpace("media-fs", mediaFree);
        var converter = new FakeConverter("A", from: null, to: "A");

        var result = await CreateService().ProcessAsync(file, [converter], Run, TestContext.Current.CancellationToken);

        result.Converted.Should().Be(converts ? 1 : 0);
        if (!converts)
        {
            result.FirstError.Should().Contain("Not enough free disk space");
            converter.ConvertCalls.Should().Be(0);
        }
    }

    [Theory]
    [InlineData(199, false)]
    [InlineData(200, true)]
    public async Task FreeSpaceCheck_ScratchPathOnSameFilesystem_Needs2x(long free, bool converts)
    {
        _options.ScratchPath = _scratch;
        var file = Write("movie.mkv", new string('x', 100));
        _disk.Space = _ => new DiskSpace("one-fs", free);

        var result = await CreateService().ProcessAsync(file, [new FakeConverter("A", null, "A")], Run, TestContext.Current.CancellationToken);

        result.Converted.Should().Be(converts ? 1 : 0);
    }

    [Fact]
    public async Task FreeSpaceCheck_UnknownSpace_NotChecked()
    {
        _options.ScratchPath = _scratch;
        var file = Write("movie.mkv", new string('x', 100));
        _disk.Space = _ => null;

        var result = await CreateService().ProcessAsync(file, [new FakeConverter("A", null, "A")], Run, TestContext.Current.CancellationToken);

        result.Converted.Should().Be(1);
    }

    [Fact]
    public void TempPathFor_IsHiddenInSameDirectory()
    {
        ConversionArtifacts.TempPathFor("/a/b/Movie (2023).mkv").Should().Be("/a/b/.Movie (2023).harrbor-tmp.mkv");
    }

    private sealed class FakeDiskSpace : IDiskSpaceProvider
    {
        public Func<string, DiskSpace?> Space { get; set; } = _ => new DiskSpace("fs", long.MaxValue);
        public DiskSpace? GetDiskSpace(string path) => Space(path);
    }

    /// <summary>Probe whose only content is the file text, exposed as the "content" format tag.</summary>
    private sealed class FakeProbeService : IMediaProbeService
    {
        public int Calls { get; private set; }
        public bool Throw { get; set; }

        public Task<MediaProbe> ProbeAsync(string path, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Throw)
                throw new InvalidOperationException("ffprobe failed");
            var content = File.ReadAllText(path).TrimEnd();
            return Task.FromResult(new MediaProbe(
                [],
                1,
                "matroska",
                new Dictionary<string, string> { ["content"] = content }));
        }
    }

    /// <summary>Converts when the probed content equals <c>from</c> (or always, if null), writing <c>to</c>.</summary>
    private sealed class FakeConverter(string name, string? from, string to) : IMediaConverter
    {
        public string Name => name;
        public IReadOnlyCollection<string> Extensions => [".mkv"];
        public IReadOnlyCollection<string> RequiredTools => [];
        public bool ThrowAfterWriting { get; init; }
        public bool CancelAfterWriting { get; init; }
        public bool FailVerification { get; init; }
        public bool Unsupported { get; init; }
        public int EvaluateCalls { get; private set; }
        public int ConvertCalls { get; private set; }
        public string? LastScratchDir { get; private set; }
        public string? LastOutputPath { get; private set; }
        public string? LastInputContent { get; private set; }

        public ConversionDecision Evaluate(MediaProbe probe)
        {
            EvaluateCalls++;
            if (Unsupported)
                return ConversionDecision.Unsupported("weird");
            return from is null || probe.FormatTags["content"] == from
                ? ConversionDecision.Convert()
                : ConversionDecision.Skip("not mine");
        }

        public async Task ConvertAsync(string inputPath, string outputPath, MediaProbe probe, string scratchDir, CancellationToken cancellationToken)
        {
            ConvertCalls++;
            LastScratchDir = scratchDir;
            LastOutputPath = outputPath;
            LastInputContent = probe.FormatTags["content"];
            Directory.Exists(scratchDir).Should().BeTrue();
            await File.WriteAllTextAsync(Path.Combine(scratchDir, "intermediate"), "x", cancellationToken);
            await File.WriteAllTextAsync(outputPath, to, cancellationToken);
            if (ThrowAfterWriting)
                throw new InvalidOperationException("boom");
            if (CancelAfterWriting)
                throw new OperationCanceledException();
        }

        public ConversionVerification Verify(MediaProbe source, MediaProbe output) =>
            FailVerification ? ConversionVerification.Fail("mismatch") : ConversionVerification.Pass();
    }
}
