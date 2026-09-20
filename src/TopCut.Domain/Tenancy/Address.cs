using TopCut.Domain.Common;

namespace TopCut.Domain.Tenancy;

/// <summary>
/// A structured address, not a free-text line. Stored broken down from v1 so that switching on
/// "tournaments near me" in the mobile app is a distance query rather than a redesign
/// (see docs/arquitectura.md).
/// </summary>
public sealed record Address
{
    private Address() { }

    public Address(string street, string city, string province, string postalCode)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(street), "La dirección necesita una calle.");
        DomainException.Require(!string.IsNullOrWhiteSpace(city), "La dirección necesita una ciudad.");
        DomainException.Require(!string.IsNullOrWhiteSpace(province), "La dirección necesita una provincia.");
        DomainException.Require(!string.IsNullOrWhiteSpace(postalCode), "La dirección necesita un código postal.");

        Street = street.Trim();
        City = city.Trim();
        Province = province.Trim();
        PostalCode = postalCode.Trim();
    }

    public string Street { get; private set; } = null!;

    public string City { get; private set; } = null!;

    public string Province { get; private set; } = null!;

    public string PostalCode { get; private set; } = null!;

    /// <summary>Reserved for the mobile phase. Stored when known, but nothing reads it in v1.</summary>
    public double? Latitude { get; private set; }

    /// <summary>Reserved for the mobile phase. Stored when known, but nothing reads it in v1.</summary>
    public double? Longitude { get; private set; }

    public Address WithCoordinates(double latitude, double longitude)
    {
        DomainException.Require(latitude is >= -90 and <= 90, "Latitud fuera de rango.");
        DomainException.Require(longitude is >= -180 and <= 180, "Longitud fuera de rango.");

        return this with { Latitude = latitude, Longitude = longitude };
    }
}
