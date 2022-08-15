using NUnit.Framework;
using Serilog;
using System.Text;
using TauCode.Infrastructure.Time;
using TauCode.IO;

namespace TauCode.Jobs.Tests.Jobs;

// todo clean

[TestFixture]
public partial class JobTests
{
    //private StringLogger _logger;
    private ILogger _logger = null!;
    private StringWriterWithEncoding _writer = null!;

    private string CurrentLog => _writer.ToString();

    // todo: describe what's going on here - why 5000 and 0?
    //private const int SetUpTimeout = 5000;
    private const int SetUpTimeout = 0;

    [SetUp]
    public async Task SetUp()
    {
        TimeProvider.Reset();
        GC.Collect();
        await Task.Delay(SetUpTimeout);

        _writer = new StringWriterWithEncoding(Encoding.UTF8);
        _logger = new LoggerConfiguration()
            .WriteTo.TextWriter(
                _writer,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}]{ObjectTag} {Message}{NewLine}{Exception}"
            )
            .MinimumLevel.Verbose()
            .CreateLogger();
        Log.Logger = _logger;

        //_logger = new StringLogger();

        //var collection = new LoggerProviderCollection();

        //Log.Logger = new LoggerConfiguration()
        //    .WriteTo.Providers(collection)
        //    .MinimumLevel
        //    .Debug()
        //    .CreateLogger();

        //var providerMock = new Mock<ILoggerProvider>();
        //providerMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_logger);

        //collection.AddProvider(providerMock.Object);
    }
}