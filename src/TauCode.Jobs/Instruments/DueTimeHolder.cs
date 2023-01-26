using Serilog;
using TauCode.Infrastructure.Time;
using TauCode.Jobs.Schedules;

namespace TauCode.Jobs.Instruments;

internal class DueTimeHolder : IDisposable
{
    #region Fields

    private ISchedule _schedule;
    private DateTimeOffset? _overriddenDueTime;

    private DateTimeOffset _scheduleDueTime; // calculated

    private bool _isDisposed;
    private readonly string _jobName;

    private readonly object _lock;
    private readonly ILogger? _logger;

    #endregion

    #region Constructor

    internal DueTimeHolder(string jobName, ILogger? logger)
    {
        _logger = logger;
        _jobName = jobName;
        _schedule = NeverSchedule.Instance;
        _lock = new object();
        this.UpdateScheduleDueTime();
    }

    #endregion

    #region Private

    private void CheckNotDisposed()
    {
        lock (_lock)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(_jobName);
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
                    throw new InvalidOperationException("Cannot override due time in the past."); // already came
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
                _logger?.Warning(
                    "Inside method '{0:l}'. Rejected attempt to update schedule due time of a disposed '{1}'.",
                    nameof(UpdateScheduleDueTime),
                    this.GetType().FullName);

                return;
            }

            try
            {
                _scheduleDueTime = _schedule.GetDueTimeAfter(now.AddTicks(1));
                if (_scheduleDueTime < now)
                {
                    _logger?.Warning(
                        "Inside method '{0:l}'. Due time is earlier than current time. Due time is changed to 'never'.",
                        nameof(UpdateScheduleDueTime));

                    _scheduleDueTime = JobExtensions.Never;
                }
                else if (_scheduleDueTime > JobExtensions.Never)
                {
                    _logger?.Warning(
                        "Inside method '{0:l}'. Due time is later than 'never'. Due time is changed to 'never'.",
                        nameof(UpdateScheduleDueTime));

                    _scheduleDueTime = JobExtensions.Never;
                }

            }
            catch (Exception ex)
            {
                _scheduleDueTime = JobExtensions.Never;

                _logger?.Warning(
                    ex,
                    "Inside method '{0:l}'. An exception was thrown on attempt to calculate due time. Due time is changed to 'never'.",
                    nameof(UpdateScheduleDueTime));
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