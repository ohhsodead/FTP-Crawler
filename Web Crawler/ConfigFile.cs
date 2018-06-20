using System;

namespace Web_Crawler
{
    public partial class ConfigFile
    {
        public string ServerList { get; set; }
        public string PathToWrite { get; set; }
        public bool OneFile { get; set; }
        public string OneFileName { get; set; }
        public bool RewriteList { get; set; }
        public int RequestTimeout { get; set; }
        public bool SubDirectories { get; set; }
        public string FileTypes { get; set; }
    }
}