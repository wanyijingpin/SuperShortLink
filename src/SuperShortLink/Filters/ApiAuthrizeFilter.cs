using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SuperShortLink.Models;
using SuperShortLink.Helpers;
using System.Collections.Concurrent;
using System.Security.Cryptography;
namespace SuperShortLink
{
    public class ApiAuthrizeAttribute : TypeFilterAttribute
    {
        public ApiAuthrizeAttribute() : base(typeof(ApiAuthrizeFilter))
        {

        }
    }

    public class ApiAuthrizeFilter : Attribute, IAsyncActionFilter
    {
        private static readonly ConcurrentDictionary<string, long> UsedNonces = new();
        private readonly ILogger<ApiAuthrizeFilter> _logger;
        private readonly IApplicationService _applicationService;

        public ApiAuthrizeFilter(IApplicationService applicationService
            , ILogger<ApiAuthrizeFilter> logger)
        {
            _applicationService = applicationService;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var allowAnonymous = context.HttpContext.GetEndpoint().Metadata.GetMetadata<IAllowAnonymous>();
            if (allowAnonymous != null)
            {
                await next();
                return;
            }

            //获取参数
            var dic = GetParamsDictionary(context);
            if (dic == null)
            {
                _logger.LogWarning($"{nameof(ApiAuthrizeFilter)}--获取参数失败");
                return;
            }

            //验证时间
            var isValid = CheckTimestamp(context, dic);
            if (!isValid)
            {
                _logger.LogWarning($"{nameof(ApiAuthrizeFilter)}--验证时间失败--{System.Text.Json.JsonSerializer.Serialize(dic)}");
                return;
            }

            //验证app_code
            var appInfo = await CheckAppCodeAsync(context, dic);
            if (appInfo == null)
            {
                _logger.LogWarning($"{nameof(ApiAuthrizeFilter)}--验证app_code失败--{System.Text.Json.JsonSerializer.Serialize(dic)}");
                return;
            }

            //验证token
            isValid = CheckToken(context, dic, appInfo);
            if (!isValid)
            {
                return;
            }

            await next();
        }

        private bool CheckToken(ActionExecutingContext context, Dictionary<string, string> dic, ApplicationModel appInfo)
        {
            var signature = context.HttpContext.Request.Headers["X-Signature"].FirstOrDefault();
            var nonce = dic.GetValueOrDefault(nameof(ApiBaseDto.nonce));
            if (string.IsNullOrWhiteSpace(nonce) || nonce.Length > 128)
            {
                context.Result = new UnauthorizedObjectResult(new ResponseModel { resultMsg = "接口权限验证失败[nonce]" });
                return false;
            }

            var canonical = string.Join("&", dic
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => $"{x.Key}={Uri.EscapeDataString(x.Value ?? string.Empty)}"));
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appInfo.app_secret ?? string.Empty));
            var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
            var signatureBytes = Encoding.UTF8.GetBytes(signature ?? string.Empty);
            var expectedBytes = Encoding.UTF8.GetBytes(expected);
            var validSignature = signatureBytes.Length == expectedBytes.Length
                && CryptographicOperations.FixedTimeEquals(signatureBytes, expectedBytes);
            var replayKey = $"{dic.GetValueOrDefault(nameof(ApiBaseDto.app_code))}:{nonce}";
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (var item in UsedNonces.Where(x => x.Value < now - 120).ToArray())
            {
                UsedNonces.TryRemove(item.Key, out _);
            }
            if (!validSignature || !UsedNonces.TryAdd(replayKey, now))
            {
                _logger.LogWarning("{Filter}--API签名验证失败", nameof(ApiAuthrizeFilter));
                context.Result = new UnauthorizedObjectResult(new ResponseModel { resultMsg = "接口权限验证失败[signature]" });
                return false;
            }

            return true;
        }

        private async Task<ApplicationModel> CheckAppCodeAsync(ActionExecutingContext context, Dictionary<string, string> dic)
        {
            try
            {
                if (dic.ContainsKey(nameof(ApiBaseDto.app_code)))
                {
                    var appInfo = await _applicationService.GetByCodeAsync(dic[nameof(ApiBaseDto.app_code)]);
                    if (appInfo != null)
                    {
                        return appInfo;
                    }
                }

                context.Result = new UnauthorizedObjectResult(new ResponseModel { resultMsg = "接口权限验证失败[app_code]" });
                return null;
            }
            catch (Exception e)
            {

                context.Result = new UnauthorizedObjectResult(new ResponseModel { resultMsg = $"接口权限验证失败[app_code]:{e.Message}" });
                return null;
            }
        }

        private bool CheckTimestamp(ActionExecutingContext context, Dictionary<string, string> dic)
        {
            if (dic.ContainsKey(nameof(ApiBaseDto.timestamp)))
            {
                var timestampStr = dic[nameof(ApiBaseDto.timestamp)];
                var offsetTime = 60; //1分钟内的请求校验

                if (double.TryParse(timestampStr, out var time) &&
                    time <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() &&
                    time > DateTimeOffset.UtcNow.AddSeconds(-offsetTime).ToUnixTimeSeconds())
                {
                    return true;
                }
            }

            context.Result = new UnauthorizedObjectResult(new ResponseModel { resultMsg = "接口权限验证失败[timestamp]" });
            return false;
        }

        private Dictionary<string, string> GetParamsDictionary(ActionExecutingContext context)
        {
            var paramsObj = context.ActionArguments.FirstOrDefault().Value;
            if (paramsObj != null)
            {
                var dic = new Dictionary<string, string>();

                foreach (var c in paramsObj.GetType().GetProperties().OrderBy(c => c.Name))
                {
                    var val = c.GetValue(paramsObj);
                    dic.Add(c.Name, val?.ToString());
                }

                return dic;
            }

            context.Result = new UnauthorizedObjectResult(new ResponseModel() { resultMsg = "接口权限验证失败[参数]" });
            return null;
        }
    }
}
