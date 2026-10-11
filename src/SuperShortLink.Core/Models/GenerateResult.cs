using System.Collections.Generic;

namespace SuperShortLink.Models
{
    public sealed class GenerateResult
    {
        public bool success { get; init; }
        public string short_url { get; init; }
        public string origin_url { get; init; }
        public string remark { get; init; }
        public string group_name { get; init; }
        public string message { get; init; }
    }

    public sealed class BatchGenerateResult
    {
        public int line { get; init; }
        public string source_text { get; init; }
        public string origin_url { get; init; }
        public string short_url { get; init; }
        public string remark { get; init; }
        public string output_text { get; init; }
        public string message { get; init; }
        public bool success { get; init; }
    }

    public sealed class BatchGenerateRequest
    {
        public string urls { get; set; }
        public string remark { get; set; }
        public string group_name { get; set; }
    }

    public sealed class BatchInputLine
    {
        public int line { get; init; }
        public string source_text { get; init; }
        public string url { get; init; }
        public string remark { get; init; }
    }

    public sealed class BatchMetadataRequest
    {
        public IEnumerable<int> ids { get; set; } = new List<int>();
        public string remark { get; set; }
        public string group_name { get; set; }
    }

    public sealed class BatchStatusRequest
    {
        public IEnumerable<int> ids { get; set; } = new List<int>();
        public int status { get; set; }
    }

    public sealed class SafetyReviewResult
    {
        public int checked_count { get; init; }
        public int disabled_count { get; init; }
    }

    public sealed class BatchReviewRequest
    {
        public IEnumerable<int> ids { get; set; } = new List<int>();
    }
}
