using Serilog;

namespace TauCode.Jobs.Instruments;

internal class Runner : IDisposable
{
    #region Fields

    private bool _isEnabled;
    private bool _isDisposed;

    private RunContext _runContext;

    private readonly object _lock;
    private readonly ILogger _logger;

    #endregion

    #region Constructor

    internal Runner(string jobName, ILogger logger)
    {
        this.JobName = jobName;
        _logger = logger;

        this.JobPropertiesHolder = new JobPropertiesHolder(this.JobName, _logger);
        this.DueTimeHolder = new DueTimeHolder(this.JobName, _logger);
        this.JobRunsHolder = new JobRunsHolder();

        _lock = new object();
    }

    #endregion

    #region Private

    private void CheckNotDisposed()
    {
        lock (_lock)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(this.JobName);
            }
        }
    }

    private RunContext Run(JobStartReason startReason, CancellationToken? token)
    {
        // always guarded by '_lock'
        var runContext = new RunContext(this, startReason, token, _logger);
        var startedRunContext = runContext.Start();
        return startedRunContext;
    }

    #endregion

    #region Internal

    internal string JobName { get; }

    internal bool IsEnabled
    {
        get
        {
            lock (_lock)
            {
                return _isEnabled;
            }
        }
        set
        {
            lock (_lock)
            {
                this.CheckNotDisposed();
                _isEnabled = value;
            }
        }
    }

    internal bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _runContext != null;
            }
        }
    }

    internal JobPropertiesHolder JobPropertiesHolder { get; }

    internal DueTimeHolder DueTimeHolder { get; }

    internal JobRunsHolder JobRunsHolder { get; }

    internal bool IsDisposed
    {
        get
        {
            lock (_lock)
            {
                return _isDisposed;
            }
        }
    }

    internal DueTimeInfo? GetDueTimeInfoForVice(bool future)
    {
        lock (_lock)
        {
            if (_isDisposed)
            {
                return null;
            }

            if (future)
            {
                this.DueTimeHolder.UpdateScheduleDueTime();
            }

            return this.DueTimeHolder.GetDueTimeInfo();
        }
    }

    internal bool Cancel()
    {
        lock (_lock)
        {
            this.CheckNotDisposed();

            if (_runContext == null)
            {
                return false;
            }

            _runContext.Cancel();
            return true;
        }
    }

    internal JobStartResult Start(JobStartReason startReason, CancellationToken? token)
    {
        if (startReason == JobStartReason.Force)
        {
            lock (_lock)
            {
                this.CheckNotDisposed();

                if (this.IsRunning)
                {
                    throw new InvalidOperationException($"Job '{this.JobName}' is already running.");
                }

                if (!this.IsEnabled)
                {
                    throw new InvalidOperationException($"Job '{this.JobName}' is disabled.");
                }

                _runContext = this.Run(startReason, token);

                if (_runContext == null)
                {
                    return JobStartResult.CompletedSynchronously;
                }

                return JobStartResult.Started;
            }
        }
        else
        {
            lock (_lock)
            {
                try
                {
                    if (this.IsRunning)
                    {
                        return JobStartResult.AlreadyRunning;
                    }

                    if (!this.IsEnabled)
                    {
                        return JobStartResult.Disabled;
                    }

                    _runContext = this.Run(startReason, token);

                    if (_runContext == null)
                    {
                        return JobStartResult.CompletedSynchronously;
                    }

                    return JobStartResult.Started;
                }
                finally
                {
                    // started via due time (either overridden or scheduled), so clear overridden due time.
                    this.DueTimeHolder.OverriddenDueTime = null;
                }
            }
        }
    }

    internal JobInfo GetInfo(int? maxRunCount)
    {
        var tuple = this.JobRunsHolder.Get(maxRunCount);
        var currentRun = tuple.Item1;
        var runs = tuple.Item2;

        var dueTimeInfo = this.DueTimeHolder.GetDueTimeInfo();

        return new JobInfo(
            currentRun,
            dueTimeInfo.GetEffectiveDueTime(),
            dueTimeInfo.IsDueTimeOverridden(),
            this.JobRunsHolder.Count,
            runs);
    }

    internal JobRunStatus? Wait(int millisecondsTimeout)
    {
        if (millisecondsTimeout < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout));
        }

        RunContext runContext;

        lock (_lock)
        {
            this.CheckNotDisposed();
            runContext = _runContext;
        }

        if (runContext == null)
        {
            return JobRunStatus.Completed; // nothing to wait for, not in process of running.
        }

        return runContext.Wait(millisecondsTimeout);
    }

    internal JobRunStatus? Wait(TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        var millisecondsTimeout = (int)timeout.TotalMilliseconds;
        return this.Wait(millisecondsTimeout);
    }

    internal void OnTaskEnded()
    {
        lock (_lock)
        {
            _runContext = null;
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

            try
            {
                _runContext?.Cancel();
                _runContext = null;
            }
            catch
            {
                // dismiss; Dispose shouldn't throw
            }

            this.JobPropertiesHolder.Dispose();
            this.DueTimeHolder.Dispose();
        }
    }

    #endregion
}