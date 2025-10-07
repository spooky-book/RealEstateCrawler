namespace RealEstateCrawler.Services;

public interface ICrawlOrchestrator
{
    Task RunAsync(CancellationToken cancellationToken = default);
}
