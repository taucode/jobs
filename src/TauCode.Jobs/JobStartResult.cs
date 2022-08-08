namespace TauCode.Jobs;

internal enum JobStartResult
{
    Started = 1,
    CompletedSynchronously = 2,
    AlreadyRunning = 3,
    Disabled = 4,
}