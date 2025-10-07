using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using RealEstateCrawler.Configuration;
using RealEstateCrawler.Contracts;
using RealEstateCrawler.Util;
using System.Net;
using System.Runtime.CompilerServices;

namespace RealEstateCrawler.Services;

public sealed class RealEstateComAuCrawler : IListingCrawler
{
    private readonly ILogger<RealEstateComAuCrawler> _logger;
    private readonly IPlaywrightFactory _playwrightFactory;
    private readonly CrawlerOptions _crawlerOptions;
    private readonly PlaywrightOptions _playwrightOptions;
    private readonly IClock _clock;

    private const string SourceSite = "realestate.com.au";

    public RealEstateComAuCrawler(
        ILogger<RealEstateComAuCrawler> logger,
        IPlaywrightFactory playwrightFactory,
        IOptions<CrawlerOptions> crawlerOptions,
        IOptions<PlaywrightOptions> playwrightOptions,
        IClock clock)
    {
        _logger = logger;
        _playwrightFactory = playwrightFactory;
        _clock = clock;
        _crawlerOptions = crawlerOptions.Value;
        _playwrightOptions = playwrightOptions.Value;
    }

    public async IAsyncEnumerable<RealEstateListing> CrawlAsync(CrawlRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_crawlerOptions.DryRun)
        {
            _logger.LogWarning("Crawler running in dry-run mode; returning a synthetic listing for {Suburb}.", request.SuburbQuery);
            yield return CreateDryRunListing(request);
            yield break;
        }

