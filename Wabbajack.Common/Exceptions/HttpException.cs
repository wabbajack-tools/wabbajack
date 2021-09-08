using System;
using System.Net;
using System.Net.Http;
using Wabbajack.Common.Serialization.Json;

namespace Wabbajack.Common.Exceptions
{
    [JsonName("HttpException")]
    public class HttpException : Exception
    {
        public string Reason { get; set; }
        public HttpStatusCode Code { get; set; }

        public HttpException(int code, string reason) : base($"Http Error {code} - {reason}")
        {
            Code = (HttpStatusCode)code;
            Reason = reason;
        }

        public HttpException(HttpResponseMessage response) : base(
            $"Http Error {response.StatusCode} - {response.ReasonPhrase}")
        {
            Code = response.StatusCode;
            Reason = response.ReasonPhrase ?? "Unknown";
        }
    }
}
