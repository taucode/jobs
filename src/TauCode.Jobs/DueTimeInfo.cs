using System;

namespace TauCode.Jobs
{
    internal readonly struct DueTimeInfo
    {
        internal DueTimeInfo(DateTimeOffset scheduleDue, DateTimeOffset? overriddenDueTime)
        {
            this.ScheduleDueTime = scheduleDue;
            this.OverriddenDueTime = overriddenDueTime;
        }

        internal DateTimeOffset ScheduleDueTime { get; }
        internal DateTimeOffset? OverriddenDueTime { get; }
    }
}
