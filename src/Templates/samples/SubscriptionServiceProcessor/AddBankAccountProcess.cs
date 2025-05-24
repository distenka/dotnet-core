using Distenka;

using SubscriptionServiceProcessor.Models;

namespace SubscriptionServiceProcessor
{
	public class AddBankAccountProcess: Processor<AddBankAccountConfig>
	{
		private readonly SubscriptionServiceContext _dbContext;
		private readonly IPlaidClient _plaidClient;
		private readonly IEncryptionService _encryptionService;

		public AddBankAccountProcess(AddBankAccountConfig config, SubscriptionServiceContext dbContext, IPlaidClient plaidClient, IEncryptionService encryptionService) : base(config)
		{
			_dbContext = dbContext;
			_plaidClient = plaidClient;
			_encryptionService = encryptionService;
		}
		public override async Task<Result> ProcessAsync()
		{
			// Validate bank account with Plaid
			var validationResult = await _plaidClient.ValidateAccountAsync(Config.AccountNumber, Config.RoutingNumber);
			if (!validationResult.IsValid) return Result.Failure("Invalid bank account");

			// Encrypt details
			var encryptedAccount = _encryptionService.Encrypt(Config.AccountNumber);
			var encryptedRouting = _encryptionService.Encrypt(Config.RoutingNumber);

			// Store in database
			var bankAccount = new Models.BankAccount
			{
				UserId = Config.UserId,
				EncryptedAccountNumber = encryptedAccount,
				EncryptedRoutingNumber = encryptedRouting,
				ValidationStatus = "Validated",
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			};
			_dbContext.BankAccounts.Add(bankAccount);
			await _dbContext.SaveChangesAsync();

			return Result.Success(bankAccount);
		}
	}
	public class AddBankAccountConfig: Config
	{
		public int UserId { get; set; }
		public string AccountNumber { get; set; }
		public string RoutingNumber { get; set; }
	}
}
