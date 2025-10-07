using Microsoft.Playwright;
using Microsoft.Extensions.Options;
using RealEstateCrawler.Configuration;

namespace RealEstateCrawler.Services;

public sealed class PlaywrightFactory : IPlaywrightFactory
{
    private readonly PlaywrightOptions _options;
    private IPlaywright? _playwright;

    public PlaywrightFactory(IOptions<PlaywrightOptions> options)
    {
        _options = options.Value;
    }

    public async Task<IPlaywright> CreateAsync(CancellationToken cancellationToken = default)
    {
        if (_playwright is not null)
        {
            return _playwright;
        }

        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        return _playwright;
    }

    public async Task<IBrowser> CreateBrowserAsync(CancellationToken cancellationToken = default)
    {
        var playwright = await CreateAsync(cancellationToken);
        var browserType = (_options.Browser?.ToLowerInvariant()) switch
        {
            "firefox" => playwright.Firefox,
            "webkit" => playwright.Webkit,
            _ => playwright.Chromium
        };

        return await browserType.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = _options.Headless,
            SlowMo = _options.SlowMoMs > 0 ? _options.SlowMoMs : null
        });
    }

    public ValueTask DisposeAsync()
    {
        _playwright?.Dispose();
        return ValueTask.CompletedTask;
    }
}
