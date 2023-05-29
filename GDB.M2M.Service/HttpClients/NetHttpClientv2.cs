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
    public class NetHttpClientv2 : IM2MHttpClient
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly ILogger<NetHttpClientv2> _logger;
        private readonly M2MConfiguration _config;

        public NetHttpClientv2(IOptions<M2MConfiguration> config, IHttpClientFactory clientFactory, ILogger<NetHttpClientv2> logger)
        {
            _clientFactory = clientFactory;
            _logger = logger;
            _config = config.Value;
        }

        public async Task PerformHeartBeatAsync()
        {
            using (var client = _clientFactory.CreateClient(nameof(NetHttpClientv2)))
            {
                var result = await client.GetAsync(_config.PingResource);
                _logger.LogInformation($"Heartbeat success status code: {result.IsSuccessStatusCode}.");
            }
        }

        public async Task<bool> PostFileAsync(RequestInfoEventArgs requestInfo, Stream sourceStream)
        {
            // We will create a new client every time to make sure that do not re-use any session cookies.
            using (var client = _clientFactory.CreateClient(nameof(NetHttpClientv2)))
            {
                var pingResource = _config.PingResource;
                _logger.LogInformation($"pinging resource: {pingResource}");
                var pingResponse = await client.GetAsync(pingResource);
                if (!pingResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Heartbeat was not successful. Will abort.");
                    return false;
                }
                var pingContent = await pingResponse.Content.ReadAsStringAsync();
                _logger.LogDebug($"{pingContent}");

                FileInfo fi = new FileInfo(requestInfo.FullPath);

                // Set boundary manually.
                var boundary = Guid.NewGuid().ToString();
                var finalResult = false;
                var chunkResult = await UploadFileInChunks(client, requestInfo, fi, boundary, sourceStream);
                _logger.LogInformation($"Uploaded File In Chunks result: {chunkResult}");

                if (chunkResult)
                {
                    _logger.LogInformation("Will attempt to finalize delivery of file transfer");
                    finalResult = await FinalizeUpload(client, requestInfo, fi, boundary);
                    _logger.LogInformation($"Finalize: File delivery result: {finalResult}");
                }

                if (!finalResult)
                {
                    _logger.LogError("Transfer of file failed");
                }

                return finalResult;
            }
        }

        private async Task<bool> UploadFileInChunks(HttpClient client, RequestInfoEventArgs requestInfo, FileInfo file, string boundary, Stream inStream)
        {
            const long CHUNKSIZE = 1024 * 400;

            long uploadedBytes = 0;
            long totalBytes = inStream.Length;
            long percent = 0;
            int fragment = 0;
            long chunkSize;

            _logger.LogDebug("=====   Begin upload file chunks   =====");
            var uploading = true;
            while (uploading)
            {
                chunkSize = CHUNKSIZE;
                if (uploadedBytes + CHUNKSIZE > totalBytes)
                {
                    chunkSize = totalBytes - uploadedBytes;
                }
                var chunk = new byte[chunkSize];
                var readResult = await inStream.ReadAsync(chunk, 0, chunk.Length);
                _logger.LogDebug($"Read {readResult} bytes from file {file.FullName}");

                // TODO: .Net6 need to go to C#8 here it is currently 7 // using var formFile = new MultipartFormDataContent(boundary);
                var formFile = new MultipartFormDataContent(boundary);
                var fileContent = new StreamContent(new MemoryStream(chunk));
                formFile.Add(fileContent, "file", file.Name);

                // HttpClient adds double qoutes (") to the boundary, which is not supported by the server. Therefore we remove them.
                formFile.Headers.Remove("Content-Type");
                formFile.Headers.TryAddWithoutValidation("Content-Type", "multipart/mixed; boundary=" + boundary);

                _logger.LogDebug($"Base Address from config: {client.BaseAddress}");
                Uri uri = GetResourceUriFileChunk(requestInfo, fragment);
                _logger.LogInformation($"Posting file chunk {fragment} to {uri}");

                fragment++;

                var response = await client.PostAsync(uri, formFile);//  (resource, multiContent);

                var parsedResponse = await JsonSerializer.DeserializeAsync<FileUploadResponse>(await response.Content.ReadAsStreamAsync(), new JsonSerializerOptions()
                {
                    PropertyNameCaseInsensitive = true
                });
                _logger.LogDebug($"Parsed response is: {parsedResponse}");

                _logger.LogDebug($"Response Status Code: {response.StatusCode}");
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Posted file chunk successfully.");
                }
                uploadedBytes += chunkSize;
                percent = uploadedBytes * 100 / totalBytes;
                _logger.LogInformation($"Uploaded bytes : {uploadedBytes}   percent: {percent} ");

                if (percent >= 100L)
                {
                    uploading = false;
                }
            }
            _logger.LogDebug("=====   End upload file chunks   =====");

            return true;
        }

        private async Task<bool> FinalizeUpload(HttpClient client, RequestInfoEventArgs requestInfo, FileInfo file, string boundary)
        {
            //var chunk = new byte[0];
            //MultipartFormDataContent multiContent = new MultipartFormDataContent(boundary);

            // Use application/x-www-form-urlencoded instead - skip the empty chunk

            //var fileContent = new StreamContent(new MemoryStream(chunk)); //
            //multiContent.Add(fileContent, "file", file.Name);

            // HttpClient adds double qoutes (") to the boundary, which is not supported by the server. Therefore we remove them.
            //multiContent.Headers.Remove("Content-Type");
            //multiContent.Headers.TryAddWithoutValidation("Content-Type", "multipart/mixed; boundary=" + boundary);

            var resource = GetResourceUriFinalize(requestInfo);
            _logger.LogInformation($"Posting finalize file transfer to: {resource}");

            //var response = await client.PostAsync(resource, multiContent);
            var response = await client.GetAsync(resource);

            _logger.LogInformation($"Finalize upload response status code {response.StatusCode}");
            _logger.LogInformation($"Response content: {response.Content}");

            // Do or not
            var parsedResponse = await JsonSerializer.DeserializeAsync<FileUploadResponse>(await response.Content.ReadAsStreamAsync(), new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            });
            _logger.LogDebug($"Parsed response: {parsedResponse}");

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"File successfully posted. The deliveryId is: {parsedResponse.DeliveryId}");
                return true;
            }
            _logger.LogError("Failed to finalize the file transfer.");
            return false;
        }

        private Uri GetResourceUriFinalize(RequestInfoEventArgs requestInfo)
        {
            //var baseUri = new Uri(baseAddress, UriKind.Absolute);
            var resource = _config.FileUploadResource_v2_Deliver
                .Replace("{organisationNumber}", requestInfo.OrganizationNumber)
                .Replace("{statisticalProgram}", requestInfo.StatisticalProgram)
                .Replace("{referencePeriod}", requestInfo.ReferencePeriod)
                .Replace("{fileFormat}", requestInfo.FileFormat)
                .Replace("{fileName}", requestInfo.FileName)
                .Replace("{version}", requestInfo.Version ?? string.Empty);
            _logger.LogDebug($"resource: {resource} ");
            //Uri relativeUri = new Uri(resource, UriKind.Relative);
            //Uri absoluteUri = new Uri(baseUri, relativeUri);
            //_logger.LogDebug($"Absolute Uri: {absoluteUri}");
            return new Uri(resource, UriKind.Relative);
        }

        private Uri GetResourceUriFileChunk(RequestInfoEventArgs requestInfo, int segment)
        {
            //var baseUri = new Uri(baseAddress, UriKind.Absolute);
            var resource = _config.FileUploadResource_v2_Chunk
                .Replace("{segment}", segment.ToString())
                .Replace("{organisationNumber}", requestInfo.OrganizationNumber)
                .Replace("{statisticalProgram}", requestInfo.StatisticalProgram)
                .Replace("{referencePeriod}", requestInfo.ReferencePeriod)
                .Replace("{fileFormat}", requestInfo.FileFormat)
                .Replace("{fileName}", requestInfo.FileName)
                .Replace("{version}", requestInfo.Version ?? string.Empty);
            _logger.LogDebug($"using resource: {resource} ");
            //Uri relativeUri = new Uri(resource, UriKind.Relative);
            //Uri absoluteUri = new Uri(baseUri, relativeUri);
            //_logger.LogDebug($"Absolute Uri: {absoluteUri}");
            return new Uri(resource, UriKind.Relative);
        }

    }
}
