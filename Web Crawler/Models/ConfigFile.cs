namespace Web_Crawler
{
    public partial class ConfigFile
    {
        public string ServersFilename { get; set; }
        public string OutputFilename { get; set; }
        public bool Overwrite { get; set; }
        public int RequestTimeout { get; set; }
    }
}