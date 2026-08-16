using FileTether.Core.Configuration;
using FileTether.Core.Configuration.Internal;
using FileTether.Core.Providers;
using FileTether.Core.Providers.Internal;
using FileTether.Core.State;
using FileTether.Core.Tethering;
using FileTether.Core.Tethering.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace FileTether.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFileTetherCore(this IServiceCollection services)
    {
        services.AddSingleton<EnvFileLoader>();
        services.AddSingleton<ConfigLoader>();
        services.AddSingleton<StateStore>();
        services.AddSingleton<TetherSession>();
        services.AddSingleton<ProviderRegistry>();
        services.AddHttpClient<GitHubApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.github.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("file-tether");
        });
        services.AddSingleton<IFileProvider, GitHubProvider>();
        services.AddSingleton<IFileProvider, GistProvider>();
        services.AddSingleton<LocalFingerprintCalculator>();
        services.AddSingleton<TetherAnalyzer>();
        services.AddSingleton<FileWriter>();
        services.AddSingleton<PullApplier>();

        return services;
    }
}
