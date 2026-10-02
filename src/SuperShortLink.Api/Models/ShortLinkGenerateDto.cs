using System;
using System.Collections.Generic;
using System.Text;

namespace SuperShortLink.Api.Models
{
    internal class ShortLinkGenerateDto : ShortLinkBaseDto
    {
        public string generate_url { get; set; }
        public string remark { get; set; } = string.Empty;
        public string group_name { get; set; } = string.Empty;
    }
}
