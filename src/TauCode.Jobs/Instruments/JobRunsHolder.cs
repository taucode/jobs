using System;
using System.Collections.Generic;
using System.Linq;

namespace TauCode.Jobs.Instruments
{
    internal class JobRunsHolder
    {
        private JobRunInfo? _currentRun;
        private readonly List<JobRunInfo> _list;

        private readonly object _lock;

        internal JobRunsHolder()
        {
            _list = new List<JobRunInfo>();
            _lock = new object();
        }

        internal void Start(JobRunInfo initialRunInfo)
        {
            // todo: check _currentRun == null (log if not?)

            lock (_lock)
            {
                _currentRun = initialRunInfo;
            }
        }

        internal void Finish(JobRunInfo finalRunInfo)
        {
            lock (_lock)
            {
                _list.Add(finalRunInfo);
                _currentRun = null;
            }
        }

        internal Tuple<JobRunInfo?, IReadOnlyList<JobRunInfo>> Get(int? maxRunCount)
        {
            if (maxRunCount.HasValue && maxRunCount.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRunCount));
            }

            lock (_lock)
            {
                var item1 = _currentRun;

                var count = _list.Count;
                if (maxRunCount.HasValue)
                {
                    count = Math.Min(count, maxRunCount.Value);
                }

                IReadOnlyList<JobRunInfo> item2 = _list
                    .Take(count)
                    .ToList();

                return Tuple.Create(item1, item2);
            }
        }

        internal int Count
        {
            get
            {
                lock (_lock)
                {
                    return _list.Count;
                }
            }
        }
    }
}
