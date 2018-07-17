using System;
using System.Collections.Generic;

namespace FTP_Crawler.Models
{
    public class ServerHistory
    {
        public ICollection<Server> Servers { get; set; }

        public class Server
        {
            public string Name { get; set; }
            public DateTime LastCrawled { get; set; }
        }
    }
}