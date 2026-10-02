using SuperShortLink.Cache;
using SuperShortLink.Helpers;
using SuperShortLink.Models;
using SuperShortLink.Repository;
using SuperShortLink.Safety;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SuperShortLink
{
    public class ShortLinkService : IShortLinkService
    {
        private readonly IShortLinkRepository _repository;
        private readonly IMemoryCaching _memory;
        private readonly Base62Converter _converter;
        private readonly ILogService _logService;
        private readonly IUrlSafetyService _urlSafetyService;
        private readonly UrlSafetyOptions _urlSafetyOptions;
        public ShortLinkService(IShortLinkRepository repository,
            IMemoryCaching memory,
            Base62Converter converter,
            ILogService logService,
            IUrlSafetyService urlSafetyService,
            Microsoft.Extensions.Options.IOptions<ShortLinkOptions> options)
        {
            _repository = repository;
            _memory = memory;
            _converter = converter;
            _logService = logService;
            _urlSafetyService = urlSafetyService;
            _urlSafetyOptions = options.Value.UrlSafety ?? new UrlSafetyOptions();
        }

        /// <summary>
        /// 分页查询短链信息
        /// </summary>
        public async Task<PageResponseDto<LinkModel>> GetListAsync(RecordListRequest dto)
        {
            return await _repository.GetListAsync(dto);
        }

        /// <summary>
        /// 生成短链
        /// </summary>
        public async Task<string> GenerateAsync(string originUrl)
        {
            var result = await GenerateWithResultAsync(originUrl);
            return result.success ? result.short_url : null;
        }

        public async Task<GenerateResult> GenerateWithResultAsync(string originUrl, string remark = "", string groupName = "")
        {
            originUrl = originUrl?.Trim();
            var safety = await _urlSafetyService.CheckAsync(originUrl);
            if (!safety.Allowed)
            {
                return new GenerateResult { success = false, origin_url = originUrl, message = safety.Reason };
            }

            var cacheUrl = _memory.Get<string>(originUrl);
            if (!cacheUrl.IsNull)
            {
                await _repository.UpdateMetadataByShortUrlAsync(cacheUrl.Value, remark, groupName);
                return new GenerateResult { success = true, short_url = cacheUrl.Value, origin_url = originUrl, remark = remark, group_name = groupName };
            }

            var model = LinkModel.GenDefault(originUrl, remark, groupName);
            var id = await _repository.InsertAsync(model);
            var shortKey = _converter.Confuse(id);
            model.id = id;
            model.short_url = shortKey;
            await _repository.UpdateShortUrlAsync(model);
            _memory.Set(originUrl, shortKey);
            _memory.Set(shortKey, originUrl);
            return new GenerateResult { success = true, short_url = shortKey, origin_url = originUrl, remark = remark, group_name = groupName };
        }

        public async Task<IReadOnlyList<BatchGenerateResult>> GenerateBatchAsync(string urls, string remark = "", string groupName = "")
        {
            var lines = (urls ?? string.Empty).Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
                .Select((url, index) => new { Url = url.Trim(), Line = index + 1 })
                .Where(x => x.Url.Length > 0)
                .ToList();
            if (lines.Count > _urlSafetyOptions.MaxBatchSize)
            {
                return new[] { new BatchGenerateResult { line = 0, success = false, message = $"单次最多生成 {_urlSafetyOptions.MaxBatchSize} 条链接" } };
            }

            var results = new List<BatchGenerateResult>(lines.Count);
            foreach (var line in lines)
            {
                var result = await GenerateWithResultAsync(line.Url, remark, groupName);
                results.Add(new BatchGenerateResult
                {
                    line = line.Line,
                    origin_url = line.Url,
                    short_url = result.short_url,
                    success = result.success,
                    message = result.success ? "生成成功" : result.message
                });
            }
            return results;
        }

        public async Task<int> UpdateMetadataAsync(IEnumerable<int> ids, string remark, string groupName)
        {
            var updated = 0;
            foreach (var id in (ids ?? Enumerable.Empty<int>()).Distinct().Take(1000))
            {
                updated += await _repository.UpdateMetadataAsync(id, remark, groupName);
            }
            return updated;
        }

        public async Task<int> UpdateStatusAsync(IEnumerable<int> ids, int status)
        {
            if (status is not (0 or 1))
            {
                throw new ArgumentOutOfRangeException(nameof(status), "Status must be 0 or 1.");
            }

            var updated = 0;
            foreach (var id in (ids ?? Enumerable.Empty<int>()).Distinct().Take(1000))
            {
                var link = await _repository.GetAsync(id);
                if (link == null)
                {
                    continue;
                }

                updated += await _repository.UpdateStatusAsync(id, status, status == 0 ? "管理员手动停用" : string.Empty);
                _memory.Remove(link.short_url);
                _memory.Remove(link.origin_url);
            }
            return updated;
        }

        public async Task<SafetyReviewResult> ReviewSafetyAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
        {
            var links = new List<LinkModel>();
            foreach (var id in (ids ?? Enumerable.Empty<int>()).Distinct().Take(1000))
            {
                var link = await _repository.GetAsync(id);
                if (link != null)
                {
                    links.Add(link);
                }
            }
            return await ReviewLinksAsync(links, cancellationToken);
        }

        public async Task<SafetyReviewResult> ReviewDueLinksAsync(int batchSize, CancellationToken cancellationToken = default)
        {
            var links = await _repository.GetSafetyReviewCandidatesAsync(batchSize);
            return await ReviewLinksAsync(links, cancellationToken);
        }

        private async Task<SafetyReviewResult> ReviewLinksAsync(IEnumerable<LinkModel> links, CancellationToken cancellationToken)
        {
            var checkedCount = 0;
            var disabledCount = 0;
            foreach (var link in links)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await _urlSafetyService.CheckAsync(link.origin_url, cancellationToken);
                var status = result.Allowed ? link.status : 0;
                var reason = result.Allowed ? string.Empty : result.Reason;
                await _repository.UpdateSafetyReviewAsync(link.id, status, reason);
                checkedCount++;

                if (!result.Allowed)
                {
                    disabledCount++;
                    _memory.Remove(link.short_url);
                    _memory.Remove(link.origin_url);
                }
            }

            return new SafetyReviewResult { checked_count = checkedCount, disabled_count = disabledCount };
        }

        /// <summary>
        /// 访问短链
        /// </summary>
        /// <param name="shortKey"></param>
        /// <returns></returns>
        public async Task<string> AccessAsync(string shortKey)
        {
            var id = _converter.ReCoverConfuse(shortKey);

            if (id == 0)
            {
                return string.Empty;
            }

            var cacheUrl = _memory.Get<string>(shortKey);
            var originUrl = !cacheUrl.IsNull ?
                            cacheUrl.Value :
                            await _repository.GetOriginUrlAsync(id);

            if (!string.IsNullOrEmpty(originUrl))
            {
                if (cacheUrl.IsNull)
                {
                    _memory.Set(shortKey, originUrl);
                }

                await _repository.UpdateAccessDataAsync(id);
                await _logService.Insert(id);
            }
            else
            {
                //防止缓存击穿
                _memory.Set(shortKey, string.Empty);
            }

            return originUrl;
        }
    }
}
