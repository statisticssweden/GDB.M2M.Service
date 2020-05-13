namespace GDB.M2M.Service
{
    public class RequestInfoEventArgs
    {
        public string OrganizationNumber { get; set; }
        public string StatisticalProgram { get; set; }
        public string FileFormat { get; set; }
        public string Version { get; set; }
        public string FullPath { get; set; }
        public string FileName { get; set; }
    }
}