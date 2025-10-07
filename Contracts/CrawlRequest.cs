namespace RealEstateCrawler.Contracts;

public record CrawlRequest
{
    public string SuburbQuery { get; init; } = string.Empty;

    public string? State { get; init; }

    public int? MaxListings { get; init; }

    public IReadOnlyDictionary<string, string> QueryParameters { get; init; } = new Dictionary<string, string>();
}
