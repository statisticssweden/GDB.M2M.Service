using System;
using System.IO;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace GDB.M2M.Service.HttpClients
{
    public interface IM2MHttpClient
    {
        /// <summary>
        /// Posts a file.
        /// </summary>
        /// <param name="requestInfo"></param>
        /// <param name="stream">FileStream</param>
        /// <returns></returns>
        Task<bool> PostFileAsync(RequestInfoEventArgs requestInfo, Stream stream);

        /// <summary>
        /// Makes a request to the Heartbeat-endpoint
        /// </summary>
        Task PerformHeartBeatAsync();
    }
}
