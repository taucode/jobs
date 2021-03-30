using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using TauCode.Infrastructure.Time;

namespace TauCode.Jobs.Tests
{
    internal static class TestHelper
    {
        internal static readonly DateTimeOffset NeverCopy = new DateTimeOffset(9000, 1, 1, 0, 0, 0, TimeSpan.Zero);

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
            var jobManager = new JobManager();
            jobManager.Logger = logger;

            if (start)
            {
                jobManager.Start();

                while (true)
                {
                    if (jobManager.IsRunning)
                    {
                        break;
                    }

                    Thread.Sleep(1);
                }
            }

            return jobManager;
        }
        
        // todo: use taucode.infra time machine.
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
                //return false;
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
}
