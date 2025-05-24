using Distenka;

using SubscriptionServiceProcessor.Models;


namespace SubscriptionServiceProcessor
{
	public class ActivateSubscriptionProcess: Processor<ActivateSubscriptionConfig>
	{
		private readonly SubscriptionServiceContext _dbContext;
		public ActivateSubscriptionProcess(ActivateSubscriptionConfig config, SubscriptionServiceContext dbContext) : base(config)
		{
			_dbContext = dbContext;
		}


		public override async Task<Result> ProcessAsync()
		{
			var subscription = await _dbContext.Subscriptions.FindAsync(Config.SubscriptionId);
			if (subscription == null) return Result.Failure("Subscription not found");

			var oldStatus = subscription.Status;
			subscription.Status = "Active";
			subscription.UpdatedAt = DateTime.UtcNow;

			// Log status change
			_dbContext.StatusChanges.Add(new StatusChange
			{
				SubscriptionId = Config.SubscriptionId,
				OldStatus = oldStatus,
				NewStatus = "Active",
				Timestamp = DateTime.UtcNow
			});

			await _dbContext.SaveChangesAsync();
			return Result.Success();
		}

	}
	public class ActivateSubscriptionConfig : Config
	{
		public int SubscriptionId { get; set; }
	}
}
