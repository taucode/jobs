using System;
using System.Threading;
using System.Threading.Tasks;

// todo clean up
namespace TauCode.Jobs.Tests
{
    internal static class TestHelper
    {
        internal static readonly DateTimeOffset NeverCopy = new DateTimeOffset(9000, 1, 1, 0, 0, 0, TimeSpan.Zero);


        //internal static void DebugPulseJobManager(this IJobManager jobManager)
        //{
        //    var method = jobManager.GetType().GetMethod("DebugPulse", BindingFlags.NonPublic | BindingFlags.Instance);
        //    if (method == null)
        //    {
        //        throw new NotSupportedException();
        //    }

        //    method.Invoke(jobManager, new object[] { });
        //}

        internal static async Task WaitUntil(DateTimeOffset now, DateTimeOffset moment, CancellationToken cancellationToken = default)
        {
            var timeout = moment - now;
            if (timeout < TimeSpan.Zero)
            {
                return;
            }

            await Task.Delay(timeout, cancellationToken);
        }

        internal static IJobManager CreateJobManager(bool start)
        {
            var jobManager = new JobManager();
            JobDiagnosticsHelper.EnableLogging(jobManager, true);

            if (start)
            {
                jobManager.Start();

                while (true)
                {
                    if (JobDiagnosticsHelper.JobManagerStartedWorking(jobManager))
                    {
                        break;
                    }

                    Thread.Sleep(1);
                }
            }

            return jobManager;
        }

        //internal static void WaitUntil(this ITimeProvider timeProvider, DateTimeOffset moment)
        //{
        //    var now = timeProvider.GetCurrent();
        //    if (now >= moment)
        //    {
        //        return;
        //    }

        //    while (true)
        //    {
        //        Thread.Sleep(1);

        //        now = timeProvider.GetCurrent();
        //        if (now >= moment)
        //        {
        //            return;
        //        }
        //    }
        //}

        //internal static async Task WaitUntil(
        //    this ITimeProvider timeProvider,
        //    DateTimeOffset moment,
        //    CancellationToken token)
        //{
        //    var now = timeProvider.GetCurrent();
        //    if (now >= moment)
        //    {
        //        return;
        //    }

        //    while (true)
        //    {
        //        await Task.Delay(1, token);

        //        now = timeProvider.GetCurrent();
        //        if (now >= moment)
        //        {
        //            return;
        //        }
        //    }
        //}

        internal static async Task<bool> WaitUntilSecondsElapse(
            this ITimeProvider timeProvider,
            DateTimeOffset start,
            double seconds,
            CancellationToken token = default)
        {
            var timeout = TimeSpan.FromSeconds(seconds);
            var now = timeProvider.GetCurrent();

            var elapsed = now - start;
            if (elapsed >= timeout)
            {
                //return false;
                throw new InvalidOperationException("Too late.");
            }

            while (true)
            {
                await Task.Delay(1, token);

                now = timeProvider.GetCurrent();

                elapsed = now - start;
                if (elapsed >= timeout)
                {
                    return true;
                }
            }
        }
    }
}
