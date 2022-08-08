using System.Runtime.Serialization;

namespace TauCode.Jobs.Exceptions;

[Serializable]
public class JobFailedToStartException : Exception
{
    public JobFailedToStartException(Exception inner)
        : base("Job failed to start.", inner)
    {
    }

    protected JobFailedToStartException(
        SerializationInfo info,
        StreamingContext context) : base(info, context)
    {
    }
}