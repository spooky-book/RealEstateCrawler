using RealEstateCrawler.Contracts;

namespace RealEstateCrawler.Services;

public interface IListingCrawler
{
    IAsyncEnumerable<RealEstateListing> CrawlAsync(CrawlRequest request, CancellationToken cancellationToken = default);
}
