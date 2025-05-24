using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Distenka.Client;

public static class ServiceExtensions
{
    public static IServiceCollection AddDistenkaClient(this IServiceCollection services, Config config)
    {
        Console.WriteLine(config);
        var configError = IsValidConfiguration(config, out var errors);
        
        if (!configError)
            throw new ArgumentNullException(nameof(config), errors);

        services.AddSingleton<IAuthenticationProvider>(s => new ApiKeyProvider(config.Execution.APIKey));

        services.AddHttpClient(Constants.HttpClientName, (s, client) =>
        {
            client.BaseAddress = new Uri(string.Format(Constants.ApiEndpointTemplate, config.Execution.OrgName, config.Execution.Env));
        });

        services.AddTransient<IDistenkaClient>(s =>
        {
            var factory = s.GetRequiredService<IHttpClientFactory>();
            return new DistenkaClient(factory.CreateClient(Constants.HttpClientName), s.GetRequiredService<IAuthenticationProvider>(), config);
        });

        return services;
    }
    
    public static bool IsValidConfiguration(Config config, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (config.Execution == null)
        {
            errorMessage = "Execution configuration is missing.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.Execution.OrgName))
        {
            errorMessage = "Organization name is missing or invalid.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.Execution.APIKey))
        {
            errorMessage = "API key is missing or invalid.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.Execution.Env))
        {
            errorMessage = "Environment is missing or invalid.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.Execution.Processor))
        {
            errorMessage = "Processor is missing or invalid.";
            return false;
        }
        
        return true;
    }
}