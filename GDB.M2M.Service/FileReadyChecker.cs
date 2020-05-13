using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace GDB.M2M.Service
{
	public interface IFileReadyChecker
	{
		bool IsReady(string filePath);
	}

	/// <summary>
	/// A simple util class that ensures that a file is ready to be read.
	/// During a Windows-copy operation something the FileAddedEvent is triggered before the file
	/// is ready to be read.
	/// </summary>
    public class FileReadyChecker : IFileReadyChecker
	{
		private readonly int _waitTries = 5;
		private readonly int _sleepTime = 3000;

		public bool IsReady(string filePath)
		{
			int tries = 0;
			while (tries < _waitTries)
			{
				try
				{
					using (File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
					{
						break;
					}
				}
				catch (FileNotFoundException ex)
				{
					Trace.WriteLine($"Output file {filePath} not yet ready ({ex.Message})");
				}
				catch (IOException ex)
				{
					Trace.WriteLine($"Output file {filePath} not yet ready ({ex.Message})");
				}
				catch (UnauthorizedAccessException ex)
				{
					Trace.WriteLine($"Output file {filePath} not yet ready ({ex.Message})");
				}
				tries++;

				// Wait a while.
				Thread.Sleep(_sleepTime);
			}
			if (tries == _waitTries)
			{
				return false;
			}
			return true;
		}
	}
}