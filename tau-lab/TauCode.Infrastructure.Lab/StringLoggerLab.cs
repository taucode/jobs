using Microsoft.Extensions.Logging;
using System;
using System.Text;
using TauCode.Infrastructure.Time;

namespace TauCode.Infrastructure.Lab
{
    public class StringLoggerLab : ILogger
    {
        private readonly StringBuilder _stringBuilder;

        public StringLoggerLab(StringBuilder stringBuilder)
        {
            _stringBuilder = stringBuilder ?? throw new ArgumentNullException(nameof(stringBuilder));
        }

        public StringLoggerLab()
            : this(new StringBuilder())
        {
        }

        public virtual IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public virtual bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public virtual void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var timeStamp = TimeProvider.GetCurrentTime();
            var timeStampString = timeStamp.ToString("yyyy-MM-dd HH:mm:ss+00:00");
            var message = formatter(state, exception);
            var exceptionString = exception?.ToString();

            _stringBuilder.Append($"[{timeStampString}] [{logLevel}] {message}");
            
            if (exceptionString != null)
            {
                _stringBuilder.AppendLine();
                _stringBuilder.Append(exceptionString);
            }
        }

        public override string ToString() => _stringBuilder.ToString();
    }
}