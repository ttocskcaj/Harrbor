using System.Diagnostics;
using FakeItEasy;
using FluentAssertions;
using Harrbor.Services.Processes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Harrbor.Tests.Services.Conversion;

/// <summary>Runs real POSIX processes; skipped on Windows.</summary>
public class ProcessRunnerTests
{
    private readonly ProcessRunner _runner = new(A.Fake<ILogger<ProcessRunner>>());

    private static void RequirePosix() =>
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Requires a POSIX shell");

    [Fact]
    public async Task RunAsync_CapturesStdoutStderrAndExitCode()
    {
        RequirePosix();

        var result = await _runner.RunAsync(
            new ProcessSpec("sh", ["-c", "echo out; echo err >&2; exit 3"]), TestContext.Current.CancellationToken);

        result.ExitCode.Should().Be(3);
        result.StandardOutput.Trim().Should().Be("out");
        result.StandardError.Trim().Should().Be("err");
        result.Succeeded().Should().BeFalse();
        result.Succeeded(3).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_ArgumentsAreNotShellInterpreted()
    {
        RequirePosix();

        var result = await _runner.RunAsync(
            new ProcessSpec("printf", ["%s|", "a b", "$HOME", "it's"]), TestContext.Current.CancellationToken);

        result.StandardOutput.Should().Be("a b|$HOME|it's|");
    }

    [Fact]
    public async Task RunPipelineAsync_PipesStdoutToNextStdin()
    {
        RequirePosix();

        var results = await _runner.RunPipelineAsync(
            [
                new ProcessSpec("sh", ["-c", "head -c 3000000 /dev/zero | tr '\\0' 'a'"]),
                new ProcessSpec("wc", ["-c"])
            ],
            TestContext.Current.CancellationToken);

        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.ExitCode == 0);
        results[1].StandardOutput.Trim().Should().Be("3000000");
    }

    [Fact]
    public async Task RunPipelineAsync_DownstreamFails_UpstreamIsKilled()
    {
        RequirePosix();
        var sw = Stopwatch.StartNew();

        var results = await _runner.RunPipelineAsync(
            [
                new ProcessSpec("sh", ["-c", "while true; do echo y; done"]),
                new ProcessSpec("sh", ["-c", "head -c 10 >/dev/null; echo bad >&2; exit 4"])
            ],
            TestContext.Current.CancellationToken);

        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
        results[1].ExitCode.Should().Be(4);
        results[1].StandardError.Should().Contain("bad");
    }

    [Fact]
    public async Task RunAsync_Cancelled_KillsProcessAndThrows()
    {
        RequirePosix();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var sw = Stopwatch.StartNew();

        var act = () => _runner.RunAsync(new ProcessSpec("sleep", ["30"]), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task RunAsync_MissingExecutable_Throws()
    {
        var act = () => _runner.RunAsync(new ProcessSpec("harrbor-no-such-tool", ["--version"]), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<System.ComponentModel.Win32Exception>();
    }
}
