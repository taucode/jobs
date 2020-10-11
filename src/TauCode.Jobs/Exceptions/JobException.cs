using System;
using System.Runtime.Serialization;
using TauCode.Working.Exceptions;

namespace TauCode.Jobs.Exceptions
{
    [Serializable]
    public class JobException : WorkingException
    {
        public JobException()
        {
        }

        public JobException(string message)
            : base(message)
        {
        }

        public JobException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected JobException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}
