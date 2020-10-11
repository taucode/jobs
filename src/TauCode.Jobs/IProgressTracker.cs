using System;

namespace TauCode.Jobs
{
    public interface IProgressTracker
    {
        void UpdateProgress(decimal? percentCompleted, DateTimeOffset? estimatedEndTime);
    }
}
