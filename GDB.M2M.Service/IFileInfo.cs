using System.IO;

namespace GDB.M2M.Service
{
    public interface IFileInfo
    {
        IDirectoryInfo Directory();
    }

    public class CustomFileInfo : IFileInfo
    {
        private readonly FileInfo fileInfo;

        public CustomFileInfo(string path)
        {
            fileInfo = new FileInfo(path);
        }

        public IDirectoryInfo Directory()
        {
            return new CustomDirectoryInfo(fileInfo.Directory);
        }
    }
}