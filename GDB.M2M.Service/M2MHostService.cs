using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using GDB.M2M.Service.Configurations;
using GDB.M2M.Service.HttpClients;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


namespace GDB.M2M.Service
{
    public class M2MHostService : IHostedService
    {
        private readonly M2MConfiguration _requestConfiguration;
        private readonly IFileWatcher _watcher;
        private readonly IM2MHttpClient _fileUploader;
        private readonly ILogger<M2MHostService> _logger;
        private readonly System.Timers.Timer _timer;

        public M2MHostService(IOptions<M2MConfiguration> requestConfiguration, IFileWatcher fileFileWatcher, IM2MHttpClient fileUploader, ILogger<M2MHostService> logger)
        {
            _requestConfiguration = requestConfiguration.Value;
            _watcher = fileFileWatcher;
            _fileUploader = fileUploader;
            _logger = logger;

            // We create a timer which will check for a heartbeat (ping) from server at a given interval.
            _timer = new System.Timers.Timer(_requestConfiguration.PingInterval);
        }

        private async void FileReadyForPost(object sender, RequestInfoEventArgs requestInfoEventArgs)
	    {
		    try
		    {
                // To avoid events firing twice.
                // https://blogs.msdn.microsoft.com/oldnewthing/20140507-00/?p=1053/

                _watcher.EnableRaisingEvents = false;
				await HandleFile(requestInfoEventArgs);
		    }

		    finally
		    {
                _watcher.EnableRaisingEvents = true;
		    }
	    }

		private async Task HandleFile(RequestInfoEventArgs requestInfoEventArgs)
        {
	        try
            {
                bool success;
		        using (var stream = File.OpenRead(requestInfoEventArgs.FullPath))
		        {
                    _logger.LogInformation($"Will post \"{requestInfoEventArgs.FullPath}\" using: \n- StatisticalProgram:{requestInfoEventArgs.StatisticalProgram}\n- OrganizationNumber:{requestInfoEventArgs.OrganizationNumber}\n- FileFormat:{requestInfoEventArgs.FileFormat}");

			        success = await _fileUploader.PostFileAsync(requestInfoEventArgs, stream);
                }

                // Move file on success.
                if (success)
		            MoveFile(requestInfoEventArgs.FullPath);
	        }
	        catch (Exception e)
	        {
				_logger.LogError(e, "Post failed.");
            }
        }

		/// <summary>
        /// Moves the files to done-directory.
        /// </summary>
        /// <param name="fullPath"></param>
        private void MoveFile(string fullPath)
        {
            var file = new FileInfo(fullPath);
            var fileName = $"{Path.GetFileNameWithoutExtension(file.Name)}_{DateTime.Now:yyyy_MM_dd_mm_ss}.{file.Extension}";
            var destination = Path.Combine(_requestConfiguration.DoneDirectory, fileName);
            _logger.LogDebug($"Will move file \"{fullPath}\" to \"{destination}\".");
            File.Move(fullPath, destination);
            _logger.LogDebug($"Finished moving file to \"{destination}\".");
        }


        /// <summary>
        /// Every tick on the timer. Used for heartbeat/ping.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="elapsedEventArgs"></param>
        private async void TimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            // Try to perform a heartbeat/ping check to the server.
            await _fileUploader.PerformHeartBeatAsync();
        }

        /// <summary>
        /// On application startup.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Service started.");

            _watcher.FileReadyForPost += FileReadyForPost;

            // Enable a heartbeat/ping timer.
            _timer.Elapsed += TimerOnElapsed;
            _timer.AutoReset = true;

            // Enable this if you want to enable heartbeat at a given interval.
            // timer.Start();

            // Everything started correctly. Let the log know.
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Service stopped.");
            _timer?.Stop();
            return Task.CompletedTask;
        }
    }
}
