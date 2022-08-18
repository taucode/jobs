using TauCode.Working;

namespace TauCode.Jobs;

public interface IJobManager : IWorker
{
    IJob CreateJob(string jobName);

    IReadOnlyList<string> GetJobNames();

    IJob GetJob(string jobName);
}