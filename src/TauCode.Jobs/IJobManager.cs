using Microsoft.Extensions.Logging;

namespace TauCode.Jobs;

public interface IJobManager : IDisposable
{
    void Start();

    bool IsRunning { get; }

    bool IsDisposed { get; }

    IJob Create(string jobName);

    IReadOnlyList<string> GetNames();

    IJob Get(string jobName);

    ILogger Logger { get; set; }
}