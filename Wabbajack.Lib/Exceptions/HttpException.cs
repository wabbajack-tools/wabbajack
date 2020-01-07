using System;

namespace Wabbajack.Lib.Exceptions
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

    }
}
