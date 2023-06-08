using System;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using GDB.M2M.Service.Configurations;
using GDB.M2M.Service.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestSharp;

namespace GDB.M2M.Service.HttpClients
{
    /// <summary>
    /// A HttpClient using RestSharp. Replaces RestSharpFileUploader.
    /// </summary>
    public class RestSharpFileUploaderV2 : IM2MHttpClient
    {
        private const int DELIVERFILE = -1;
        private const long MAXCHUNKSIZE = 1024 * 400;

        private readonly ICertificateStore _certificateStore;
        private readonly M2MConfiguration _config;
        private readonly ILogger<RestSharpFileUploaderV2> _logger;

        public RestSharpFileUploaderV2(IOptions<M2MConfiguration> config, ICertificateStore certificateStore, ILogger<RestSharpFileUploaderV2> logger)
        {
            _certificateStore = certificateStore;
            _config = config.Value;
            _logger = logger;
        }

        public async Task<bool> PostFileAsync(RequestInfoEventArgs requestInfo, Stream stream)
        {
            IRestClient client = CreateRestClient();
            client.CookieContainer = new CookieContainer();

            var heartBeat = new RestRequest(_config.PingResource, Method.GET);
            var heartBeatUrl = client.BuildUri(heartBeat); // For debugging purposes.
            _logger.LogDebug($"Will perform a GET to Heartbeat-endpoint using {heartBeatUrl}.");
            var heartBeatResponse = await client.ExecuteGetAsync(heartBeat);
            if (!heartBeatResponse.IsSuccessful)
            {
                // Possible error handling here.
                // If you are unable to ping the heartbeat endpoint there's probably
                // something wrong with the certificate.
                _logger.LogWarning("Get to Heartbeat-endpoint was not successful. Will abort.");
                return false;
            }

            _logger.LogDebug("Get to Heartbeat-endpoint was successful.");

            var cookies = client.CookieContainer.GetCookies(new Uri(_config.BaseUrl));

            var chunkSegment = 0;
            var totalChunks = (int)Math.Ceiling(stream.Length / (double)MAXCHUNKSIZE);
            _logger.LogInformation($"Total number of chunks to send: {totalChunks}");

            while (chunkSegment < totalChunks)
            {
                var chunkSize = stream.Length - stream.Position;
                if (chunkSize > MAXCHUNKSIZE) { chunkSize = MAXCHUNKSIZE; }
                _logger.LogDebug($"Chunk: {chunkSegment}   Chunksize: {chunkSize}");

                IRestRequest request = CreateFileUploadRequest(requestInfo, stream, chunkSegment, chunkSize );

                foreach (Cookie restResponseCookie in cookies)
                {
                    request.AddCookie(restResponseCookie.Name, restResponseCookie.Value);
                }

                var url = client.BuildUri(request);
                _logger.LogInformation($"Posting file to: {url}");

                var response = await client.ExecutePostAsync<FileUploadResponse>(request);

                if (response.IsSuccessful)
                {
                    _logger.LogInformation($"Post of chunk {chunkSegment} success.");
                }
                else
                {
                    _logger.LogInformation($"Post chunk failed. Response: {response.ErrorMessage}");
                    return false;
                }
                chunkSegment++;
            }
            // Final post to make delivery of the chunk uploaded file
            IRestRequest deliveryRequest = CreateFileUploadRequest(requestInfo, stream, DELIVERFILE, 0L);
            foreach (Cookie restResponseCookie in cookies)
            {
                deliveryRequest.AddCookie(restResponseCookie.Name, restResponseCookie.Value);
            }

            var deliveryUrl = client.BuildUri(deliveryRequest);
            _logger.LogInformation($"Posting file to: {deliveryUrl}");
            var deliveryResponse = await client.ExecutePostAsync<FileUploadResponse>(deliveryRequest);

            if (deliveryResponse.IsSuccessful)
            {
                _logger.LogInformation($"Post delivery response data: {deliveryResponse.Data} ");
            }
            else
            {
                _logger.LogInformation($"Post chunk failed. Response: {deliveryResponse.ErrorMessage}");
                return false;
            }

            return true; // all is well that ends well.
        }

        /// <summary>
        /// Makes a request to the Heartbeat-endpoint
        /// </summary>
        public async Task PerformHeartBeatAsync()
        {
            try
            {
                IRestClient client = CreateRestClient();
                var request = new RestRequest
                {
                    Method = Method.GET,
                    Resource = _config.PingResource
                };

                var url = client.BuildUri(request);
                _logger.LogInformation($"Making request to url: {url}");

                IRestResponse result = await client.ExecuteGetAsync(request);
                if (result.StatusCode == HttpStatusCode.OK)
                {
                    _logger.LogInformation("Successful heartbeat from server.");
                }
                else
                {
                    _logger.LogWarning($"Failed to get a heartbeat from the server. StatusCode: {result.StatusCode}. Message: {result.ErrorMessage}");
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error when checking heartbeat.");
            }
        }

        /// <summary>
        /// Creates a IRestRequest based on a RequestInfoEventArgs
        /// </summary>
        /// <param name="requestInfo"></param>
        /// <param name="stream"></param>
        /// <param name="segment"></param>
        /// <param name="chunkSize"></param>
        /// <returns></returns>
        private IRestRequest CreateFileUploadRequest(RequestInfoEventArgs requestInfo, Stream stream, int segment, long chunkSize)
        {
            var chunk = segment.ToString();
            _logger.LogDebug($"RestSharpFileUploaderV2 Chunk: {chunk}");
            var request = new RestRequest
            {
                Resource = _config.FileUploadResource_v2_Chunk,
                Method = Method.POST
            };
            request.AddUrlSegment("segment", chunk);
            request.AddUrlSegment("organisationNumber", requestInfo.OrganizationNumber);
            request.AddUrlSegment("statisticalProgram", requestInfo.StatisticalProgram);
            request.AddUrlSegment("referencePeriod", requestInfo.ReferencePeriod);
            request.AddUrlSegment("fileFormat", requestInfo.FileFormat);
            request.AddUrlSegment("fileName", requestInfo.FileName);
            request.AddUrlSegment("version", requestInfo.Version ?? string.Empty);

            byte[] data = new byte[chunkSize];
            stream.Read(data, 0, (int)chunkSize);

            request.AddFileBytes("file", data, requestInfo.FileName, "application/xml");
            return request;
        }

        /// <summary>
        /// Creates a IRestClient with a ClientCertificate.
        /// </summary>
        /// <returns></returns>
        private IRestClient CreateRestClient()
        {
            var certificate = _certificateStore.GetCertificate();
            var client = new RestClient(_config.BaseUrl)
            {
                // We need to add the clientCertificate in order to authenticate.
                ClientCertificates = new X509CertificateCollection { certificate }
            };
            return client;
        }

    }
}