        await using var browser = await _playwrightFactory.CreateBrowserAsync(cancellationToken);
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            Locale = "en-AU",
            IgnoreHTTPSErrors = true
        });

        try
        {
            var searchUrl = BuildSearchUrl(request);
            _logger.LogInformation("Navigating to search page {Url}.", searchUrl);

            var page = await context.NewPageAsync();
            page.SetDefaultTimeout(_playwrightOptions.NavigationTimeoutMs);
            await page.GotoAsync(searchUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = _playwrightOptions.NavigationTimeoutMs
            });

            var listingUrls = await ExtractListingUrlsAsync(page, request, cancellationToken);
            _logger.LogInformation("Discovered {Count} candidate listings for {Suburb}.", listingUrls.Count, request.SuburbQuery);

            var processed = 0;
            foreach (var url in listingUrls)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (request.MaxListings is int max && processed >= max)
                {
                    _logger.LogInformation("Reached max listing limit ({Limit}) for {Suburb}.", max, request.SuburbQuery);
                    yield break;
                }

                var listing = await ScrapeListingAsync(context, url, cancellationToken);
                if (listing is not null)
                {
                    processed++;
                    yield return listing;
                }
            }
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    private async Task<IReadOnlyList<string>> ExtractListingUrlsAsync(IPage page, CrawlRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var anchors = await page.Locator("a[data-testid='listing-card-link']").AllAsync();
            if (anchors.Count == 0)
            {
                return Array.Empty<string>();
            }

            var filtered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var anchor in anchors)
            {
                var href = await anchor.GetAttributeAsync("href");
                if (string.IsNullOrWhiteSpace(href))
                {
                    continue;
                }

                var queryIndex = href.IndexOf("?", StringComparison.Ordinal);
                var normalized = queryIndex >= 0 ? href[..queryIndex] : href;
                filtered.Add(normalized);
            }

            if (_crawlerOptions.ListingPageLimit > 1)
            {
                _logger.LogDebug("ListingPageLimit > 1 specified. TODO: handle pagination explicitly.");
            }

            return filtered.ToList();
        }
        catch (PlaywrightException ex)
        {
            _logger.LogError(ex, "Failed to extract listing URLs for {Suburb}.", request.SuburbQuery);
            return Array.Empty<string>();
        }
    }

    private async Task<RealEstateListing?> ScrapeListingAsync(IBrowserContext context, string listingUrl, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Scraping listing {Url}.", listingUrl);
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(_playwrightOptions.NavigationTimeoutMs);

        try
        {
            await page.GotoAsync(listingUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = _playwrightOptions.NavigationTimeoutMs
            });

            var address = await GetTextContentAsync(page, "[data-testid='listing-details__address']");
            var priceGuide = await GetTextContentAsync(page, "[data-testid='listing-details__price']");
            var propertyType = await GetTextContentAsync(page, "[data-testid='listing-summary-property-type']");

            var bedroomCount = await ParseFeatureAsync(page, "[data-testid='general-features__beds']");
            var bathroomCount = await ParseFeatureAsync(page, "[data-testid='general-features__baths']");
            var parkingCount = await ParseFeatureAsync(page, "[data-testid='general-features__cars']");

            var listing = new RealEstateListing
            {
                Identity = new ListingIdentity
                {
                    SourceSite = SourceSite,
                    SourceListingId = ExtractListingId(listingUrl),
                    CanonicalUrl = listingUrl
                },
                Status = new ListingStatusMetadata
                {
                    FirstSeenAt = _clock.UtcNow,
                    LastSeenAt = _clock.UtcNow,
                    LifecycleStatus = ListingLifecycleStatus.Active
                },
                Address = new AddressDetails
                {
                    FullAddressRaw = address ?? string.Empty
                },
                Attributes = new CoreAttributes
                {
                    PropertyType = propertyType,
                    Bedrooms = bedroomCount,
                    Bathrooms = bathroomCount,
                    ParkingSpaces = parkingCount
                },
                Pricing = new PricingInformation
                {
                    PriceGuideRaw = priceGuide
                }
            };

            listing.RawAttributes["pageUrl"] = listingUrl;
            listing.RawAttributes["scrapedAt"] = _clock.UtcNow.ToString("O");

            return listing;
        }
        catch (PlaywrightException ex)
        {
            _logger.LogError(ex, "Failed to scrape listing at {Url}.", listingUrl);
            return null;
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private static async Task<string?> GetTextContentAsync(IPage page, string selector)
    {
        var handle = await page.QuerySelectorAsync(selector);
        if (handle is null)
        {
            return null;
        }

        var value = await handle.InnerTextAsync();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static async Task<int?> ParseFeatureAsync(IPage page, string selector)
    {
        var text = await GetTextContentAsync(page, selector);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var digits = new string(text.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) ? value : null;
    }

    private RealEstateListing CreateDryRunListing(CrawlRequest request)
    {
        return new RealEstateListing
        {
            Identity = new ListingIdentity
            {
                SourceSite = SourceSite,
                SourceListingId = Guid.NewGuid().ToString("N"),
                CanonicalUrl = $"{_crawlerOptions.BaseUrl.TrimEnd('/')}/sample-listing"
            },
            Status = new ListingStatusMetadata
            {
                FirstSeenAt = _clock.UtcNow,
                LastSeenAt = _clock.UtcNow,
                LifecycleStatus = ListingLifecycleStatus.Active
            },
            Address = new AddressDetails
            {
                FullAddressRaw = $"{request.SuburbQuery} (dry run)",
                State = request.State
            },
            Attributes = new CoreAttributes
            {
                PropertyType = "house",
                Bedrooms = 3,
                Bathrooms = 2,
                ParkingSpaces = 1,
                InternalSizeSqm = 120
            },
            Pricing = new PricingInformation
            {
                PriceGuideRaw = "$1,000,000 - $1,100,000",
                PriceMinAud = 1_000_000,
                PriceMaxAud = 1_100_000,
                NbnTech = "FTTP"
            }
        };
    }

    private string BuildSearchUrl(CrawlRequest request)
    {
        var baseUri = _crawlerOptions.BaseUrl.TrimEnd('/');
        var parameters = new Dictionary<string, string>
        {
            ["includeSurrounding"] = "false",
            ["source"] = "refine",
            ["activeSort"] = "default",
            ["where"] = request.SuburbQuery
        };

        foreach (var parameter in request.QueryParameters)
        {
            parameters[parameter.Key] = parameter.Value;
        }

        var query = string.Join("&", parameters.Select(kvp => $"{WebUtility.UrlEncode(kvp.Key)}={WebUtility.UrlEncode(kvp.Value)}"));
        return $"{baseUri}/buy?{query}";
    }

    private static string ExtractListingId(string listingUrl)
    {
        try
        {
            var uri = new Uri(listingUrl);
            var lastSegment = uri.Segments.LastOrDefault()?.Trim('/') ?? listingUrl;
            var digits = new string(lastSegment.Where(char.IsDigit).ToArray());
            return string.IsNullOrWhiteSpace(digits) ? lastSegment : digits;
        }
        catch (Exception)
        {
            return listingUrl;
        }
    }
}
