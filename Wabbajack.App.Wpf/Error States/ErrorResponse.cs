using System;
using System.Collections.Generic;
using System.Linq;

namespace Wabbajack
{
    public struct ErrorResponse : IErrorResponse
    {
        public static readonly ErrorResponse Success = Succeed();
        public static readonly ErrorResponse Failure = new();

        public bool Succeeded { get; }
        public Exception? Exception { get; }
        private readonly string _reason;

        public bool Failed => !Succeeded;
        public string Reason
        {
            get
            {
                if (Exception != null)
                {
                    if (string.IsNullOrWhiteSpace(_reason))
                    {
                        return Exception.ToString();
                    }
                    else
                    {
                        return $"{_reason}: {Exception.Message}";
                    }
                }
                return _reason;
            }
        }

        bool IErrorResponse.Succeeded => Succeeded;
        Exception? IErrorResponse.Exception => Exception;

        private ErrorResponse(
            bool succeeded,
            string? reason = null,
            Exception? ex = null)
        {
            Succeeded = succeeded;
            _reason = reason ?? string.Empty;
            Exception = ex;
        }

        public override string ToString()
        {
            return $"({(Succeeded ? "Success" : "Fail")}, {Reason})";
        }

        #region Factories
        public static ErrorResponse Succeed()
        {
            return new ErrorResponse(true);
        }

        public static ErrorResponse Succeed(string reason)
        {
            return new ErrorResponse(true, reason);
        }

        public static ErrorResponse Fail(string reason, Exception? ex = null)
        {
            return new ErrorResponse(false, reason: reason, ex: ex);
        }

        public static ErrorResponse Fail(Exception ex)
        {
            return new ErrorResponse(false, ex: ex);
        }

        public static ErrorResponse Fail()
        {
            return new ErrorResponse(false);
        }

        public static ErrorResponse Create(bool successful, string? reason = null)
        {
            return new ErrorResponse(successful, reason);
        }
        #endregion

        public static ErrorResponse Convert(IErrorResponse err, bool nullIsSuccess = true)
        {
            if (err == null) return Create(nullIsSuccess);
            return new ErrorResponse(err.Succeeded, err.Reason, err.Exception);
        }

        public static ErrorResponse FirstFail(params ErrorResponse[] responses)
        {
            foreach (var resp in responses)
            {
                if (resp.Failed) return resp;
            }
            return ErrorResponse.Success;
        }

        public static ErrorResponse Combine(List<ErrorResponse> errors)
        {
            if (errors.All(e => e.Succeeded) || !errors.Any())
                return Success;
            return Fail(string.Join("\n", errors.Where(e => e.Failed).Select(e => e.Reason)));
        }
    }

    public interface IErrorResponse
    {
        bool Succeeded { get; }
        Exception? Exception { get; }
        string Reason { get; }
    }
}
