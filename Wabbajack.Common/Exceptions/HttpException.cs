using System;
using System.Net.Http;

namespace Wabbajack.Common.Exceptions
{
    public class HttpException : Exception
    {
        public string Reason { get; set; }
        public int Code { get; set; }

        public HttpException(int code, string reason) : base($"Http Error {code} - {reason}")
        {
            Code = code;
            Reason = reason;
        }

        public HttpException(HttpResponseMessage response) : base(
            $"Http Error {response.StatusCode} - {response.ReasonPhrase}")
        {
            Code = (int)response.StatusCode;
            Reason = response.ReasonPhrase;
        }
    }
}
