using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SuperShortLink.Safety
{
    public sealed class UrlSafetyService : IUrlSafetyService
    {
        private readonly UrlSafetyOptions _options;
        private readonly HttpClient _httpClient;
        private readonly ILogger<UrlSafetyService> _logger;

        public UrlSafetyService(IOptions<ShortLinkOptions> options, IHttpClientFactory httpClientFactory, ILogger<UrlSafetyService> logger)
        {
            _options = options.Value.UrlSafety ?? new UrlSafetyOptions();
            _httpClient = httpClientFactory.CreateClient("url-moderation");
            _logger = logger;
        }

        public async Task<UrlSafetyResult> CheckAsync(string url, CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
            {
                return UrlSafetyResult.Allow();
            }

            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            {
                return UrlSafetyResult.Deny("链接格式无效");
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                return UrlSafetyResult.Deny("只允许 http 或 https 链接");
            }

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                return UrlSafetyResult.Deny("不允许包含账号密码的链接");
            }

            var host = uri.Host.TrimEnd('.').ToLowerInvariant();
            if (IsPrivateOrLocalHost(host))
            {
                return UrlSafetyResult.Deny("不允许发布指向本机、内网或云元数据地址的链接");
            }

            if (!IPAddress.TryParse(host, out _))
            {
                try
                {
                    var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
                    if (addresses.Length == 0 || addresses.Any(IsPrivateAddress))
                    {
                        return UrlSafetyResult.Deny("域名解析到了本机、内网或云元数据地址");
                    }
                }
                catch (System.Net.Sockets.SocketException)
                {
                    return UrlSafetyResult.Deny("链接域名无法解析");
                }
            }

            if ((_options.BlockedHosts ?? Array.Empty<string>()).Any(pattern => HostMatches(host, pattern)))
            {
                return UrlSafetyResult.Deny("链接域名在黑名单中");
            }

            var searchableText = Uri.UnescapeDataString(uri.AbsoluteUri).ToLowerInvariant();
            var keyword = (_options.BlockedKeywords ?? Array.Empty<string>())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x) && searchableText.Contains(x.Trim().ToLowerInvariant(), StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                return UrlSafetyResult.Deny("链接包含禁止发布的风险内容");
            }

            if (!string.IsNullOrWhiteSpace(_options.ModerationEndpoint))
            {
                return await CheckRemoteModerationAsync(uri.AbsoluteUri, cancellationToken);
            }

            return UrlSafetyResult.Allow();
        }

        private async Task<UrlSafetyResult> CheckRemoteModerationAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _options.ModerationEndpoint);
                request.Content = new StringContent(JsonSerializer.Serialize(new { url }), Encoding.UTF8, "application/json");
                if (!string.IsNullOrWhiteSpace(_options.ModerationApiKey))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ModerationApiKey);
                }

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("URL moderation provider returned {StatusCode}; rejecting publication", response.StatusCode);
                    return UrlSafetyResult.Deny("外部内容审核服务暂时不可用，请稍后重试");
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var result = await JsonSerializer.DeserializeAsync<ModerationResponse>(stream, cancellationToken: cancellationToken);
                return result?.Allowed == true
                    ? UrlSafetyResult.Allow()
                    : UrlSafetyResult.Deny("外部内容审核未通过");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return UrlSafetyResult.Deny("外部内容审核超时，请稍后重试");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "URL moderation provider failed; rejecting publication");
                return UrlSafetyResult.Deny("外部内容审核服务异常，请稍后重试");
            }
        }

        private static bool HostMatches(string host, string pattern)
        {
            pattern = (pattern ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
            return pattern.Length > 0 && (host == pattern || host.EndsWith("." + pattern, StringComparison.Ordinal));
        }

        private static bool IsPrivateOrLocalHost(string host)
        {
            if (host == "localhost" || host.EndsWith(".local", StringComparison.Ordinal) || host == "metadata.google.internal")
            {
                return true;
            }

            if (!IPAddress.TryParse(host, out var address))
            {
                return false;
            }

            return IsPrivateAddress(address);
        }

        private static bool IsPrivateAddress(IPAddress address)
        {
            if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            {
                return true;
            }

            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var bytes = address.GetAddressBytes();
                return bytes[0] == 10
                    || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                    || (bytes[0] == 192 && bytes[1] == 168)
                    || (bytes[0] == 169 && bytes[1] == 254);
            }

            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal;
        }

        private sealed class ModerationResponse
        {
            public bool Allowed { get; set; }
        }
    }
}
