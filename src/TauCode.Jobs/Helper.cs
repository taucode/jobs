using Microsoft.Extensions.Logging;
using System.Text;

namespace TauCode.Jobs;

internal static class Helper
{
    private static string BuildFullMessage(string message, Type objectType, string operationName)
    {
        if (objectType == null)
        {
            throw new ArgumentNullException(nameof(objectType));
        }

        var sb = new StringBuilder();
        sb.Append(message);

        sb.Append($" Type: '{objectType.FullName}'.");

        if (operationName != null)
        {
            sb.Append($" Operation: '{operationName}'.");
        }

        return sb.ToString();

    }

    internal static void LogWarningEx(this ILogger logger, Exception exception, string message, Type objectType, string operationName)
    {
        if (logger == null)
        {
            return;
        }

        var fullMessage = BuildFullMessage(message, objectType, operationName);

        if (exception == null)
        {
            logger.LogWarning(fullMessage);
        }
        else
        {
            logger.LogWarning(exception, fullMessage);
        }
    }

    internal static void LogDebugEx(this ILogger logger, Exception exception, string message, Type objectType, string operationName)
    {
        if (logger == null)
        {
            return;
        }

        var fullMessage = BuildFullMessage(message, objectType, operationName);

        if (exception == null)
        {
            logger.LogDebug(fullMessage);
        }
        else
        {
            logger.LogDebug(exception, fullMessage);
        }
    }

    internal static void LogInformationEx(this ILogger logger, Exception exception, string message, Type objectType, string operationName)
    {
        if (logger == null)
        {
            return;
        }

        var fullMessage = BuildFullMessage(message, objectType, operationName);

        if (exception == null)
        {
            logger.LogInformation(fullMessage);
        }
        else
        {
            logger.LogInformation(exception, fullMessage);
        }
    }

}