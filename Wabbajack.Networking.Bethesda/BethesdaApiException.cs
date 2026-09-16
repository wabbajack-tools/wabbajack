using System.Net;

namespace Wabbajack.Networking.Bethesda;

/// <summary>
///     A refusal from <c>api.bethesda.net</c>, carrying both what HTTP said and what the origin's own
///     envelope said, because the two answer different questions. The HTTP status alone cannot tell a
///     request blocked at the edge from one the origin considered and declined, and the platform code is
///     what distinguishes "your ticket is no good" (14029) from "your headers are" (5001).
/// </summary>
public class BethesdaApiException : Exception
{
    public BethesdaApiException(string message, HttpStatusCode status, int? platformCode = null,
        string? platformMessage = null) : base(message)
    {
        Status = status;
        PlatformCode = platformCode;
        PlatformMessage = platformMessage;
    }

    /// <summary>What HTTP said.</summary>
    public HttpStatusCode Status { get; }

    /// <summary>
    ///     The <c>platform.code</c> from the response envelope, when there was one. Null means the body was
    ///     not the origin's - an empty CloudFront 403, most likely, which is the edge refusing the
    ///     <c>x-bnet-key</c> before the origin ever sees the request.
    /// </summary>
    public int? PlatformCode { get; }

    /// <summary>The <c>platform.message</c> that came with <see cref="PlatformCode" />.</summary>
    public string? PlatformMessage { get; }

    /// <summary>Whether the request never reached the origin at all.</summary>
    public bool BlockedAtEdge => PlatformCode is null;
}
