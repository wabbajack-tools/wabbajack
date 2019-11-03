using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wabbajack
{
    public struct GetResponse<T> : IEquatable<GetResponse<T>>, IErrorResponse
    {
        public static readonly GetResponse<T> Failure = new GetResponse<T>();

        public readonly T Value;
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

        private GetResponse(
            bool succeeded,
            T val = default(T),
            string reason = null,
            Exception ex = null)
        {
            this.Value = val;
            this.Succeeded = succeeded;
            this._reason = reason;
            this.Exception = ex;
        }

        public bool Equals(GetResponse<T> other)
        {
            return this.Succeeded == other.Succeeded
                && object.Equals(this.Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is GetResponse<T> rhs)) return false;
            return Equals(rhs);
        }

        public override int GetHashCode()
        {
            return HashHelper.GetHashCode(Value)
                .CombineHashCode(Succeeded.GetHashCode());
        }

        public override string ToString()
        {
            return $"({(Succeeded ? "Success" : "Fail")}, {Value}, {Reason})";
        }

        public GetResponse<R> BubbleFailure<R>()
        {
            return new GetResponse<R>(
                succeeded: false,
                reason: this._reason,
                ex: this.Exception);
        }

        public GetResponse<R> Bubble<R>(Func<T, R> conv)
        {
            return new GetResponse<R>(
                succeeded: this.Succeeded,
                val: conv(this.Value),
                reason: this._reason,
                ex: this.Exception);
        }

        public T EvaluateOrThrow()
        {
            if (this.Succeeded)
            {
                return this.Value;
            }
            throw new ArgumentException(this.Reason);
        }

        #region Factories
        public static GetResponse<T> Succeed(T value)
        {
            return new GetResponse<T>(true, value);
        }

        public static GetResponse<T> Succeed(T value, string reason)
        {
            return new GetResponse<T>(true, value, reason);
        }

        public static GetResponse<T> Fail(string reason)
        {
            return new GetResponse<T>(false, reason: reason);
        }

        public static GetResponse<T> Fail(T val, string reason)
        {
            return new GetResponse<T>(false, val, reason);
        }

        public static GetResponse<T> Fail(Exception ex)
        {
            return new GetResponse<T>(false, ex: ex);
        }

        public static GetResponse<T> Fail(T val, Exception ex)
        {
            return new GetResponse<T>(false, val, ex: ex);
        }

        public static GetResponse<T> Fail(T val)
        {
            return new GetResponse<T>(false, val);
        }

        public static GetResponse<T> Create(bool successful, T val = default(T), string reason = null)
        {
            return new GetResponse<T>(successful, val, reason);
        }
        #endregion
    }
}
