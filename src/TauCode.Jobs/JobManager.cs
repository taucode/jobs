using System;
using System.Collections.Generic;
using TauCode.Jobs.Exceptions;

namespace TauCode.Jobs
{
    public class JobManager : IJobManager
    {
        #region Fields
        
        private readonly Vice _vice;

        #endregion

        #region Constructor

        public JobManager()
        {
            _vice = new Vice();
        }

        #endregion

        #region Private

        private void CheckJobName(string jobName, string jobNameParamName)
        {
            if (string.IsNullOrWhiteSpace(jobName))
            {
                throw new ArgumentException("Job name cannot be null or empty.", jobNameParamName);
            }
        }

        private void CheckCanWork()
        {
            if (this.IsDisposed)
            {
                throw new JobObjectDisposedException(typeof(IJobManager).FullName);
            }

            if (!this.IsRunning)
            {
                throw new InvalidJobOperationException($"'{typeof(IJobManager).FullName}' not started.");
            }
        }


        #endregion

        #region IJobManager Members

        public void Start()
        {
            try
            {
                _vice.Start();
            }
            catch (ObjectDisposedException)
            {
                throw new JobObjectDisposedException($"{typeof(IJobManager).FullName}");
            }
            catch (InappropriateWorkerStateException)
            {
                throw new InvalidJobOperationException($"'{typeof(IJobManager).FullName}' is already running.");
            }
        }

        public bool IsRunning => _vice.State == WorkerState.Running;

        public bool IsDisposed => _vice.IsDisposed;

        public IJob Create(string jobName)
        {
            this.CheckJobName(jobName, nameof(jobName));
            this.CheckCanWork();

            return _vice.CreateJob(jobName);
        }

        public IReadOnlyList<string> GetNames()
        {
            this.CheckCanWork();
            return _vice.GetJobNames();
        }

        public IJob Get(string jobName)
        {
            this.CheckJobName(jobName, nameof(jobName));
            this.CheckCanWork();

            return _vice.GetJob(jobName);
        }

        #endregion

        #region IDisposable Members

        public void Dispose() => _vice.Dispose();

        #endregion

        #region Internal

        internal bool IsLoggingEnabled
        {
            get => _vice.IsLoggingEnabled;
            set => _vice.EnableLogging(value);
        }

        internal bool StartedWorking() => _vice.StartedWorking();

        #endregion
    }
}
