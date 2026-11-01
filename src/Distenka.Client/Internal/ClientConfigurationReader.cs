using Distenka.Client.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Distenka.Client.Internal
{
	/// <summary>
	/// Reads <see cref="DistenkaClientOptions"/> from a configuration section without the reflection-based binder:
	/// <c>BaseAddress</c> (absolute URL), <c>Timeout</c> (<c>hh:mm:ss</c>), <c>AllowInsecureHttp</c> (<c>true</c>/<c>false</c>),
	/// <c>UserAgent</c>, <c>MaxRetries</c> (integer), <c>MaxRetryDelay</c> (<c>hh:mm:ss</c>), and the account key <c>ApiKey</c>
	/// (<c>DK-…</c>, DCS-027) as an <see cref="AccountKeyCredential"/>. Absent or empty keys leave the current value. Unparsable
	/// values fail with an <see cref="OptionsValidationException"/> naming the key — never quoting an <c>ApiKey</c> value.
	/// </summary>
	static class ClientConfigurationReader
	{
		internal static void Apply(IConfiguration configuration, DistenkaClientOptions options)
		{
			var prefix = configuration is IConfigurationSection section && section.Path.Length > 0 ? section.Path + ":" : "";
			var failures = new List<string>();

			if (Value(configuration, "BaseAddress") is string baseAddress)
			{
				if (Uri.TryCreate(baseAddress, UriKind.Absolute, out var uri))
					options.BaseAddress = uri;
				else
					failures.Add($"{prefix}BaseAddress '{baseAddress}' is not an absolute URL.");
			}

			if (Value(configuration, "Timeout") is string timeout)
			{
				if (TimeSpan.TryParse(timeout, CultureInfo.InvariantCulture, out var span))
					options.Timeout = span;
				else
					failures.Add($"{prefix}Timeout '{timeout}' is not a time span (expected hh:mm:ss, e.g. 00:01:40).");
			}

			if (Value(configuration, "AllowInsecureHttp") is string allowInsecureHttp)
			{
				if (bool.TryParse(allowInsecureHttp, out var allow))
					options.AllowInsecureHttp = allow;
				else
					failures.Add($"{prefix}AllowInsecureHttp '{allowInsecureHttp}' is not true or false.");
			}

			if (Value(configuration, "MaxRetries") is string maxRetries)
			{
				if (int.TryParse(maxRetries, NumberStyles.Integer, CultureInfo.InvariantCulture, out var retries))
					options.MaxRetries = retries;
				else
					failures.Add($"{prefix}MaxRetries '{maxRetries}' is not an integer.");
			}

			if (Value(configuration, "MaxRetryDelay") is string maxRetryDelay)
			{
				if (TimeSpan.TryParse(maxRetryDelay, CultureInfo.InvariantCulture, out var delay))
					options.MaxRetryDelay = delay;
				else
					failures.Add($"{prefix}MaxRetryDelay '{maxRetryDelay}' is not a time span (expected hh:mm:ss, e.g. 00:01:00).");
			}

			if (Value(configuration, "UserAgent") is string userAgent)
				options.UserAgent = userAgent;

			// The key comes from whichever provider the host configured (environment variable Distenka__ApiKey, user secrets,
			// a vault provider); the SDK never needs it as a literal (AD-9). The message never contains the value.
			if (Value(configuration, "ApiKey") is string apiKey)
			{
				try
				{
					options.Credential = new AccountKeyCredential(apiKey);
				}
				catch (ArgumentException e)
				{
					failures.Add($"{prefix}ApiKey is not an account key. {FirstSentence(e.Message)}");
				}
			}

			if (failures.Count > 0)
				throw new OptionsValidationException(Options.DefaultName, typeof(DistenkaClientOptions), failures);
		}

		/// <summary>The credential's message without the parameter suffix .NET appends.</summary>
		static string FirstSentence(string message)
		{
			var end = message.IndexOf(" (Parameter", StringComparison.Ordinal);
			return end > 0 ? message.Substring(0, end) : message;
		}

		static string? Value(IConfiguration configuration, string key) =>
			configuration[key] is string value && value.Trim().Length > 0 ? value.Trim() : null;
	}
}
