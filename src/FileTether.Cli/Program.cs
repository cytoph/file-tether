using ConsoleAppFramework;
using FileTether.Cli.Commands;
using FileTether.Core;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.Spectre;
using Spectre.Console;

namespace FileTether.Cli;

public static class Program
{
    // Not hosted via Host.CreateApplicationBuilder: ConfigureGlobalOptions makes ConsoleAppFramework
    // rebuild its service provider at run time from the builder's own registrations, silently
    // discarding a host-built provider (Cysharp/ConsoleAppFramework, still present in 5.7.13).
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
            ConsoleApp.ConsoleAppBuilder app = ConsoleApp.Create();

            app.ConfigureServices(RegisterServices);

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

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton(s_levelSwitch);
        services.AddSerilog((serviceProvider, configuration) =>
        {
            configuration
                .MinimumLevel.ControlledBy(s_levelSwitch)
                .ReadFrom.Services(serviceProvider)
                .Enrich.FromLogContext()
                .WriteTo.Spectre();
        });

        services.AddFileTetherCore();
        services.AddSingleton(AnsiConsole.Console);
        services.AddSingleton<ConsoleRenderer>();
    }
}
