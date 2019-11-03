using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack
{
    public struct ErrorResponse : IErrorResponse
    {
        public readonly static ErrorResponse Success = Succeed();
        public readonly static ErrorResponse Failure = new ErrorResponse();

        public readonly bool Succeeded;
        public readonly Exception Exception;
        private readonly string _reason;

        public bool Failed => !Succeeded;
        public string Reason
        {
            get
            {
                if (this.Exception != null)
                {
                    return this.Exception.ToString();
                }
                return _reason;
            }
        }

        bool IErrorResponse.Succeeded => this.Succeeded;
        Exception IErrorResponse.Exception => this.Exception;

        private ErrorResponse(
            bool succeeded,
            string reason = null,
            Exception ex = null)
        {
            this.Succeeded = succeeded;
            this._reason = reason;
            this.Exception = ex;
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

        public static ErrorResponse Fail(string reason)
        {
            return new ErrorResponse(false, reason: reason);
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
    }

    public interface IErrorResponse
    {
        bool Succeeded { get; }
        Exception Exception { get; }
        string Reason { get; }
    }
}
