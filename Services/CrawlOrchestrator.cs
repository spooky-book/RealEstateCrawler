using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealEstateCrawler.Configuration;
using RealEstateCrawler.Contracts;
using RealEstateCrawler.Util;

namespace RealEstateCrawler.Services;

public sealed class CrawlOrchestrator : ICrawlOrchestrator
{
    private readonly IListingCrawler _crawler;
    private readonly IListingRepository _repository;
    private readonly ILogger<CrawlOrchestrator> _logger;
    private readonly CrawlerOptions _options;
    private readonly IClock _clock;

    public CrawlOrchestrator(
        IListingCrawler crawler,
        IListingRepository repository,
        IOptions<CrawlerOptions> options,
        ILogger<CrawlOrchestrator> logger,
        IClock clock)
    {
        _crawler = crawler;
        _repository = repository;
        _logger = logger;
        _clock = clock;
        _options = options.Value;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (_options.Suburbs.Count == 0)
        {
            _logger.LogWarning("No suburbs configured. Update appsettings.json or pass configuration via environment variables.");
            return;
        }

        _logger.LogInformation("Starting crawl for {SuburbCount} suburb(s).", _options.Suburbs.Count);

        foreach (var suburb in _options.Suburbs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = BuildRequest(suburb);

            _logger.LogInformation("Crawling suburb query {Suburb} with limit {Limit}.", request.SuburbQuery, request.MaxListings);

            var listings = new List<RealEstateListing>();
            await foreach (var listing in _crawler.CrawlAsync(request, cancellationToken).WithCancellation(cancellationToken))
            {
                listings.Add(listing);
            }

            if (listings.Count == 0)
            {
                _logger.LogWarning("No listings captured for {Suburb}.", request.SuburbQuery);
            }
            else
            {
                await _repository.StoreBatchAsync(request, listings, cancellationToken);
                _logger.LogInformation("Persisted {Count} listings for {Suburb}.", listings.Count, request.SuburbQuery);
            }

            if (_options.DelayBetweenRequestsMs > 0)
            {
                var delay = TimeSpan.FromMilliseconds(_options.DelayBetweenRequestsMs);
                _logger.LogDebug("Delaying for {Delay} before next suburb.", delay);
                await Task.Delay(delay, cancellationToken);
            }
        }

        _logger.LogInformation("Crawl completed at {Timestamp}.", _clock.UtcNow);
    }

    private static CrawlRequest BuildRequest(SuburbOptions suburb)
    {
        var request = new CrawlRequest
        {
            SuburbQuery = suburb.Query,
            State = suburb.State,
            MaxListings = suburb.MaxListings,
            QueryParameters = new Dictionary<string, string>(suburb.ExtraQueryParameters, StringComparer.OrdinalIgnoreCase)
        };

        return request;
    }
}
