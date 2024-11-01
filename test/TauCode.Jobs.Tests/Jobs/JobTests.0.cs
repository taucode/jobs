﻿using System.Text;
using NUnit.Framework;
using Serilog;
using TauCode.Infrastructure.Time;
using TauCode.IO;

using TimeProvider = TauCode.Infrastructure.Time.TimeProvider;

namespace TauCode.Jobs.Tests.Jobs;

#pragma warning disable NUnit1032

[TestFixture]
public partial class JobTests
{
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
    }
}