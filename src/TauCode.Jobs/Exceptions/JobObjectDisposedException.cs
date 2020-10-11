using System;
using System.Runtime.Serialization;

namespace TauCode.Jobs.Exceptions
{
    [Serializable]
    public class JobObjectDisposedException : InvalidJobOperationException
    {
        private JobObjectDisposedException(string objectName, string message)
            : base(message)
        {
            this.ObjectName = objectName;
        }

        public JobObjectDisposedException(string objectName)
            : this(objectName, $"'{objectName}' is disposed.")
        {
            this.ObjectName = objectName;
        }

        protected JobObjectDisposedException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }

        public string ObjectName { get; }
    }
}
