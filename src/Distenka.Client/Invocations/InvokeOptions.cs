using System;

namespace Distenka.Client.Invocations
{
	/// <summary>Options for <see cref="IInvocationsClient.InvokeAsync"/>.</summary>
	public sealed class InvokeOptions
	{
		/// <summary>
		/// Sent as the <c>Idempotency-Key</c> header: a retry after a timeout returns the first run instead of starting (and
		/// billing the developer for) a second, and it lets the client retry the invocation safely. The key namespace is the
		/// <em>developer's account</em>: two consumers of the same processor sending the same key with the same input get
		/// one run. Remembered for 24 hours. 1–255 printable ASCII characters, no spaces.
		/// </summary>
		public string? IdempotencyKey { get; set; }

		/// <summary>
		/// Sent as the <c>Callback-Url</c> header: the platform POSTs the outcome there when the run finishes (verify it with
		/// the webhook helper, DCS-063). Optional — the result can always be read with the returned capability. Must be an
		/// absolute <c>https</c> URL on a public address; the platform refuses private, link-local and metadata addresses.
		/// </summary>
		public Uri? CallbackUrl { get; set; }
	}
}
