using System.IO;

namespace GDB.M2M.Service
{
    public interface IDirectoryInfo
    {
        IDirectoryInfo Parent();
        string Name { get; }
    }

    public class CustomDirectoryInfo : IDirectoryInfo
    {
        private readonly DirectoryInfo directoryInfo;

        public CustomDirectoryInfo(DirectoryInfo directoryInfo)
        {
            this.directoryInfo = directoryInfo;
        }

        public string Name
        {
            get { return directoryInfo.Name; }
        }

        public IDirectoryInfo Parent()
        {
            return new CustomDirectoryInfo(directoryInfo.Parent);
        }
    }
}