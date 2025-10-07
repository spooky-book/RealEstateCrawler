using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealEstateCrawler.Configuration;
using RealEstateCrawler.Contracts;
using RealEstateCrawler.Util;

namespace RealEstateCrawler.Services;

public sealed class JsonListingRepository : IListingRepository
{
    private readonly StorageOptions _options;
    private readonly ILogger<JsonListingRepository> _logger;
    private readonly IClock _clock;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly Regex TimestampTokenRegex = new("\\{timestamp:(?<format>[^}]+)\\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public JsonListingRepository(IOptions<StorageOptions> options, ILogger<JsonListingRepository> logger, IClock clock)
    {
        _options = options.Value;
        _logger = logger;
        _clock = clock;
    }

    public async Task StoreBatchAsync(CrawlRequest request, IReadOnlyCollection<RealEstateListing> listings, CancellationToken cancellationToken = default)
    {
        if (listings.Count == 0)
        {
            _logger.LogDebug("Skipping storage for {Suburb} because no listings were provided.", request.SuburbQuery);
            return;
        }

        var outputDirectory = Path.GetFullPath(_options.OutputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var fileName = ResolveFileName(request);
        var path = Path.Combine(outputDirectory, fileName);

        _logger.LogInformation("Writing {Count} listings to {Path}.", listings.Count, path);

        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));

        foreach (var listing in listings)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = JsonSerializer.Serialize(listing, _serializerOptions);
            await writer.WriteLineAsync(json);
        }

        await writer.FlushAsync();
    }

    private string ResolveFileName(CrawlRequest request)
    {
        var timestamp = _clock.UtcNow;
        var safeSuburb = SanitizeForFileName(request.SuburbQuery.Replace(' ', '_'));

        var fileName = _options.FileNameFormat;
        fileName = fileName.Replace("{suburb}", safeSuburb, StringComparison.OrdinalIgnoreCase);
        fileName = fileName.Replace("{state}", request.State ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        fileName = TimestampTokenRegex.Replace(fileName, match =>
        {
            var format = match.Groups["format"].Value;
            return timestamp.ToString(format);
        });

        if (fileName.Contains("{timestamp}", StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName.Replace("{timestamp}", timestamp.ToString("yyyyMMddHHmmss"), StringComparison.OrdinalIgnoreCase);
        }

        fileName = SanitizeForFileName(fileName);
        return fileName;
    }

    private static string SanitizeForFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(invalid.Contains(character) ? '_' : character);
        }

        return builder.ToString();
    }
}
