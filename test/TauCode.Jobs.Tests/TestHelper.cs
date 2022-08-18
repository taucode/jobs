using Serilog;
using TauCode.Infrastructure.Time;
using TauCode.Working;

namespace TauCode.Jobs.Tests;

internal static class TestHelper
{
    internal static readonly DateTimeOffset NeverCopy = new(9000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    internal static async Task WaitUntil(DateTimeOffset now, DateTimeOffset moment, CancellationToken cancellationToken = default)
    {
        var timeout = moment - now;
        if (timeout < TimeSpan.Zero)
        {
            return;
        }

        await Task.Delay(timeout, cancellationToken);
    }

    internal static IJobManager CreateJobManager(bool start, ILogger logger)
    {
        var jobManager = new JobManager(logger);

        if (start)
        {
            jobManager.Start();

            while (true)
            {
                if (jobManager.State == WorkerState.Running)
                {
                    break;
                }

                Thread.Sleep(1);
            }
        }

        return jobManager;
    }

    internal static async Task<bool> WaitUntilSecondsElapse(
        this ITimeProvider timeProvider,
        DateTimeOffset start,
        double seconds,
        CancellationToken token = default)
    {
        var timeout = TimeSpan.FromSeconds(seconds);
        var now = timeProvider.GetCurrentTime();

        var elapsed = now - start;
        if (elapsed >= timeout)
        {
            throw new InvalidOperationException("Too late.");
        }

        while (true)
        {
            await Task.Delay(1, token);

            now = timeProvider.GetCurrentTime();

            elapsed = now - start;
            if (elapsed >= timeout)
            {
                return true;
            }
        }
    }
}