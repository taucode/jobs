using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Serilog;
using Serilog.Extensions.Logging;
using System;
using System.Threading.Tasks;
using TauCode.Infrastructure.Lab;
using TauCode.Infrastructure.Time;

namespace TauCode.Jobs.Tests.Jobs
{
    [TestFixture]
    public partial class JobTests
    {
        private StringLoggerLab _logger;
        private string CurrentLog => _logger.ToString();

        // todo: describe what's going on here - why 5000 and 0?
        //private const int SetUpTimeout = 5000;
        private const int SetUpTimeout = 0;

        [SetUp]
        public async Task SetUp()
        {
            TimeProvider.Reset();
            GC.Collect();
            await Task.Delay(SetUpTimeout);

            _logger = new StringLoggerLab();

            var collection = new LoggerProviderCollection();

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Providers(collection)
                .MinimumLevel
                .Debug()
                .CreateLogger();

            var providerMock = new Mock<ILoggerProvider>();
            providerMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_logger);

            collection.AddProvider(providerMock.Object);
        }
    }
}
