using System;

namespace TauCode.Jobs
{
    public class ProgressEventArgs : EventArgs
    {
        public ProgressEventArgs(decimal percentCompleted)
        {
            this.PercentCompleted = percentCompleted;
        }

        public decimal PercentCompleted { get; }
    }
}
