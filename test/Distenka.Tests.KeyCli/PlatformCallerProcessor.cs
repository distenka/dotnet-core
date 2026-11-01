using Distenka.Client;
using Distenka.Client.Authentication;
using Microsoft.Extensions.Logging;

namespace Distenka.Tests.KeyCli;

/// <summary>
/// Configuration of <see cref="PlatformCallerProcessor"/>. On the platform, <see cref="DistenkaApiKey"/> is an owner binding
/// (ACC-37): the owner stores the key once, and the platform merges it into the run configuration server-side — it is never
/// part of the package or of what a caller sends.
/// </summary>
public class PlatformCallerConfig : Config
{
    /// <summary>The account key (<c>DK-…</c>) this processor uses to call the platform, e.g. to submit child runs.</summary>
    public string? DistenkaApiKey { get; set; }

    /// <summary>The platform's base address; no request is sent by the tests.</summary>
    public string BaseAddress { get; set; } = "https://distenka.invalid/";

    /// <summary>When set, the processor fails after building its client, to exercise the failure output.</summary>
    public bool Fail { get; set; }

    /// <summary>
    /// DCS-046: when set, the processor carelessly puts the key into everything the SDK copies onto stdout — the item
    /// category, the failure's exception message and the FinalizeAsync output — so the tests prove the SDK redacts it.
    /// </summary>
    public bool EchoKey { get; set; }
}

/// <summary>
/// DCS-027 AC 2: builds an <see cref="AccountKeyCredential"/> and an <see cref="IDistenkaClient"/> from its configuration, logs
/// what a developer typically logs about it, and succeeds (or fails on request). The tests assert that nothing the host
/// writes — stdout, the ERROR line, the results file — contains the key.
/// </summary>
public class PlatformCallerProcessor : Processor<PlatformCallerConfig>
{
    readonly ILogger<PlatformCallerProcessor> _logger;

    public PlatformCallerProcessor(PlatformCallerConfig config, ILogger<PlatformCallerProcessor> logger)
        : base(config)
    {
        _logger = logger;
    }

    public override async Task<Result> ProcessAsync()
    {
        var credential = new AccountKeyCredential(Config.DistenkaApiKey ?? throw new InvalidOperationException("distenkaApiKey is not bound."));
        using var http = new HttpClient();
        IDistenkaClient client = new DistenkaClient(http, new DistenkaClientOptions
        {
            BaseAddress = new Uri(Config.BaseAddress),
            Credential = credential,
        });

        // The credential's ToString shows only the public prefix (DCS-021), so logging it is safe.
        _logger.LogInformation("Platform client ready with {Credential}", credential);

        // A real processor would now call client.Runs.SubmitAsync(…); the tests send no request.
        var token = await credential.GetBearerTokenAsync(CancellationToken.None);
        if (token.Length == 0)
            return Result.Failure("no credential");
        _ = client.Runs;

        if (Config.Fail)
            throw new InvalidOperationException(Config.EchoKey ? $"Submitting the child run failed with {token}." : "Submitting the child run failed.");

        return Config.EchoKey ? Result.Success(token) : Result.Success();
    }

    public override Task<object?> FinalizeAsync(Disposition disposition) =>
        Task.FromResult<object?>(Config.EchoKey ? new { submittedWith = Config.DistenkaApiKey } : null);
}
