namespace SuperShortLink.Safety
{
    public sealed class UrlSafetyResult
    {
        public bool Allowed { get; init; }
        public string Reason { get; init; } = string.Empty;

        public static UrlSafetyResult Allow() => new() { Allowed = true };
        public static UrlSafetyResult Deny(string reason) => new() { Allowed = false, Reason = reason };
    }
}
