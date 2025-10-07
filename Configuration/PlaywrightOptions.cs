namespace RealEstateCrawler.Configuration;

public class PlaywrightOptions
{
    public string Browser { get; set; } = "chromium";

    public bool Headless { get; set; } = true;

    public int SlowMoMs { get; set; }

    public int NavigationTimeoutMs { get; set; } = 30_000;
}
