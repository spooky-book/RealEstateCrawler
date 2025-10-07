namespace RealEstateCrawler.Configuration;

public class StorageOptions
{
    public string OutputDirectory { get; set; } = "output";

    public string FileNameFormat { get; set; } = "{suburb}_{timestamp:yyyyMMddHHmmss}.ndjson";
}
