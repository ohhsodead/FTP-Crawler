using System;

namespace Web_Crawler.Models
{
    public partial class WebFile
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
        public DateTime DateUploaded { get; set; }
        public string Host { get; set; }
        public string URL { get; set; }
    }
}