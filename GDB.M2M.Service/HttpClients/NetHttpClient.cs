using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using GDB.M2M.Service.Configurations;
using GDB.M2M.Service.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GDB.M2M.Service.HttpClients
{
    public class NetHttpClient : IM2MHttpClient
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly ILogger<NetHttpClient> _logger;
        private readonly M2MConfiguration _config;

        public NetHttpClient(IOptions<M2MConfiguration> config, IHttpClientFactory clientFactory, ILogger<NetHttpClient> logger)
        {
            _clientFactory = clientFactory;
            _logger = logger;
            _config = config.Value;
        }

        public async Task PerformHeartBeatAsync()
        {
            using (var client = _clientFactory.CreateClient(nameof(NetHttpClient)))
            {
                var result = await client.GetAsync(_config.PingResource);
                _logger.LogInformation($"Heartbeat was successful: {result.IsSuccessStatusCode}.");
            }
        }

        private Uri GetResourceUri(RequestInfoEventArgs requestInfo)
        {
            var resource = _config.FileUploadResource
                .Replace("{organisationNumber}", requestInfo.OrganizationNumber)
                .Replace("{statisticalProgram}", requestInfo.StatisticalProgram)
                .Replace("{referencePeriod}", requestInfo.ReferencePeriod)
                .Replace("{fileFormat}", requestInfo.FileFormat)
                .Replace("{fileName}", requestInfo.FileName)
                .Replace("{version}", requestInfo.Version ?? string.Empty);
            return new Uri(resource, UriKind.Relative);
        }

        public async Task<bool> PostFileAsync(RequestInfoEventArgs requestInfo, Stream stream)
        {
            // We will create a new client every time to make sure that do not re-use any session cookies.
            using (var client = _clientFactory.CreateClient(nameof(NetHttpClient)))
            {
                client.Timeout = new TimeSpan(0, 30, 0);
                var pingResponse = await client.GetAsync(_config.PingResource);
                if (!pingResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Heartbeat was not successful. Will abort.");
                    return false;
                }

                // Result from heartbeat isn't used but shown for readability. It should be a DateTime.
                var pingContent = await pingResponse.Content.ReadAsStringAsync();

                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, (int)stream.Length);

                ByteArrayContent bytes = new ByteArrayContent(data);      
                var resource = GetResourceUri(requestInfo);
                _logger.LogDebug($"Will POST file to {client.BaseAddress}/{resource}.");

                var multiContent = new MultipartFormDataContent();                
                multiContent.Add(bytes, "File", requestInfo.FileName);

               

                var response = await client.PostAsync(resource, multiContent);
                var parsedResponse = await JsonSerializer.DeserializeAsync<FileUploadResponse>(await response.Content.ReadAsStreamAsync(), new JsonSerializerOptions()
                {
                    PropertyNameCaseInsensitive = true
                });

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"File successfully posted. Thank you. The id for your deliveryId is: {parsedResponse.DeliveryId}.");
                    return true;
                }

                _logger.LogError("POST failed.");
                return false;
            }
        }
    }
}
