namespace Distenka.Client.Invocations
{
	/// <summary>
	/// One read of a run's result: either a new document (<c>200</c>) or confirmation that the document sent with
	/// <see cref="ETag"/> is still current (<c>304 Not Modified</c>, no body).
	/// </summary>
	public sealed class RunResultResponse
	{
		/// <summary>Creates the value (handy in tests).</summary>
		/// <param name="result">The document; <see langword="null"/> when <paramref name="notModified"/>.</param>
		/// <param name="etag">The validator to send on the next read.</param>
		/// <param name="notModified">The platform answered <c>304</c>.</param>
		public RunResultResponse(RunResult? result, string? etag, bool notModified)
		{
			Result = result;
			ETag = etag;
			NotModified = notModified;
		}

		/// <summary>The document from a <c>200</c>; <see langword="null"/> when <see cref="NotModified"/> — keep the one you have.</summary>
		public RunResult? Result { get; }

		/// <summary>The response's <c>ETag</c> (e.g. <c>W/"…"</c>). Pass it to the next read so an unchanged run costs no body.</summary>
		public string? ETag { get; }

		/// <summary><see langword="true"/> when the run has not changed since the <c>ETag</c> you sent (<c>304</c>).</summary>
		public bool NotModified { get; }
	}
}
