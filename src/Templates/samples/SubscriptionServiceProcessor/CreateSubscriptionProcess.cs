using Distenka;
using Microsoft.Extensions.Logging;
using SubscriptionServiceProcessor.Models;

namespace SubscriptionServiceProcessor
{
	public class CreateSubscriptionProcess : Processor<CreateSubscriptionConfig>
	{
		private readonly SubscriptionServiceContext _dbContext;
		private readonly ILogger<CreateSubscriptionProcess> _logger;

		public CreateSubscriptionProcess(
			CreateSubscriptionConfig config,
			SubscriptionServiceContext dbContext,
			ILogger<CreateSubscriptionProcess> logger)
			: base(config)
		{
			_dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		public override async Task<Result> ProcessAsync()  // Changed signature
		{
			try
			{
				ValidateConfig();
				_logger.LogInformation("Starting subscription creation for user {UserId}", Config.UserId);

				var subscription = await CreateAndSaveSubscriptionAsync();

				_logger.LogInformation("Successfully created subscription {SubscriptionId}", subscription.SubscriptionId);

				return Result.Success(new CreateSubscriptionResult  // Return your result type
				{
					UserId = Config.UserId,
					SubscriptionId = subscription.SubscriptionId
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to create subscription for user {UserId}", Config.UserId);
				return Result.Failure($"Subscription creation failed: {ex.Message}");
			}
		}

		private void ValidateConfig()
		{
			if (Config.UserId <= 0)
				throw new ArgumentException("Invalid User ID");

			if (Config.PlanId <= 0)
				throw new ArgumentException("Invalid Plan ID");
		}

		private async Task<Models.Subscription> CreateAndSaveSubscriptionAsync()
		{
			var subscription = CreateSubscriptionEntity();

			_dbContext.Subscriptions.Add(subscription);
			await _dbContext.SaveChangesAsync();

			return subscription;
		}

		private Models.Subscription CreateSubscriptionEntity() => new()
		{
			UserId = Config.UserId,
			PlanId = Config.PlanId,
			Status = "Inactive",
			CreatedAt = DateTime.UtcNow,
			StartDate = DateTime.UtcNow,
			EndDate = DateTime.UtcNow.AddYears(1),
			UpdatedAt = DateTime.UtcNow
		};

	}

	public class CreateSubscriptionConfig : Config
	{
		public int UserId { get; set; }
		public int PlanId { get; set; }
	}

	public class CreateSubscriptionResult
	{
		public int UserId { get; set; }
		public int SubscriptionId { get; set; }
	}
}
