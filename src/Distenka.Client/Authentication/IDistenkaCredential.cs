using System.Threading;
using System.Threading.Tasks;

namespace Distenka.Client.Authentication
{
	/// <summary>
	/// Supplies the bearer token the client sends as <c>Authorization: Bearer &lt;token&gt;</c> (ADR-006). The platform
	/// recognises the credential kind from the token itself; no other authentication header is sent.
	/// </summary>
	/// <remarks>
	/// The SDK never acquires, caches or refreshes tokens: a rejected credential surfaces as a 401 from the platform.
	/// Implementations must not log or otherwise expose the token.
	/// </remarks>
	public interface IDistenkaCredential
	{
		/// <summary>Returns the bearer token for the next request.</summary>
		/// <param name="cancellationToken">Cancels the operation.</param>
		ValueTask<string> GetBearerTokenAsync(CancellationToken cancellationToken);
	}
}
