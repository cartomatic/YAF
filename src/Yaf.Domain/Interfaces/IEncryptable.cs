namespace Yaf.Domain.Interfaces;

/// <summary>
/// Marker interface for memento types containing properties that require encryption at rest.
/// Infrastructure encrypts/decrypts properties marked with
/// <see cref="Yaf.Domain.Attributes.EncryptAttribute"/> during persistence operations.
/// </summary>
/// <remarks>
/// <para>
/// Supported property types for encryption:
/// <see cref="string"/>, <see cref="T:string[]"/>,
/// <see cref="T:System.Collections.Generic.List{string}"/>,
/// and <see cref="T:byte[]"/>.
/// </para>
/// <para>
/// Mementos implementing this interface signal to infrastructure that they contain
/// sensitive data requiring encryption. The actual encryption/decryption is performed
/// by an <c>IEncryptionProvider</c> (defined in infrastructure).
/// </para>
/// </remarks>
public interface IEncryptable;
