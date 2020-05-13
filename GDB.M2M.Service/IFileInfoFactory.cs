namespace GDB.M2M.Service
{
    public interface IFileInfoFactory
    {
        IFileInfo Create(string filePath);
    }

    public class FileInfoFactory : IFileInfoFactory
    {
        public IFileInfo Create(string filePath)
        {
            return new CustomFileInfo(filePath);
        }
    }
}