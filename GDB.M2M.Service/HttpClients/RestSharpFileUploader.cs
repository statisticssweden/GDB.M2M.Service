using System;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using GDB.M2M.Service.Configurations;
using GDB.M2M.Service.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestSharp;

namespace GDB.M2M.Service.HttpClients
{
    /// <summary>
    /// A HttpClient using RestSharp.
    /// </summary>
    public class RestSharpFileUploader : IM2MHttpClient
    {
        private readonly ICertificateStore _certificateStore;
        private readonly M2MConfiguration _config;
        private readonly ILogger<RestSharpFileUploader> _logger;

        public RestSharpFileUploader(IOptions<M2MConfiguration> config, ICertificateStore certificateStore, ILogger<RestSharpFileUploader> logger)
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

            IRestRequest request = CreateFileUploadRequest(requestInfo, stream);

            foreach (Cookie restResponseCookie in cookies)
            {
                request.AddCookie(restResponseCookie.Name, restResponseCookie.Value);
            }

            var url = client.BuildUri(request);
            _logger.LogInformation($"Posting file to: {url}");
            var response = await client.ExecutePostAsync<FileUploadResponse>(request);

            if (response.StatusCode == HttpStatusCode.OK && response.Data != null)
            {
                _logger.LogInformation($"File successfully posted. Thank you. The id for your deliveryId is: {response.Data.DeliveryId}.");

                // You might want to return the deliveryId for further processing.
                // For the moment we simply return true to indicate success.
                return true;
            }

            _logger.LogInformation($"Post failed. Response: {response.ErrorMessage}");
            return false;

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
            catch(Exception e)
            {
                _logger.LogError(e, "Error when checking heartbeat.");
            }
        }


        /// <summary>
        /// Creates a IRestRequest based on a RequestInfoEventArgs
        /// </summary>
        /// <param name="requestInfo"></param>
        /// <param name="stream"></param>
        /// <returns></returns>
        private IRestRequest CreateFileUploadRequest(RequestInfoEventArgs requestInfo, Stream stream)
        {
            var request = new RestRequest
            {
                Resource = _config.FileUploadResource,
                Method = Method.POST
            };
            request.AddUrlSegment("statisticalProgram", requestInfo.StatisticalProgram);
            request.AddUrlSegment("fileFormat", requestInfo.FileFormat);
            request.AddUrlSegment("organisationNumber", requestInfo.OrganizationNumber);
            request.AddUrlSegment("version", requestInfo.Version ?? string.Empty);

            byte[] data = new byte[stream.Length];
            stream.Read(data, 0, (int)stream.Length);

            request.AddFile("file", data, requestInfo.FileName, "attachment");
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
