namespace GDB.M2M.Service.Configurations
{
    public class M2MConfiguration
    {
        /// <summary>
        /// Base url for the request. Such as http://www.indataportalen.gdb.scb.se/
        /// </summary>
        public string BaseUrl { get; set; }

        /// <summary>
        /// The serial number of the installed certificate.
        /// </summary>
        public string CertificateSerialNumber { get; set; }

        /// <summary>
        /// The resource-path for uploading files. The part after the domain. Use string interpolation.
        /// </summary>
        public string FileUploadResource { get; set; } =
            @"file/{organisationNumber}/{statisticalProgram}/{referencePeriod}/{fileFormat}/{fileName}/{version}";

        /// <summary>
        /// The resource path for uploading file in chunks. The part after the domain. Use string interpolation.
        /// </summary>
        public string FileUploadResourceV2 { get; set; } =
            @"file/{segment}/{organisationNumber}/{statisticalProgram}/{referencePeriod}/{fileFormat}/{fileName}/{version}";

        /// <summary>
        /// The resource-path for heartbeat. The part after the domain.
        /// </summary>
        public string PingResource { get; set; } = "heartbeat";

        /// <summary>
        /// The resource-path for heartbeat. The part after the domain.
        /// </summary>
        public int PingInterval { get; set; } = 60000;


        /// <summary>
        /// The organisation number for the client. Can be overridden by the file structure.
        /// </summary>
        public string OrganisationNumber { get; set; }

        /// <summary>
        /// Name for the statistical program the file is meant for. 
        /// </summary>
        public string StatisticalProgram { get; set; }

        /// <summary>
        /// FileFormat for the file. Eg. V40, V10, KRITA_Monthly
        /// </summary>
        public string FileFormat { get; set; }

        /// <summary>
        /// Version of the FileFormat.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// The reference period of the file
        /// </summary>
        public string ReferencePeriod { get; set; }

        /// <summary>
        /// The directory where the files will be dropped.
        /// </summary>
        public string ReadDirectory { get; set; }

        /// <summary>
        /// The directory where the files will be places when they've been processed
        /// </summary>

        public string DoneDirectory { get; set; }
    }
}
