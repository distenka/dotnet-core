using Microsoft.Extensions.Logging;

namespace Distenka.Tests.Cli;

/// <summary>Configuration of <see cref="LoggingProcessor"/>: what to log, at which level, with which exception.</summary>
public class LoggingConfig : Config
{
    /// <summary>A <see cref="LogLevel"/> name.</summary>
    public string Level { get; set; } = nameof(LogLevel.Information);

    public string Message { get; set; } = "";

    /// <summary>When greater than zero, the message is this many 'x' characters instead of <see cref="Message"/>.</summary>
    public int MessageLength { get; set; }

    /// <summary><c>timeout</c>: an unthrown <see cref="TimeoutException"/>; <c>thrown</c>: a thrown and caught <see cref="InvalidOperationException"/> (has a stack trace).</summary>
    public string? Exception { get; set; }
}

/// <summary>
/// Logs one event through <see cref="ILogger{TCategoryName}"/> and succeeds — used by the DCS-042 process-level tests of the
/// stdout line logger (ADR-009).
/// </summary>
public class LoggingProcessor : Processor<LoggingConfig>
{
    readonly ILogger<LoggingProcessor> _logger;

    public LoggingProcessor(LoggingConfig config, ILogger<LoggingProcessor> logger)
        : base(config)
    {
        _logger = logger;
    }

    public override Task<Result> ProcessAsync()
    {
        var level = Enum.Parse<LogLevel>(Config.Level, ignoreCase: true);
        var message = Config.MessageLength > 0 ? new string('x', Config.MessageLength) : Config.Message;

        Exception? exception = Config.Exception switch
        {
            "timeout" => new TimeoutException("timed out"),
            "thrown" => Thrown(),
            _ => null
        };

        // The message is passed as a value, not as the template, so braces in test messages are not parsed.
        _logger.Log(level, exception, "{Message}", message);

        return Task.FromResult(Result.Success());
    }

    static Exception Thrown()
    {
        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (InvalidOperationException ex)
        {
            return ex;
        }
    }
}
