using System;
using System.Collections.Generic;
using System.Linq;

namespace TauCode.Jobs
{
    public readonly struct JobInfo
    {
        internal JobInfo(
            JobRunInfo? currentRun,
            DateTimeOffset nextDueTime,
            bool nextDueTimeIsOverridden,
            int runCount,
            IEnumerable<JobRunInfo> runs)
        {
            if (runs == null)
            {
                throw new ArgumentNullException(nameof(runs));
            }

            this.CurrentRun = currentRun;
            this.NextDueTime = nextDueTime;
            this.NextDueTimeIsOverridden = nextDueTimeIsOverridden;
            this.RunCount = runCount;
            this.Runs = runs.ToList();
        }

        public JobRunInfo? CurrentRun { get; }
        public DateTimeOffset NextDueTime { get; }
        public bool NextDueTimeIsOverridden { get; }
        public int RunCount { get; }
        public IReadOnlyList<JobRunInfo> Runs { get; }
    }
}
