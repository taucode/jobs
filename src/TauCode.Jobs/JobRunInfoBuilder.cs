using System;
using System.IO;

namespace TauCode.Jobs
{
    internal class JobRunInfoBuilder
    {
        internal JobRunInfoBuilder(
            int runIndex,
            JobStartReason startReason,
            DateTimeOffset dueTime,
            bool dueTimeWasOverridden,
            DateTimeOffset startTime,
            JobRunStatus status,
            StringWriter outputWriter)
        {
            this.RunIndex = runIndex;
            this.StartReason = startReason;
            this.DueTime = dueTime;
            this.DueTimeWasOverridden = dueTimeWasOverridden;
            this.StartTime = startTime;
            this.Status = status;
            this.OutputWriter = outputWriter;
        }

        internal int RunIndex { get; }
        internal JobStartReason StartReason { get; }
        internal DateTimeOffset DueTime { get; }
        internal bool DueTimeWasOverridden { get; }
        internal DateTimeOffset StartTime { get; }
        internal DateTimeOffset? EndTime { get; set; }
        internal JobRunStatus Status { get; set; }
        internal StringWriter OutputWriter { get; }
        internal Exception Exception { get; set; }

        internal JobRunInfo Build()
        {
            return new JobRunInfo(
                this.RunIndex,
                this.StartReason,
                this.DueTime,
                this.DueTimeWasOverridden,
                this.StartTime,
                this.EndTime,
                this.Status,
                this.OutputWriter.ToString(),
                this.Exception);
        }
    }
}
