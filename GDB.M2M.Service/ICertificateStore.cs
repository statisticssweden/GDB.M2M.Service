using System.Security.Cryptography.X509Certificates;
using GDB.M2M.Service.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GDB.M2M.Service
{
    public interface ICertificateStore
    {
        /// <summary>
        /// Retrieve the certificate.
        /// Uses config and SerialNumber to find the correct certificate in the store.
        /// </summary>
        /// <returns></returns>
        X509Certificate GetCertificate();
    }

    public class CertificateStore : ICertificateStore
    {
        private M2MConfiguration _config;
        private ILogger<CertificateStore> _logger;

        public CertificateStore(IOptions<M2MConfiguration> config, ILogger<CertificateStore> logger)
        {
            _config = config.Value;
            _logger = logger;
        }

        /// <summary>
        /// Retrieve the certificate.
        /// Uses config and SerialNumber to find the correct certificate in the store.
        /// </summary>
        /// <returns></returns>
        public X509Certificate GetCertificate()
        {
            X509Store store = null;
            try
            {
                // Note that it's currently fetching the certificate in My-store as CurrentUser.
                // This requires the developer to install the certificate as CurrentUser and run the application as CurrentUser.
                store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);

                // Currently X509FindType.FindBySerialNumber. Change to thumbprint or other if you find it better.
                X509Certificate2Collection cers = store.Certificates.Find(X509FindType.FindBySerialNumber, _config.CertificateSerialNumber, false);
                if (cers.Count > 0)
                {
                    _logger.LogDebug($"Successfully found certificate with subject: {cers[0].Subject}.");
                    return cers[0];
                }
            }
            finally
            {
                store?.Close();
            }
            _logger.LogWarning($"Could not find certificate with serial number {_config.CertificateSerialNumber}.");
            return null;
        }
    }
}
