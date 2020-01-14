using System;

namespace Wabbajack
{
    public struct ErrorResponse : IErrorResponse
    {
        public readonly static ErrorResponse Success = Succeed();
        public readonly static ErrorResponse Failure = new ErrorResponse();

        public bool Succeeded { get; }
        public Exception Exception { get; }
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
        Exception IErrorResponse.Exception => Exception;

        private ErrorResponse(
            bool succeeded,
            string reason = null,
            Exception ex = null)
        {
            Succeeded = succeeded;
            _reason = reason;
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

        public static ErrorResponse Fail(string reason, Exception ex = null)
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

        public static ErrorResponse Create(bool successful, string reason = null)
        {
            return new ErrorResponse(successful, reason);
        }
        #endregion

        public static ErrorResponse Convert(IErrorResponse err, bool nullIsSuccess = true)
        {
            if (err == null) return Create(nullIsSuccess);
            return new ErrorResponse(err.Succeeded, err.Reason, err.Exception);
        }
    }

    public interface IErrorResponse
    {
        bool Succeeded { get; }
        Exception Exception { get; }
        string Reason { get; }
    }
}
