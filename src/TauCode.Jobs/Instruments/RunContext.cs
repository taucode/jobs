using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TauCode.Extensions;
using TauCode.Infrastructure.Time;
using TauCode.Working;

namespace TauCode.Jobs.Instruments
{
    internal class RunContext
    {
        #region Fields

        private readonly Runner _initiator;

        private readonly CancellationTokenSource _tokenSource;
        private readonly JobRunInfoBuilder _runInfoBuilder;
        private readonly StringWriterWithEncoding _systemWriter;

        private readonly Task _task;

        private readonly ObjectLogger _logger;

        #endregion

        #region Constructor

        internal RunContext(
            Runner initiator,
            JobStartReason startReason,
            CancellationToken? token)
        {
            _initiator = initiator;
            var jobProperties = _initiator.JobPropertiesHolder.ToJobProperties();

            _tokenSource = token.HasValue ?
                CancellationTokenSource.CreateLinkedTokenSource(token.Value)
                :
                new CancellationTokenSource();

            _systemWriter = new StringWriterWithEncoding(Encoding.UTF8);
            var writers = new List<TextWriter>
            {
                _systemWriter,
            };

            if (jobProperties.Output != null)
            {
                writers.Add(jobProperties.Output);
            }

            var multiTextWriter = new MultiTextWriter(Encoding.UTF8, writers);

            var dueTimeInfo = _initiator.DueTimeHolder.GetDueTimeInfo();
            var dueTime = dueTimeInfo.GetEffectiveDueTime();
            var dueTimeWasOverridden = dueTimeInfo.IsDueTimeOverridden();

            var now = TimeProvider.GetCurrentTime();

            _runInfoBuilder = new JobRunInfoBuilder(
                initiator.JobRunsHolder.Count,
                startReason,
                dueTime,
                dueTimeWasOverridden,
                now,
                JobRunStatus.Running,
                _systemWriter);

            _logger = new ObjectLogger(this, _initiator.JobName)
            {
                IsEnabled = _initiator.IsLoggingEnabled,
            };

            try
            {
                _task = jobProperties.Routine(
                    jobProperties.Parameter,
                    jobProperties.ProgressTracker,
                    multiTextWriter,
                    _tokenSource.Token);

                // todo: if routine returns null?

                if (_task.IsFaulted && _task.Exception != null)
                {
                    var ex = ExtractTaskException(_task.Exception);
                    multiTextWriter.WriteLine(ex);

                    _logger.Warning("Routine has thrown an exception.", "ctor", ex);
                    _task = Task.FromException(ex);
                }
            }
            catch (Exception ex)
            {
                // it is not an error if Routine throws, but let's log it as a warning.
                multiTextWriter.WriteLine(ex);

                _logger.Warning("Routine has thrown an exception.", "ctor", ex);
                _task = Task.FromException(ex);
            }
        }

        #endregion

        #region Private

        private void EndTask(Task task)
        {
            _logger.Debug($"Task ended. Status: {task.Status}", nameof(EndTask), task.Exception?.InnerException);

            JobRunStatus status;
            Exception exception = null;

            switch (task.Status)
            {
                case TaskStatus.RanToCompletion:
                    status = JobRunStatus.Completed;
                    break;

                case TaskStatus.Canceled:
                    status = JobRunStatus.Canceled;
                    break;

                case TaskStatus.Faulted:
                    status = JobRunStatus.Faulted;
                    exception = ExtractTaskException(task.Exception);
                    break;

                default:
                    status = JobRunStatus.Unknown; // actually, very strange and should never happen.
                    break;
            }

            var now = TimeProvider.GetCurrentTime();

            _runInfoBuilder.EndTime = now;
            _runInfoBuilder.Status = status;
            _runInfoBuilder.Exception = exception;

            var jobRunInfo = _runInfoBuilder.Build();
            _initiator.JobRunsHolder.Finish(jobRunInfo);

            _tokenSource.Dispose();
            _systemWriter.Dispose();

            _initiator.OnTaskEnded();
        }

        private static Exception ExtractTaskException(AggregateException taskException)
        {
            return taskException?.InnerException ?? taskException;
        }

        #endregion

        #region Internal

        internal RunContext Start()
        {
            _initiator.JobRunsHolder.Start(_runInfoBuilder.Build());

            if (_task.IsCompleted)
            {
                this.EndTask(_task);
                return null;
            }
            else
            {
                _task.ContinueWith(this.EndTask);
                return this;
            }
        }

        internal void Cancel()
        {
            _tokenSource.Cancel(); // todo: throws if disposed. take care of it and ut it.
        }

        internal JobRunStatus? Wait(int millisecondsTimeout)
        {
            try
            {
                var result = _task.Wait(millisecondsTimeout);

                if (result)
                {
                    return JobRunStatus.Completed;
                }

                return null;
            }
            catch (AggregateException ex)
            {
                var innerEx = ExtractTaskException(ex);

                if (innerEx is TaskCanceledException)
                {
                    return JobRunStatus.Canceled;
                }

                return JobRunStatus.Faulted;
            }
        }

        #endregion
    }
}
