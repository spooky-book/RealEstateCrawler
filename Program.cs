using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RealEstateCrawler.Configuration;
using RealEstateCrawler.Services;
using RealEstateCrawler.Util;

namespace RealEstateCrawler;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, builder) =>
            {
                builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                builder.AddEnvironmentVariables(prefix: "REA_CRAWLER_");
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<CrawlerOptions>(context.Configuration.GetSection("Crawler"));
                services.Configure<PlaywrightOptions>(context.Configuration.GetSection("Playwright"));
                services.Configure<StorageOptions>(context.Configuration.GetSection("Storage"));

                services.AddSingleton<IClock, SystemClock>();
                services.AddSingleton<IPlaywrightFactory, PlaywrightFactory>();
                services.AddTransient<IListingCrawler, RealEstateComAuCrawler>();
                services.AddTransient<IListingRepository, JsonListingRepository>();
                services.AddTransient<ICrawlOrchestrator, CrawlOrchestrator>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                });
            })
            .Build();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        var orchestrator = host.Services.GetRequiredService<ICrawlOrchestrator>();
        await orchestrator.RunAsync(cts.Token);
    }
}
