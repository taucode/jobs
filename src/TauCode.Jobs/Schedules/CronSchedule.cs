using System;

namespace TauCode.Jobs.Schedules
{
    public class CronSchedule : ISchedule
    {
        public string Description { get; set; }
        public DateTimeOffset GetDueTimeAfter(DateTimeOffset after)
        {
            throw new NotImplementedException();
        }
    }
}
