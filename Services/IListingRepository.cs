using RealEstateCrawler.Contracts;

namespace RealEstateCrawler.Services;

public interface IListingRepository
{
    Task StoreBatchAsync(CrawlRequest request, IReadOnlyCollection<RealEstateListing> listings, CancellationToken cancellationToken = default);
}
