using System;

namespace FTP_Crawler.Models
{
    public partial class FtpFile
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public DateTime Modified { get; set; }
        public string URL { get; set; }
    }
}