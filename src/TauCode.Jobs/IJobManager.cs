using TauCode.Working.Slavery;

namespace TauCode.Jobs;

public interface IJobManager : ISlave
{
    IJob CreateJob(string jobName);

    IReadOnlyList<string> GetJobNames();

    IJob GetJob(string jobName);
}