using System;
using System.IO;

namespace TauCode.Jobs.Instruments
{
    internal readonly struct JobProperties
    {
        internal JobProperties(
            JobDelegate routine,
            object parameter,
            IProgressTracker progressTracker,
            TextWriter output)
        {
            this.Routine = routine ?? throw new ArgumentNullException(nameof(routine));
            this.Parameter = parameter;
            this.ProgressTracker = progressTracker;
            this.Output = output;
        }

        internal JobDelegate Routine { get; }
        internal object Parameter { get; }
        internal IProgressTracker ProgressTracker { get; }
        internal TextWriter Output { get; }
    }
}
