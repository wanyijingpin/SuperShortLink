using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SuperShortLink.Cache;
using SuperShortLink.Charts;
using SuperShortLink.Helpers;
using SuperShortLink.Models;
using SuperShortLink.Repository;

namespace SuperShortLink
{
    [LoginAuthrize]
    [AutoValidateAntiforgeryToken]
    public class HomeController : Controller
    {
        private readonly IShortLinkService _shortLinkService;
        private readonly IApplicationService _applicationService;
        private readonly ChartFactory _chartFactory;

        public HomeController(IShortLinkService shortLinkService
            , IApplicationService applicationService
            , ChartFactory chartFactory)
        {
            _shortLinkService = shortLinkService;
            _applicationService = applicationService;
            _chartFactory = chartFactory;
        }

        #region 面板

        public IActionResult Dashboard()
        {
            return View();
        }

        /// <summary>
        /// 生成短链
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Generate([FromBody] GenerateRequest request)
        {
            var result = await _shortLinkService.GenerateWithResultAsync(request.generate_url, request.remark, request.group_name);
            return result.success ? base.Json(result) : BadRequest(result);
        }

        [HttpPost]
        public async Task<IActionResult> BatchGenerate([FromBody] BatchGenerateRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { success = false, message = "请求不能为空" });
            }

            var result = await _shortLinkService.GenerateBatchAsync(request.urls, request.remark, request.group_name);
            return base.Json(result);
        }

        /// <summary>
        /// 查询图表
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> GetChart([FromBody] GetChartRequest request)
        {
            var result = await _chartFactory.GetChart(request.ChartDataType).GetCharts();
            return base.Json(new { access = result.Access, generate = result.Generate, labels = result.Labels, title = result.Title });
        }

        #endregion

        #region 短链列表

        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// 查询短链列表
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> List([FromBody] RecordListRequest request)
        {
            var pageData = await _shortLinkService.GetListAsync(request);

            return base.Json(new { pageData }, DateTimeConvertor.Serializer);
        }

        #endregion

        #region 授权应用

        public IActionResult Application()
        {
            return View();
        }

        /// <summary>
        /// 授权应用列表
        /// </summary>

        [HttpPost]
        public async Task<IActionResult> ApplicationList([FromBody] ApplicationListRequest request)
        {
            var result = await _applicationService.QueryPageAsync(request);
            return base.Json(result, DateTimeConvertor.Serializer);
        }

        /// <summary>
        /// 添加授权应用
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddApplication([FromBody] ApplicationModel request)
        {
            var result = await _applicationService.AddAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// 更新秘钥
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpdateApplicationSecret([FromBody] AppUpdateRequest request)
        {
            var result = await _applicationService.UpdateSecretAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// 删除应用
        /// </summary>
        /// <param name="update"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> DeleteApplication([FromBody] AppUpdateRequest request)
        {
            var result = await _applicationService.DeleteAsync(request.appId);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> BatchUpdateMetadata([FromBody] BatchMetadataRequest request)
        {
            var result = await _shortLinkService.UpdateMetadataAsync(request.ids, request.remark, request.group_name);
            return Ok(new { success = true, updated = result });
        }

        [HttpPost]
        public async Task<IActionResult> BatchUpdateStatus([FromBody] BatchStatusRequest request)
        {
            if (request.status is not (0 or 1))
            {
                return BadRequest(new { success = false, message = "状态值无效" });
            }

            var result = await _shortLinkService.UpdateStatusAsync(request.ids, request.status);
            return Ok(new { success = true, updated = result });
        }

        [HttpPost]
        public async Task<IActionResult> BatchReviewSafety([FromBody] BatchReviewRequest request)
        {
            var result = await _shortLinkService.ReviewSafetyAsync(request.ids, HttpContext.RequestAborted);
            return Ok(new { success = true, result.checked_count, result.disabled_count });
        }

        #endregion

        #region 短链重定向

        [HttpGet("{key?}")]
        [AllowAnonymous]
        public async Task<IActionResult> Access(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return RedirectToAction("Index", "Login");
            }

            var url = await _shortLinkService.AccessAsync(key);
            if (!string.IsNullOrWhiteSpace(url))
            {
                return Redirect(url);
            }
            return new NotFoundResult();
        }

        #endregion
    }
}
