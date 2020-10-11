using System;
using System.Collections.Generic;
using System.Linq;
using TauCode.Extensions;

namespace TauCode.Jobs.Schedules
{
    // todo clean up
    public class SimpleSchedule : ISchedule
    {
        private readonly IList<DateTimeOffset> _concreteMoments;

        public SimpleSchedule(
            SimpleScheduleKind kind,
            int multiplier,
            DateTimeOffset baseTime,
            IEnumerable<TimeSpan> concreteOffsets = null)
        {
            if (multiplier <= 0)
            {
                throw new NotImplementedException(); // todo
            }

            this.Kind = kind;
            this.Multiplier = multiplier;
            this.BaseTime = baseTime;

            this.TimeSpan = this.CalculateTimeSpan();

            var concreteMoments = new List<DateTimeOffset>();

            if (concreteOffsets != null)
            {
                var curr = baseTime;
                foreach (var offset in concreteOffsets)
                {
                    // todo: non-negative
                    curr = curr.Add(offset);
                    concreteMoments.Add(curr);
                }
            }

            _concreteMoments = concreteMoments
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }

        private TimeSpan CalculateTimeSpan()
        {
            TimeSpan timeSpan;

            switch (this.Kind)
            {
                case SimpleScheduleKind.Second:
                    timeSpan = TimeSpan.FromSeconds(this.Multiplier);
                    break;

                case SimpleScheduleKind.Minute:
                    timeSpan = TimeSpan.FromMinutes(this.Multiplier);
                    break;

                case SimpleScheduleKind.Hour:
                    timeSpan = TimeSpan.FromHours(this.Multiplier);
                    break;

                case SimpleScheduleKind.Day:
                    timeSpan = TimeSpan.FromDays(this.Multiplier);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            return timeSpan;
        }

        public SimpleScheduleKind Kind { get; }

        public int Multiplier { get; }

        public DateTimeOffset BaseTime { get; }

        public TimeSpan TimeSpan { get; }

        public string Description { get; set; }

        public DateTimeOffset GetDueTimeAfter(DateTimeOffset after)
        {
            if (after <= this.BaseTime)
            {
                return this.BaseTime;
            }
            else
            {
                var spanCount = (int) ((after - this.BaseTime).TotalMilliseconds / this.TimeSpan.TotalMilliseconds);
                var result = this.BaseTime.AddMilliseconds(spanCount * this.TimeSpan.TotalMilliseconds);

                while (true)
                {
                    if (result > after)
                    {
                        // maybe there is concrete moment between 'after' and 'result'?
                        var resultCopy = result; // capture of variable!
                        var idx = _concreteMoments.FindFirstIndexOf(x => x > after && x < resultCopy);

                        if (idx >= 0)
                        {
                            result = _concreteMoments[idx];
                        }

                        return result;
                    }

                    result = result.AddMilliseconds(this.TimeSpan.TotalMilliseconds);
                }
            }
        }
    }
}