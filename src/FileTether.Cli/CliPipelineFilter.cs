using ConsoleAppFramework;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace FileTether.Cli;

internal sealed class CliPipelineFilter : ConsoleAppFilter
{
    private readonly LoggingLevelSwitch _levelSwitch;

    public CliPipelineFilter(LoggingLevelSwitch levelSwitch, ConsoleAppFilter next)
        : base(next)
    {
        _levelSwitch = levelSwitch;
    }

    public override async Task InvokeAsync(ConsoleAppContext context, CancellationToken cancellationToken)
    {
        if (context.GlobalOptions is true)
        {
            _levelSwitch.MinimumLevel = LogEventLevel.Debug;
        }

        try
        {
            await Next.InvokeAsync(context, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Log.Fatal(exception, "Unhandled exception");
            Environment.ExitCode = ExitCodes.ProviderError;
        }
    }
}
