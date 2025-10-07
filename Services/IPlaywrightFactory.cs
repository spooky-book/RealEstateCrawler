using Microsoft.Playwright;

namespace RealEstateCrawler.Services;

public interface IPlaywrightFactory : IAsyncDisposable
{
    Task<IPlaywright> CreateAsync(CancellationToken cancellationToken = default);

    Task<IBrowser> CreateBrowserAsync(CancellationToken cancellationToken = default);
}
