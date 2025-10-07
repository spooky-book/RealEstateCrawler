namespace RealEstateCrawler.Configuration;

public class CrawlerOptions
{
    public string BaseUrl { get; set; } = "https://www.realestate.com.au";

    public List<SuburbOptions> Suburbs { get; set; } = new();

    /// <summary>
    /// Maximum number of listing result pages to crawl per suburb.
    /// </summary>
    public int ListingPageLimit { get; set; } = 1;

    /// <summary>
    /// Delay applied between navigation requests to mimic human behaviour (milliseconds).
    /// </summary>
    public int DelayBetweenRequestsMs { get; set; } = 1500;

    /// <summary>
    /// Allows experimentation without hitting the live site.
    /// </summary>
    public bool DryRun { get; set; }
}

public class SuburbOptions
{
    public string Query { get; set; } = string.Empty;

    public string? State { get; set; }

    public int? MaxListings { get; set; }

    public Dictionary<string, string> ExtraQueryParameters { get; set; } = new();
}
