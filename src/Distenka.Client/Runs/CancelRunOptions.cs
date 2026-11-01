namespace Distenka.Client.Runs
{
	/// <summary>Options for <see cref="IRunsClient.CancelAsync"/>.</summary>
	public sealed class CancelRunOptions
	{
		/// <summary>
		/// When <see langword="true"/>, a run that had already ended (<c>409 run_already_ended</c>) throws a
		/// <see cref="DistenkaApiException"/> instead of returning a <see cref="CancelResult"/> with
		/// <see cref="CancelResult.AlreadyEnded"/> set. Default <see langword="false"/>: for most callers a run that ended on its
		/// own is an expected outcome of cancelling, not an error.
		/// </summary>
		public bool ThrowOnAlreadyEnded { get; set; }
	}
}
