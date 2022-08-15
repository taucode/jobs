using Serilog;
using TauCode.Extensions;
using TauCode.Infrastructure.Time;
using TauCode.Working;

namespace TauCode.Jobs;

// todo clean

internal class Vice : LoopWorkerBase
{
    #region Fields

    private readonly Dictionary<string, Employee> _employees;
    private readonly object _lock;

    #endregion

    #region Constructor

    internal Vice(ILogger? logger)
        : base(logger)
    {
        _employees = new Dictionary<string, Employee>();
        _lock = new object();
    }

    #endregion

    #region Overridden

    protected override Task<TimeSpan> DoWork(CancellationToken token)
    {
        // todo: verbose.
        this.ContextLogger?.Debug(
            "Inside method '{0:l}'. Entered.",
            nameof(DoWork));

        //this.Logger.LogDebugEx(null, "Entered method", this.GetType(), nameof(DoWork));

        var now = TimeProvider.GetCurrentTime();
        var employeesToWakeUp = new List<Tuple<Employee, DueTimeInfo>>();
        var earliest = JobExtensions.Never;

        lock (_lock)
        {
            foreach (var employee in _employees.Values)
            {
                var info = employee.GetDueTimeInfoForVice(false);

                if (!info.HasValue)
                {
                    continue;
                }

                var dueTime = info.Value.GetEffectiveDueTime();

                if (now >= dueTime)
                {
                    // due time has come!
                    employeesToWakeUp.Add(Tuple.Create(employee, info.Value));
                }
                else
                {
                    earliest = DateTimeOffsetExtensions.Min(earliest, dueTime);
                }
            }
        }

        foreach (var tuple in employeesToWakeUp)
        {
            var employee = tuple.Item1;
            var isOverridden = tuple.Item2.IsDueTimeOverridden();
            var reason = isOverridden ? JobStartReason.OverriddenDueTime : JobStartReason.ScheduleDueTime;

            var startResult = employee.Start(reason, token);

            switch (startResult)
            {
                case JobStartResult.Started:
                    this.ContextLogger?.Information(
                        "Inside method '{0:l}'. Job '{1:l}' was started. Reason: '{2}'.",
                        nameof(DoWork),
                        employee.Name,
                        reason);

                    //this.Logger.LogInformationEx(
                    //    null,
                    //    $"Job '{employee.Name}' was started. Reason: '{reason}'.",
                    //    this.GetType(),
                    //    nameof(DoWork));

                    break;

                case JobStartResult.CompletedSynchronously:
                    this.ContextLogger?.Information(
                        "Inside method '{0:l}'. Job '{1:l}' completed synchronously. Reason of start was '{2}'.",
                        nameof(DoWork),
                        employee.Name,
                        reason);

                    //this.Logger.LogInformationEx(
                    //    null,
                    //    $"Job '{employee.Name}' completed synchronously. Reason of start was '{reason}'.",
                    //    this.GetType(),
                    //    nameof(DoWork));

                    break;

                case JobStartResult.AlreadyRunning:
                    this.ContextLogger?.Information(
                        "Inside method '{0:l}'. Job '{1:l}' already running. Attempted to start due to reason '{2}'.",
                        nameof(DoWork),
                        employee.Name,
                        reason);

                    //this.Logger.LogInformationEx(
                    //    null,
                    //    $"Job '{employee.Name}' already running. Attempted to start due to reason '{reason}'.",
                    //    this.GetType(),
                    //    nameof(DoWork));

                    break;

                case JobStartResult.Disabled:
                    this.ContextLogger?.Information(
                        "Inside method '{0:l}'. Job '{1:l}' is disabled. Attempted to start due to reason '{2}'.",
                        nameof(DoWork),
                        employee.Name,
                        reason);

                    //this.Logger.LogInformationEx(
                    //    null,
                    //    $"Job '{employee.Name}' is disabled. Attempted to start due to reason '{reason}'.",
                    //    this.GetType(),
                    //    nameof(DoWork));

                    break;
            }

            // when to visit you again, Employee?
            var nextDueTimeInfo = employee.GetDueTimeInfoForVice(true);

            if (nextDueTimeInfo.HasValue
               ) // actually, should have, he could not finish work and got disposed that fast, but who knows...
            {
                var nextDueTime = nextDueTimeInfo.Value.GetEffectiveDueTime();
                if (nextDueTime > now)
                {
                    earliest = DateTimeOffsetExtensions.Min(earliest, nextDueTime);
                }
            }
        }

        var vacationTimeout = earliest - now;

        // todo: verbose
        this.ContextLogger?.Debug(
            "Inside method '{0:l}'. Going to vacation, length is '{1}'.",
            nameof(DoWork),
            vacationTimeout);

        //this.Logger.LogDebugEx(
        //    null,
        //    $"Going to vacation, length is '{vacationTimeout}'.",
        //    this.GetType(),
        //    nameof(DoWork));

        return Task.FromResult(vacationTimeout);
    }

    protected override void OnAfterDisposed()
    {
        IList<Employee> list;
        lock (_lock)
        {
            list = _employees.Values.ToList();
        }

        foreach (var employee in list)
        {
            employee.Dispose();
        }
    }

    public override bool IsPausingSupported => false;

    #endregion

    #region Internal

    internal IJob CreateJob(string jobName)
    {
        lock (_lock)
        {
            if (_employees.ContainsKey(jobName))
            {
                throw new InvalidOperationException($"Job '{jobName}' already exists.");
            }

            var employee = new Employee(this, this.OriginalLogger, jobName);

            _employees.Add(employee.Name, employee);

            this.PulseWork($"Pulsing due to '{nameof(CreateJob)}'.");
            return employee.GetJob();
        }
    }

    internal void Remove(string jobName)
    {
        lock (_lock)
        {
            _employees.Remove(jobName);
        }
    }

    internal IJob GetJob(string jobName)
    {
        lock (_lock)
        {
            var employee = _employees.GetValueOrDefault(jobName);
            if (employee == null)
            {
                throw new InvalidOperationException($"Job not found: '{jobName}'.");
            }

            return employee.GetJob();
        }
    }

    internal IReadOnlyList<string> GetJobNames()
    {
        lock (_lock)
        {
            return _employees.Keys.ToList();
        }
    }

    internal void PulseWork(string pulseReason)
    {
        this.ContextLogger?.Debug(
            "Inside method '{0:l}'. Pulse reason: {1}",
            nameof(PulseWork),
            pulseReason);

        //this.Logger.LogDebugEx(
        //    null,
        //    pulseReason,
        //    this.GetType(),
        //    nameof(PulseWork));

        this.AbortVacation();
    }

    #endregion
}