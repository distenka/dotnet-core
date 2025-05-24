using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace Distenka.Client;

public interface IDistenkaClient
{
    Task UpdateRunStatusAsync(string runId, InstanceState instanceState, int progress);
}
public class DistenkaClient : IDistenkaClient
{
        private readonly HttpClient _httpClient;
        private readonly IAuthenticationProvider _tokenProvider;
        private readonly Config _config;
        private HubConnection _hubConnection;

        public DistenkaClient(HttpClient httpClient, IAuthenticationProvider tokenProvider, Config config)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            InitializeConnection();
        }

        private void InitializeConnection()
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(BuildUrl("/hub"), options =>
                {
                    options.Headers["Authorization"] = $"Bearer {_tokenProvider.AcquireToken().Result}";
                    options.Headers["x-api-key"] = _config.Execution.APIKey;
                })
                .Build();

            _hubConnection.StartAsync().Wait();
        }
        
        public async Task UpdateRunStatusAsync(string runId, InstanceState instanceState, int progress)
        {
            if (_hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("UpdateRunStatus", runId, instanceState, progress);
            }
            else
            {
                throw new InvalidOperationException("SignalR connection is not active.");
            }
        }
        
        private string BuildUrl(string path)
        {
            var endpoint = string.Format(Constants.ApiEndpointTemplate, _config.Execution.OrgName, _config.Execution.Env);
            return $"{endpoint}{path}";
        }
    }