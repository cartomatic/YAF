namespace Yaf.Domain.Interfaces;

/// <summary>
/// Marker interface for types that declare <see cref="Error"/> properties or fields.
/// Infrastructure scans implementations to build an error catalog for
/// documentation, translations, and API error endpoints.
/// </summary>
public interface IErrorSource;
