using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;

namespace Distenka.Client.Internal
{
	/// <summary>
	/// Validates <see cref="DistenkaClientOptions"/>. Registered by <c>AddDistenkaClient</c> with <c>ValidateOnStart</c>, so
	/// a bad configuration fails when the host starts, not on the first call. Messages never include the credential.
	/// </summary>
	sealed class DistenkaClientOptionsValidator : IValidateOptions<DistenkaClientOptions>
	{
		public ValidateOptionsResult Validate(string? name, DistenkaClientOptions options)
		{
			if (options == null)
				return ValidateOptionsResult.Fail("DistenkaClientOptions must not be null.");

			var failures = new List<string>();

			if (options.BaseAddress is not Uri address)
			{
				failures.Add("DistenkaClientOptions.BaseAddress is required: set it to the base URL of your Distenka deployment's API (for example https://api.example.com/). There is no default host.");
			}
			else if (!address.IsAbsoluteUri)
			{
				failures.Add($"DistenkaClientOptions.BaseAddress must be an absolute URL; '{address.OriginalString}' is relative.");
			}
			else
			{
				var https = string.Equals(address.Scheme, "https", StringComparison.OrdinalIgnoreCase);
				var http = string.Equals(address.Scheme, "http", StringComparison.OrdinalIgnoreCase);

				if (!https && !http)
					failures.Add($"DistenkaClientOptions.BaseAddress must use https; '{address.Scheme}' is not supported.");
				else if (http && !options.AllowInsecureHttp)
					failures.Add("DistenkaClientOptions.BaseAddress uses http, which would send credentials unencrypted. Use https, or set AllowInsecureHttp = true for a local development deployment.");

				if (address.UserInfo.Length > 0)
					failures.Add("DistenkaClientOptions.BaseAddress must not contain user information; supply credentials through DistenkaClientOptions.Credential.");

				if (address.Query.Length > 0 || address.Fragment.Length > 0)
					failures.Add("DistenkaClientOptions.BaseAddress must not contain a query or fragment.");
			}

			if (options.Timeout <= TimeSpan.Zero && options.Timeout != Timeout.InfiniteTimeSpan)
				failures.Add($"DistenkaClientOptions.Timeout must be positive or Timeout.InfiniteTimeSpan; it is {options.Timeout}.");

			if (options.UserAgent is string userAgent && userAgent.Trim().Length > 0)
			{
				using var probe = new HttpRequestMessage();
				if (!probe.Headers.UserAgent.TryParseAdd(userAgent.Trim()))
					failures.Add($"DistenkaClientOptions.UserAgent '{userAgent}' is not a valid User-Agent value (expected product tokens such as MyApp/1.2).");
			}

			return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
		}
	}
}
