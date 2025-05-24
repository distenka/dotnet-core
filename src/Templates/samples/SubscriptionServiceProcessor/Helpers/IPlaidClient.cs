namespace SubscriptionServiceProcessor
{
	public interface IPlaidClient
	{
		Task<ValidationResult> ValidateAccountAsync(string accountNumber, string routingNumber);
	}

	public class ValidationResult
	{
		public bool IsValid { get; set; }
		public string Message { get; set; }
	}
}
