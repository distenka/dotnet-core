using System;
using System.Threading.Tasks;

namespace Distenka.Client;

public class ApiKeyProvider : IAuthenticationProvider
{
    readonly string apiKey;

    public ApiKeyProvider(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentNullException(nameof(apiKey));

        this.apiKey = apiKey;
    }

    public Task<string> AcquireToken()
    {
        return Task.FromResult(apiKey);
    }
}