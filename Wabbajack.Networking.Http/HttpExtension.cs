using System;
using System.Net.Http;

namespace Wabbajack.Networking.Http;

public class HttpException : Exception
{
    public HttpException(int code, string reason) : base($"Http Error {code} - {reason}")
    {
        Code = code;
        Reason = reason;
    }

    public HttpException(HttpResponseMessage response) : base(
        $"Http Error {response.StatusCode} - {response.ReasonPhrase}")
    {
        Code = (int) response.StatusCode;
        Reason = response.ReasonPhrase ?? "Unknown";
    }

    public string Reason { get; set; }
    public int Code { get; set; }

    public static void ThrowOnFailure(HttpResponseMessage result)
    {
        if (result.IsSuccessStatusCode) return;
        throw new HttpException(result);
    }
}