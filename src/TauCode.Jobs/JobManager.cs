using Serilog;
using TauCode.Working;

namespace TauCode.Jobs;

public class JobManager : IJobManager
{
    #region Fields

    private readonly Vice _vice;

    #endregion

    #region Constructor

    public JobManager(ILogger? logger)
    {
        _vice = new Vice(logger);
    }

    #endregion

    #region Private

    private InvalidOperationException CreateCannotWorkException(string operationName)
    {
        throw new InvalidOperationException($"Cannot perform operation '{operationName}'. Job Manager is not running.");
    }

    private void CheckJobName(string jobName, string jobNameParamName)
    {
        if (string.IsNullOrWhiteSpace(jobName))
        {
            throw new ArgumentException("Job name cannot be null or empty.", jobNameParamName);
        }
    }

    private void CheckCanWork(string operationName)
    {
        if (this.IsDisposed)
        {
            throw new ObjectDisposedException(this.GetType().FullName);
        }

        if (!this.IsRunning)
        {
            throw this.CreateCannotWorkException(operationName);
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
            throw new ObjectDisposedException(this.GetType().FullName);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException("Cannot start Job Manager.", ex);
        }
    }

    public bool IsRunning => _vice.State == WorkerState.Running;

    public bool IsDisposed => _vice.IsDisposed;

    public IJob Create(string jobName)
    {
        this.CheckJobName(jobName, nameof(jobName));
        this.CheckCanWork(nameof(Create));

        return _vice.CreateJob(jobName);
    }

    public IReadOnlyList<string> GetNames()
    {
        this.CheckCanWork(nameof(GetNames));
        return _vice.GetJobNames();
    }

    public IJob Get(string jobName)
    {
        this.CheckJobName(jobName, nameof(jobName));
        this.CheckCanWork(nameof(Get));

        return _vice.GetJob(jobName);
    }

    public ILogger Logger
    {
        get => /*_vice.Logger*/ throw new NotImplementedException();
        set => /*_vice.Logger = value*/ throw new NotImplementedException();
    }

    #endregion

    #region IDisposable Members

    public void Dispose() => _vice.Dispose();

    #endregion
}