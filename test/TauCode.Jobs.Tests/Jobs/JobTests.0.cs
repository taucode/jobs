using System;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace TauCode.Jobs.Tests.Jobs
{
    [TestFixture]
    public partial class JobTests
    {
        // todo: describe what's going on here - why 5000 and 0?
        //private const int SetUpTimeout = 5000;
        private const int SetUpTimeout = 0;

        private StringWriterWithEncoding _logWriter;

        [SetUp]
        public async Task SetUp()
        {
            TimeProvider.Reset();
            GC.Collect();
            await Task.Delay(SetUpTimeout);

            _logWriter = new StringWriterWithEncoding(Encoding.UTF8);
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.TextWriter(_logWriter)
                .CreateLogger();
        }
    }
}
