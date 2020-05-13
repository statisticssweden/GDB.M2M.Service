using System;
using System.IO;
using GDB.M2M.Service.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GDB.M2M.Service
{
    public interface IFileWatcher
    {
        event EventHandler<RequestInfoEventArgs> FileReadyForPost;
        bool EnableRaisingEvents { get; set; }
    }

    
    /// <summary>
    /// Listens for when files are added to a specific directory.
    /// </summary>
    public class FileWatcher : IFileWatcher
    {
        private readonly IFileReadyChecker _fileReadyChecker;
        private readonly ILogger<FileWatcher> _logger;
        private readonly IRequestInfoGenerator _requestInfoGenerator;
        private readonly M2MConfiguration _config;
        private readonly FileSystemWatcher _fileSystemWatcher;

        public FileWatcher(IOptions<M2MConfiguration> config, IFileReadyChecker fileReadyChecker, ILogger<FileWatcher> logger, IRequestInfoGenerator requestInfoGenerator)
        {
            _fileReadyChecker = fileReadyChecker;
            _logger = logger;
            _requestInfoGenerator = requestInfoGenerator;
            _config = config.Value;

            // Listen to files being added to the selected directory.
            _fileSystemWatcher = new FileSystemWatcher(_config.ReadDirectory)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            _fileSystemWatcher.Created += OnFileCreated;
            logger.LogInformation($"Listening to file changes under: {_config.ReadDirectory}");
        }

        /// <summary>
        /// Handler for when a file is added to the folder.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType != WatcherChangeTypes.Created) { return; }

            if (!_fileReadyChecker.IsReady(e.FullPath))
            {
                _logger.LogError($"File was not available: { e.FullPath}");
                Console.WriteLine("Error! Could not read file.");
                return;
            }

            _logger.LogInformation($"Filechange registered: {e.FullPath}");
            var requestInfo = _requestInfoGenerator.GenerateRequestInfo(e.FullPath);
            OnFileReadyForPost(requestInfo);
        }

        public event EventHandler<RequestInfoEventArgs> FileReadyForPost;

        public bool EnableRaisingEvents
        {
            get => _fileSystemWatcher.EnableRaisingEvents;
            set => _fileSystemWatcher.EnableRaisingEvents = value;
        }

        protected virtual void OnFileReadyForPost(RequestInfoEventArgs e)
        {
            var handler = FileReadyForPost;
            if (handler != null)
                handler(this, e);
        }
    }
}
