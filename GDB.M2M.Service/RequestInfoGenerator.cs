using System;
using System.IO;
using GDB.M2M.Service.Configurations;
using Microsoft.Extensions.Options;

namespace GDB.M2M.Service
{
    public interface IRequestInfoGenerator
    {
        RequestInfoEventArgs GenerateRequestInfo(string fullPath);
    }

    public class RequestInfoGenerator : IRequestInfoGenerator
    {
        private readonly IFileInfoFactory _infoFactory;
        private M2MConfiguration _config;

        public RequestInfoGenerator(IFileInfoFactory infoFactory, IOptions<M2MConfiguration> requestConfiguration)
        {
            _infoFactory = infoFactory;
            _config = requestConfiguration.Value;
        }

        /// <summary>
        /// Creates a RequestInfo based on the filePath or IRequestConfiguration as fallback.
        /// </summary>
        /// <param name="fullPath"></param>
        /// <param name="requestConfiguration"></param>
        /// <returns></returns>
        public RequestInfoEventArgs GenerateRequestInfo(string fullPath)
        {
            // If we got 3 levels beneath the root we will be able to generate all the necessary info from the directory path.
            var actualPathCount = fullPath.Split(new[] { "\\" }, StringSplitOptions.RemoveEmptyEntries).Length;
            var rootCount = _config.ReadDirectory.Split(new[] { "\\" }, StringSplitOptions.RemoveEmptyEntries).Length;
            if (actualPathCount == rootCount + 5) // Since we got the file itself we will have 5 extra entries.
            {
                return GenerateWithoutVersion(fullPath);
            }

            if (actualPathCount == rootCount + 6) // 6 entries means that we got a version as well.
            {
                return GenerateWithVersion(fullPath);
            }

            // Generate the requestInfo from config instead.
            return new RequestInfoEventArgs
            {
                FileFormat = _config.FileFormat,
                StatisticalProgram = _config.StatisticalProgram,
                OrganizationNumber = _config.OrganisationNumber,
                FullPath = fullPath,
                FileName = Path.GetFileName(fullPath),
                ReferencePeriod = _config.ReferencePeriod
            };
        }

        private RequestInfoEventArgs GenerateWithVersion(string filePath)
        {
            IFileInfo fileInfo = _infoFactory.Create(filePath);
            IDirectoryInfo version = fileInfo.Directory();
            IDirectoryInfo fileFormat = version.Parent();
            IDirectoryInfo referencePeriod = fileFormat.Parent();
            IDirectoryInfo statisticalProgram = referencePeriod.Parent();
            IDirectoryInfo organizationNumber = statisticalProgram.Parent();
            return new RequestInfoEventArgs
            {
                FileFormat = fileFormat.Name,
                StatisticalProgram = statisticalProgram.Name,
                OrganizationNumber = organizationNumber.Name,
                Version = version.Name,
                FullPath = filePath,
                FileName = Path.GetFileName(filePath),
                ReferencePeriod = referencePeriod.Name
            };
        }

        private RequestInfoEventArgs GenerateWithoutVersion(string filePath)
        {
            IFileInfo fileInfo = _infoFactory.Create(filePath);
            IDirectoryInfo fileFormat = fileInfo.Directory();
            IDirectoryInfo referencePeriod = fileFormat.Parent();
            IDirectoryInfo statisticalProgram = referencePeriod.Parent();
            IDirectoryInfo organizationNumber = statisticalProgram.Parent();

            return new RequestInfoEventArgs
            {
                FileFormat = fileFormat.Name,
                StatisticalProgram = statisticalProgram.Name,
                OrganizationNumber = organizationNumber.Name,
                FullPath = filePath,
                FileName = Path.GetFileName(filePath),
                ReferencePeriod = referencePeriod.Name
            };
        }
    }
}
