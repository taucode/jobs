using System;
using TauCode.Infrastructure.Time;
using TauCode.Jobs.Exceptions;
using TauCode.Jobs.Schedules;
using TauCode.Working;

namespace TauCode.Jobs.Instruments
{
    internal class DueTimeHolder : IDisposable
    {
        #region Fields

        private ISchedule _schedule;
        private DateTimeOffset? _overriddenDueTime;

        private DateTimeOffset _scheduleDueTime; // calculated

        private bool _isDisposed;
        private readonly string _jobName;

        private readonly object _lock;

        private readonly ObjectLogger _logger;

        #endregion

        #region Constructor

        internal DueTimeHolder(string jobName)
        {
            _jobName = jobName;
            _schedule = NeverSchedule.Instance;
            _lock = new object();
            this.UpdateScheduleDueTime();

            _logger = new ObjectLogger(this, _jobName);
        }

        #endregion

        #region Private

        private void CheckNotDisposed()
        {
            lock (_lock)
            {
                if (_isDisposed)
                {
                    throw new JobObjectDisposedException(_jobName);
                }
            }
        }

        #endregion

        #region Internal

        internal ISchedule Schedule
        {
            get
            {
                lock (_lock)
                {
                    return _schedule;
                }
            }
            set
            {
                lock (_lock)
                {
                    this.CheckNotDisposed();
                    _schedule = value ?? throw new ArgumentNullException(nameof(IJob.Schedule));
                    _overriddenDueTime = null;
                    this.UpdateScheduleDueTime();
                }
            }
        }

        internal DateTimeOffset? OverriddenDueTime
        {
            get
            {
                lock (_lock)
                {
                    return _overriddenDueTime;
                }
            }
            set
            {
                lock (_lock)
                {
                    this.CheckNotDisposed();

                    var now = TimeProvider.GetCurrentTime();
                    if (now > value)
                    {
                        throw new JobException("Cannot override due time in the past."); // already came
                    }

                    _overriddenDueTime = value;
                }
            }
        }

        internal void UpdateScheduleDueTime()
        {
            var now = TimeProvider.GetCurrentTime();
            lock (_lock)
            {
                if (_isDisposed)
                {
                    _logger.Warning(
                        $"Rejected attempt to update schedule due time of an exposed '{this.GetType().FullName}'.",
                        nameof(UpdateScheduleDueTime));
                    return;
                }

                try
                {
                    _scheduleDueTime = _schedule.GetDueTimeAfter(now.AddTicks(1));
                    if (_scheduleDueTime < now)
                    {
                        _logger.Warning(
                            "Due time is earlier than current time. Due time is changed to 'never'.",
                            nameof(UpdateScheduleDueTime));
                        _scheduleDueTime = JobExtensions.Never;
                    }
                    else if (_scheduleDueTime > JobExtensions.Never)
                    {
                        _logger.Warning(
                            "Due time is later than 'never'. Due time is changed to 'never'.",
                            nameof(UpdateScheduleDueTime));
                        _scheduleDueTime = JobExtensions.Never;
                    }

                }
                catch (Exception ex)
                {
                    _scheduleDueTime = JobExtensions.Never;

                    _logger.Warning(
                        "An exception was thrown on attempt to calculate due time. Due time is changed to 'never'.",
                        nameof(UpdateScheduleDueTime),
                        ex);
                }
            }
        }

        internal DueTimeInfo GetDueTimeInfo()
        {
            lock (_lock)
            {
                return new DueTimeInfo(_scheduleDueTime, _overriddenDueTime);
            }
        }

        internal void EnableLogging(bool enable)
        {
            _logger.IsEnabled = enable;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            lock (_lock)
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
            }
        }

        #endregion
    }
}
