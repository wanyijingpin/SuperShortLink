using System.Collections.Generic;

namespace SuperShortLink
{
    public class UrlSafetyOptions
    {
        public bool Enabled { get; set; } = true;
        public int MaxBatchSize { get; set; } = 200;
        public bool PeriodicReviewEnabled { get; set; } = true;
        public int ReviewIntervalMinutes { get; set; } = 60;
        public int ReviewBatchSize { get; set; } = 100;
        public string ModerationEndpoint { get; set; } = string.Empty;
        public string ModerationApiKey { get; set; } = string.Empty;
        public string[] BlockedHosts { get; set; } = System.Array.Empty<string>();
        public string[] BlockedKeywords { get; set; } = new[]
        {
            "博彩", "赌博", "色情", "裸聊", "诈骗", "钓鱼", "木马", "病毒",
            "枪支", "毒品", "洗钱", "假证", "刷单", "外挂"
        };
    }
}
