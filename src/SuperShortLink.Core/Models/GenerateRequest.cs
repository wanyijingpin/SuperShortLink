using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuperShortLink.Models
{
    public class GenerateRequest
    {
        public string generate_url { get; set; }
        public string remark { get; set; }
        public string group_name { get; set; }
    }
}
