namespace Imazen.Abstractions.HttpStrings;

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


/// <summary>
/// Contains methods to verify the request protocol version of an HTTP request.
/// </summary>
public static class HttpProtocolStrings
{
    // We are intentionally using 'static readonly' here instead of 'const'.
    // 'const' values would be embedded into each assembly that used them
    // and each consuming assembly would have a different 'string' instance.
    // Using .'static readonly' means that all consumers get these exact same
    // 'string' instance, which means the 'ReferenceEquals' checks below work
    // and allow us to optimize comparisons when these constants are used.

    // Please do NOT change these to 'const'

    /// <summary>
    ///  HTTP protocol version 0.9.
    /// </summary>
    public static readonly string Http09 = "HTTP/0.9";

    /// <summary>
    ///  HTTP protocol version 1.0.
    /// </summary>
    public static readonly string Http10 = "HTTP/1.0";

    /// <summary>
    ///  HTTP protocol version 1.1.
    /// </summary>
    public static readonly string Http11 = "HTTP/1.1";

    /// <summary>
    ///  HTTP protocol version 2.
    /// </summary>
    public static readonly string Http2 = "HTTP/2";

    /// <summary>
    ///  HTTP protcol version 3.
    /// </summary>
    public static readonly string Http3 = "HTTP/3";

    /// <summary>
    /// Returns a value that indicates if the HTTP request protocol is HTTP/0.9.
    /// </summary>
    /// <param name="protocol">The HTTP request protocol.</param>
    /// <returns>
    /// <see langword="true" /> if the protocol is HTTP/0.9; otherwise, <see langword="false" />.
    /// </returns>
    public static bool IsHttp09(string protocol)
    {
        return object.ReferenceEquals(Http09, protocol) || StringComparer.OrdinalIgnoreCase.Equals(Http09, protocol);
    }

    /// <summary>
    /// Returns a value that indicates if the HTTP request protocol is HTTP/1.0.
    /// </summary>
    /// <param name="protocol">The HTTP request protocol.</param>
    /// <returns>
    /// <see langword="true" /> if the protocol is HTTP/1.0; otherwise, <see langword="false" />.
    /// </returns>
    public static bool IsHttp10(string protocol)
    {
        return object.ReferenceEquals(Http10, protocol) || StringComparer.OrdinalIgnoreCase.Equals(Http10, protocol);
    }

    /// <summary>
    /// Returns a value that indicates if the HTTP request protocol is HTTP/1.1.
    /// </summary>
    /// <param name="protocol">The HTTP request protocol.</param>
    /// <returns>
    /// <see langword="true" /> if the protocol is HTTP/1.1; otherwise, <see langword="false" />.
    /// </returns>
    public static bool IsHttp11(string protocol)
    {
        return object.ReferenceEquals(Http11, protocol) || StringComparer.OrdinalIgnoreCase.Equals(Http11, protocol);
    }

    /// <summary>
    /// Returns a value that indicates if the HTTP request protocol is HTTP/2.
    /// </summary>
    /// <param name="protocol">The HTTP request protocol.</param>
    /// <returns>
    /// <see langword="true" /> if the protocol is HTTP/2; otherwise, <see langword="false" />.
    /// </returns>
    public static bool IsHttp2(string protocol)
    {
        return object.ReferenceEquals(Http2, protocol) || StringComparer.OrdinalIgnoreCase.Equals(Http2, protocol);
    }

    /// <summary>
    /// Returns a value that indicates if the HTTP request protocol is HTTP/3.
    /// </summary>
    /// <param name="protocol">The HTTP request protocol.</param>
    /// <returns>
    /// <see langword="true" /> if the protocol is HTTP/3; otherwise, <see langword="false" />.
    /// </returns>
    public static bool IsHttp3(string protocol)
    {
        return object.ReferenceEquals(Http3, protocol) || StringComparer.OrdinalIgnoreCase.Equals(Http3, protocol);
    }

    /// <summary>
    /// Gets the HTTP request protocol for the specified <see cref="Version"/>.
    /// </summary>
    /// <param name="version">The version.</param>
    /// <returns>A HTTP request protocol.</returns>
    public static string GetHttpProtocol(Version version)
    {
        ArgumentNullThrowHelper.ThrowIfNull(version);

        return version switch
        {
            { Major: 3, Minor: 0 } => Http3,
            { Major: 2, Minor: 0 } => Http2,
            { Major: 1, Minor: 1 } => Http11,
            { Major: 1, Minor: 0 } => Http10,
            { Major: 0, Minor: 9 } => Http09,
            _ => throw new ArgumentOutOfRangeException(nameof(version), "Version doesn't map to a known HTTP protocol.")
        };
    }
}