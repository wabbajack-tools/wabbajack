using System;

namespace Wabbajack
{
    public struct GetResponse<T> : IEquatable<GetResponse<T>>, IValidationResult
    {
        public static readonly GetResponse<T> Failure = new GetResponse<T>();

        public readonly T Value;
        public readonly bool Succeeded;
        public readonly Exception? Exception;
        private readonly string _reason;

        public bool Failed => !Succeeded;
        public string Reason
        {
            get
            {
                if (Exception != null)
                {
                    return Exception.ToString();
                }
                return _reason;
            }
        }

        bool IValidationResult.Succeeded => Succeeded;
        Exception? IValidationResult.Exception => Exception;

        private GetResponse(
            bool succeeded,
            T? val = default,
            string? reason = null,
            Exception? ex = null)
        {
            Value = val!;
            Succeeded = succeeded;
            _reason = reason ?? string.Empty;
            Exception = ex;
        }

        public bool Equals(GetResponse<T> other)
        {
            return Succeeded == other.Succeeded
                && Equals(Value, other.Value);
        }

        public override bool Equals(object? obj)
        {
            if (!(obj is GetResponse<T> rhs)) return false;
            return Equals(rhs);
        }

        public override int GetHashCode()
        {
            System.HashCode hash = new HashCode();
            hash.Add(Value);
            hash.Add(Succeeded);
            return hash.ToHashCode();
        }

        public override string ToString()
        {
            return $"({(Succeeded ? "Success" : "Fail")}, {Value}, {Reason})";
        }

        public GetResponse<R> BubbleFailure<R>()
        {
            return new GetResponse<R>(
                succeeded: false,
                reason: _reason,
                ex: Exception);
        }

        public GetResponse<R> Bubble<R>(Func<T, R> conv)
        {
            return new GetResponse<R>(
                succeeded: Succeeded,
                val: conv(Value),
                reason: _reason,
                ex: Exception);
        }

        public T EvaluateOrThrow()
        {
            if (Succeeded)
            {
                return Value;
            }
            throw new ArgumentException(Reason);
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

        public static GetResponse<T> Create(bool successful, T? val = default(T), string? reason = null)
        {
            return new GetResponse<T>(successful, val!, reason);
        }
        #endregion
    }
}
