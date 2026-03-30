namespace Yaf.Domain.Attributes;

/// <summary>
/// Marks a memento property for transparent encryption at rest.
/// Infrastructure encrypts the value before persistence and decrypts after retrieval.
/// </summary>
/// <remarks>
/// <para>Valid on properties of type:</para>
/// <list type="bullet">
///   <item><description><see cref="string"/></description></item>
///   <item><description><see cref="T:string[]"/></description></item>
///   <item><description><see cref="T:System.Collections.Generic.List{string}"/></description></item>
///   <item><description><see cref="T:byte[]"/></description></item>
/// </list>
/// <para>
/// The containing memento class must implement <see cref="Yaf.Domain.Interfaces.IEncryptable"/>.
/// Applying this attribute to unsupported types will cause a runtime exception
/// during infrastructure initialization.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class EncryptAttribute : Attribute;
