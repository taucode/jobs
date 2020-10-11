using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TauCode.Extensions;

// todo clean up
namespace TauCode.Jobs
{
    public static class JobExtensions
    {
        private const int NeverYear = 9000;

        public static readonly DateTimeOffset Never;

        static JobExtensions()
        {
            Never = $"{NeverYear}-01-01Z".ToUtcDateOffset();
        }

        //public static bool IsNever(this DueTimeInfo dueTimeInfo) => dueTimeInfo.DueTime.Equals(Never);

        // todo: internal
        public static Task IdleJobRoutine(
            object parameter,
            IProgressTracker progressTracker,
            TextWriter output,
            CancellationToken cancellationToken)
        {
            output?.WriteLine("Warning: usage of default idle routine.");
            return Task.CompletedTask;
        }

        internal static DateTimeOffset GetEffectiveDueTime(this DueTimeInfo dueTimeInfo) =>
            dueTimeInfo.OverriddenDueTime ?? dueTimeInfo.ScheduleDueTime;

        internal static bool IsDueTimeOverridden(this DueTimeInfo dueTimeInfo) =>
            dueTimeInfo.OverriddenDueTime.HasValue;
    }
}
