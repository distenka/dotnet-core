using Distenka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Stripe;
using SubscriptionServiceProcessor;
using SubscriptionServiceProcessor.Models;

public class Program
{
	public static async Task Main(string[] args)
	{
		await ProcessorHost.CreateDefaultBuilder(args)
			.ConfigureServices((hostContext, services) =>
			{
				// Register DbContext for SQL Server (optional, if database is needed)
				services.AddDbContextPool<SubscriptionServiceContext>(options =>
				options.UseSqlServer(hostContext.Configuration.GetConnectionString("DefaultConnection")));

				// Register IEncryptionService with key and IV from configuration
				services.AddSingleton<IEncryptionService>(provider =>
				{
					var config = provider.GetRequiredService<IConfiguration>();
					var key = config["Encryption:Key"];
					var iv = config["Encryption:IV"];
					return new EncryptionService(key, iv);
				});

				// Add other services as needed
				services.AddSingleton<IStripeClient, StripeClient>();
				services.AddSingleton<IPlaidClient, PlaidClient>();

				// Add Runly jobs
				services.AddScoped<CreateSubscriptionProcess>();
				services.AddScoped<AddBankAccountProcess>();
				services.AddScoped<ActivateSubscriptionProcess>();
			})
			.Build()
			.RunProcessorAsync();
	}
}