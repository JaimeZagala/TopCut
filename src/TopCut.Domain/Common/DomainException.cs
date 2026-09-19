namespace TopCut.Domain.Common;

/// <summary>
/// Thrown when an operation would break a domain invariant. Presentation translates it into a
/// 400/409 <c>problem+json</c>; it must never reach the user as a 500.
/// <para>
/// Messages are written in Spanish on purpose: the product is Spanish-only in v1 and these strings
/// can surface to the organiser. They move to <c>.resx</c> resources in milestone 3 (ADR-0008).
/// </para>
/// </summary>
public sealed class DomainException(string message) : Exception(message)
{
    /// <summary>Guard clause: throws when <paramref name="condition"/> does not hold.</summary>
    public static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new DomainException(message);
        }
    }
}
