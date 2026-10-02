using Microsoft.AspNetCore.Mvc;
using SuperShortLink.Models;

namespace SuperShortLink
{
    [ApiAuthrize]
    [IgnoreAntiforgeryToken]
    [Route("api/[controller]/[Action]")]
    public class ShortLinkController : Controller
    {
        private readonly IShortLinkService _shortLinkService;
        public ShortLinkController(IShortLinkService shortLinkService)
        {
            _shortLinkService = shortLinkService;
        }

        /// <summary>
        /// 解析生成短网址，并保存到数据库
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Generate([FromBody] ApiGenerateRequest request)
        {
            var result = new ResponseModel<string>();
            var generated = await _shortLinkService.GenerateWithResultAsync(request.generate_url, request.remark, request.group_name);
            result.resultData = generated.short_url;
            if (generated.success)
            {
                result.SetSuccess();
            }
            else
            {
                result.SetError(generated.message);
            }

            return base.Json(result);
        }
    }
}
