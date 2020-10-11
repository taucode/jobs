using System;

namespace TauCode.Jobs
{
    public static class JobDiagnosticsHelper
    {
        public static void EnableLogging(IJobManager jobManager, bool enable)
        {
            if (jobManager == null)
            {
                throw new ArgumentNullException(nameof(jobManager));
            }

            if (jobManager is JobManager jobManagerInstance)
            {
                jobManagerInstance.IsLoggingEnabled = enable;
            }
            else
            {
                throw new ArgumentException($"'{nameof(jobManager)}' is not of type '{typeof(JobManager).FullName}'.");
            }
        }

        public static bool JobManagerStartedWorking(IJobManager jobManager)
        {
            if (jobManager == null)
            {
                throw new ArgumentNullException(nameof(jobManager));
            }

            if (jobManager is JobManager jobManagerInstance)
            {
                return jobManagerInstance.StartedWorking();
            }
            else
            {
                throw new ArgumentException($"'{nameof(jobManager)}' is not of type '{typeof(JobManager).FullName}'.");
            }
        }
    }
}
