using ConsoleAppFramework;
using FileTether.Cli.Commands;
using FileTether.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.Spectre;
using Spectre.Console;

namespace FileTether.Cli;

public static class Program
{
    private static readonly LoggingLevelSwitch s_levelSwitch = new(LogEventLevel.Warning);

    public static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(s_levelSwitch)
            .Enrich.FromLogContext()
            .WriteTo.Spectre()
            .CreateBootstrapLogger();

        try
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();

            builder.Services.AddSingleton(s_levelSwitch);
            builder.Services.AddSerilog((serviceProvider, configuration) =>
            {
                configuration
                    .MinimumLevel.ControlledBy(s_levelSwitch)
                    .ReadFrom.Services(serviceProvider)
                    .Enrich.FromLogContext()
                    .WriteTo.Spectre();
            });

            builder.Services.AddFileTetherCore();
            builder.Services.AddSingleton(AnsiConsole.Console);
            builder.Services.AddSingleton<ConsoleRenderer>();

            ConsoleApp.ConsoleAppBuilder app = builder.ToConsoleAppBuilder();

            app.ConfigureGlobalOptions((ref globalOptionsBuilder) =>
            {
                return globalOptionsBuilder.AddGlobalOption<bool>("--verbose");
            });

            app.UseFilter<CliPipelineFilter>();

            app.Add<InitCommand>();
            app.Add<StatusCommand>();
            app.Add<CheckCommand>();
            app.Add<PullCommand>();

            await app.RunAsync(args);

            return Environment.ExitCode;
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "Unhandled exception during startup");

            return ExitCodes.ProviderError;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
