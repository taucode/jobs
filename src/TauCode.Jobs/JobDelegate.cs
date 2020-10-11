using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TauCode.Jobs
{
    public delegate Task JobDelegate(
        object parameter,
        IProgressTracker progressTracker,
        TextWriter output,
        CancellationToken cancellationToken);
}
