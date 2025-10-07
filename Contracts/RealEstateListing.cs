namespace RealEstateCrawler.Contracts;

public record RealEstateListing
{
    public ListingIdentity Identity { get; init; } = new();

    public ListingStatusMetadata Status { get; init; } = new();

    public AddressDetails Address { get; init; } = new();

    public CoreAttributes Attributes { get; init; } = new();

    public PricingInformation Pricing { get; init; } = new();

    public Dictionary<string, string?> RawAttributes { get; init; } = new();
}

public record ListingIdentity
{
    public string SourceSite { get; init; } = string.Empty;

    public string SourceListingId { get; init; } = string.Empty;

    public string CanonicalUrl { get; init; } = string.Empty;
}

public record ListingStatusMetadata
{
    public DateTimeOffset? FirstSeenAt { get; init; }

    public DateTimeOffset? LastSeenAt { get; init; }

    public ListingLifecycleStatus LifecycleStatus { get; init; } = ListingLifecycleStatus.Unknown;
}

public enum ListingLifecycleStatus
{
    Unknown = 0,
    Active,
    UnderOffer,
    Sold,
    Withdrawn
}

public record AddressDetails
{
    public string FullAddressRaw { get; init; } = string.Empty;

    public string? StreetNumber { get; init; }

    public string? StreetName { get; init; }

    public string? Suburb { get; init; }

    public string? State { get; init; }

    public string? Postcode { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }
}

public record CoreAttributes
{
    public string? PropertyType { get; init; }

    public double? InternalSizeSqm { get; init; }

    public double? LandSizeSqm { get; init; }

    public int? YearBuilt { get; init; }

    public int? Bedrooms { get; init; }

    public int? Bathrooms { get; init; }

    public int? ParkingSpaces { get; init; }
}

public record PricingInformation
{
    public string? PriceGuideRaw { get; init; }

    public decimal? PriceMinAud { get; init; }

    public decimal? PriceMaxAud { get; init; }

    public decimal? StrataLeviesQuarter { get; init; }

    public decimal? CouncilRatesQuarter { get; init; }

    public decimal? WaterRatesQuarter { get; init; }

    public string? NbnTech { get; init; }
}
