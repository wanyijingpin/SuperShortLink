using System.Threading;
using System.Threading.Tasks;

namespace SuperShortLink.Safety
{
    public interface IUrlSafetyService
    {
        Task<UrlSafetyResult> CheckAsync(string url, CancellationToken cancellationToken = default);
    }
}
