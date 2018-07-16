using System;
using System.Collections.Generic;

namespace Web_Crawler.Models
{
    public class ServerLog
    {
        public ICollection<Server> Servers { get; set; }

        public class Server
        {
            public string Name { get; set; }
            public DateTime LastCrawled { get; set; }
        }
    }
}