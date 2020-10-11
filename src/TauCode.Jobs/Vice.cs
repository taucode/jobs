using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TauCode.Extensions;
using TauCode.Infrastructure.Time;
using TauCode.Jobs.Exceptions;
using TauCode.Working;

namespace TauCode.Jobs
{
    internal class Vice : LoopWorkerBase
    {
        #region Fields

        private readonly Dictionary<string, Employee> _employees;
        private readonly object _lock;

        private readonly ObjectLogger _logger; // todo: WorkerBase will have its own ObjectLogger.
        private bool _startedWorking;

        #endregion

        #region Constructor

        internal Vice()
        {
            _employees = new Dictionary<string, Employee>();
            _lock = new object();

            _logger = new ObjectLogger(this, null);
        }

        #endregion

        #region Overridden

        protected override Task<TimeSpan> DoWork(CancellationToken token)
        {
            _logger.Debug("Entered method", nameof(DoWork));

            var now = TimeProvider.GetCurrentTime();
            var employeesToWakeUp = new List<Tuple<Employee, DueTimeInfo>>();
            var earliest = JobExtensions.Never;

            lock (_lock)
            {
                _startedWorking = true;

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
                        _logger.Information(
                            $"Job '{employee.Name}' was started. Reason: '{reason}'.",
                            nameof(DoWork));
                        break;

                    case JobStartResult.CompletedSynchronously:
                        _logger.Information(
                            $"Job '{employee.Name}' completed synchronously. Reason of start was '{reason}'.",
                            nameof(DoWork));
                        break;

                    case JobStartResult.AlreadyRunning:
                        _logger.Information(
                            $"Job '{employee.Name}' already running. Attempted to start due to reason '{reason}'.",
                            nameof(DoWork));
                        break;

                    case JobStartResult.Disabled:
                        _logger.Information(
                            $"Job '{employee.Name}' is disabled. Attempted to start due to reason '{reason}'.",
                            nameof(DoWork));
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

            _logger.Debug($"Going to vacation, length is '{vacationTimeout}'.", nameof(DoWork));

            return Task.FromResult(vacationTimeout);
        }

        protected override void OnDisposed()
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

        #endregion

        #region Internal

        internal IJob CreateJob(string jobName)
        {
            lock (_lock)
            {
                if (_employees.ContainsKey(jobName))
                {
                    throw new InvalidJobOperationException($"Job '{jobName}' already exists.");
                }

                var employee = new Employee(this, jobName);
                if (this.IsLoggingEnabled)
                {
                    employee.EnableLogging(true);
                }

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
                    throw new InvalidJobOperationException($"Job not found: '{jobName}'.");
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
            _logger.Debug(pulseReason, nameof(PulseWork));
            this.AdvanceWorkGeneration();
        }

        internal bool IsLoggingEnabled => _logger.IsEnabled;

        internal void EnableLogging(bool enable)
        {
            _logger.IsEnabled = enable;
            lock (_lock)
            {
                foreach (var employee in _employees.Values)
                {
                    employee.EnableLogging(enable);
                }
            }
        }

        internal bool StartedWorking()
        {
            lock (_lock)
            {
                return _startedWorking;
            }
        }

        #endregion
    }
}
