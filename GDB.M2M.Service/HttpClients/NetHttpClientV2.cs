using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GDB.M2M.Service.Configurations;
using GDB.M2M.Service.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GDB.M2M.Service.HttpClients
{
    /// <summary>
    /// NetHttpClient replaces NetHttpClient
    /// </summary>
    public class NetHttpClientV2 : IM2MHttpClient
    {
        private const int DELIVERFILE = -1;
        private const long FINALRESPONSESIZE = 0;
        private const long MAXCHUNKSIZE = 1024 * 400;

        private readonly IHttpClientFactory _clientFactory;
        private readonly ILogger<NetHttpClientV2> _logger;
        private readonly M2MConfiguration _config;

        public NetHttpClientV2(IOptions<M2MConfiguration> config, IHttpClientFactory clientFactory, ILogger<NetHttpClientV2> logger)
        {
            _clientFactory = clientFactory;
            _logger = logger;
            _config = config.Value;
        }

        public async Task PerformHeartBeatAsync()
        {
            using (var client = _clientFactory.CreateClient(nameof(NetHttpClientV2)))
            {
                var result = await client.GetAsync(_config.PingResource);
                _logger.LogInformation($"Heartbeat was successful: {result.IsSuccessStatusCode}.");
            }
        }

        private Uri GetResourceUri(RequestInfoEventArgs requestInfo, int segment)
        {
            var resource = _config.FileUploadResourceV2
                .Replace("{segment}", segment.ToString())
                .Replace("{organisationNumber}", requestInfo.OrganizationNumber)
                .Replace("{statisticalProgram}", requestInfo.StatisticalProgram)
                .Replace("{referencePeriod}", requestInfo.ReferencePeriod)
                .Replace("{fileFormat}", requestInfo.FileFormat)
                .Replace("{fileName}", requestInfo.FileName)
                .Replace("{version}", requestInfo.Version ?? string.Empty);
            _logger.LogDebug($"using resource: {resource} ");
            return new Uri(resource, UriKind.Relative);
        }

        public async Task<bool> PostFileAsync(RequestInfoEventArgs requestInfo, Stream stream)
        {
            // We will create a new client every call to make sure that do not re-use any session cookies.
            using (var client = _clientFactory.CreateClient(nameof(NetHttpClientV2)))
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


                var totalChunks = (int)Math.Ceiling(stream.Length / (double)MAXCHUNKSIZE);
                _logger.LogInformation($"Total number of chunks to send: {totalChunks}");
                bool result = true;
                int chunkSegment = 0;
                while (chunkSegment < totalChunks)
                {
                    var chunkSize = stream.Length - stream.Position;
                    if (chunkSize > MAXCHUNKSIZE) { chunkSize = MAXCHUNKSIZE; }
                    _logger.LogDebug($"Chunk: {chunkSegment}   Chunksize: {chunkSize}");

                    var chunkResponse = await PostData(requestInfo, stream, client, chunkSize, chunkSegment);

                    if (chunkResponse.IsSuccessStatusCode)
                    {
                        _logger.LogInformation($"POST of Chunk {chunkSegment} success");
                    }
                    else
                    {
                        _logger.LogError($"POST of Chunk {chunkSegment} failed");
                        result = false;
                    }

                    chunkSegment++;
                }

                _logger.LogInformation($"Posted {chunkSegment} chunk segments.");

                var finalResponse = await PostData(requestInfo, stream, client, FINALRESPONSESIZE, DELIVERFILE);

                if (finalResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"POST of file transfer to delivery is success.");

                }
                else
                {
                    _logger.LogError($"POST of file transfer to delivery failed");
                    result = false;
                }

                return result;
            }
        }

        private async Task<HttpResponseMessage> PostData(RequestInfoEventArgs requestInfo, Stream stream, HttpClient client, long chunkSize, int segment)
        {
            byte[] noData = new byte[chunkSize];
            stream.Read(noData, 0, (int)chunkSize);

            ByteArrayContent noBytes = new ByteArrayContent(noData);

            var multiContent = new MultipartFormDataContent();
            multiContent.Add(noBytes, "File", requestInfo.FileName);

            // Post with negative chunk segment value to make the delivery happen
            var post = GetResourceUri(requestInfo, segment);
            _logger.LogInformation($"Will POST request to {client.BaseAddress}{post}");

            var response = await client.PostAsync(post, multiContent);

            return response;
        }
    }
}
