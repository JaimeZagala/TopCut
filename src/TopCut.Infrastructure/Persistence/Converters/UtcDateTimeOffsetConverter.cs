using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TopCut.Infrastructure.Persistence.Converters;

/// <summary>
/// Hands every instant to PostgreSQL in UTC (ADR-0039).
/// <para>
/// <c>timestamptz</c> stores an instant, not an offset, and Npgsql refuses to write a
/// <see cref="DateTimeOffset"/> whose offset is not zero rather than silently pick one. The domain
/// is right to carry the store's offset — <c>LocalTime.ToInstant</c> builds "18:00 at +02:00" — and
/// the instant is the same either way, so the conversion happens here, once, instead of at every
/// call site that might hand one over.
/// </para>
/// Reading back yields offset zero. Nothing depends on the offset that was written: the screens
/// convert to the store's zone with <c>LocalTime</c>, and equality compares instants.
/// </summary>
internal sealed class UtcDateTimeOffsetConverter() : ValueConverter<DateTimeOffset, DateTimeOffset>(
    instant => instant.ToUniversalTime(),
    instant => instant);
