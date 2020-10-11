using System;
using System.IO;
using TauCode.Jobs.Schedules;

namespace TauCode.Jobs
{
    public interface IJob : IDisposable
    {
        string Name { get; }
        bool IsEnabled { get; set; }
        ISchedule Schedule { get; set; }
        JobDelegate Routine { get; set; }
        object Parameter { get; set; }
        IProgressTracker ProgressTracker { get; set; }
        TextWriter Output { get; set; }
        JobInfo GetInfo(int? maxRunCount);
        void OverrideDueTime(DateTimeOffset? dueTime);
        void ForceStart();
        bool Cancel();
        JobRunStatus? Wait(int millisecondsTimeout);
        JobRunStatus? Wait(TimeSpan timeout);
        bool IsDisposed { get; }
    }
}
