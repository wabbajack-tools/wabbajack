using System;
using System.Collections.Generic;
using System.Linq;

namespace Wabbajack
{
    public class ValidationResult : IValidationResult
    {
        public static readonly ValidationResult Success = Succeed();
        public static readonly ValidationResult Failure = new();

        public bool Succeeded { get; }
        public Exception? Exception { get; }
        protected readonly string _reason;

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

        bool IValidationResult.Succeeded => Succeeded;
        Exception? IValidationResult.Exception => Exception;

        protected ValidationResult() { }
        protected ValidationResult(
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
        public static ValidationResult Succeed()
        {
            return new ValidationResult(true);
        }

        public static ValidationResult Succeed(string reason)
        {
            return new ValidationResult(true, reason);
        }

        public static ValidationResult Fail(string reason, Exception? ex = null)
        {
            return new ValidationResult(false, reason: reason, ex: ex);
        }

        public static ValidationResult Fail(Exception ex)
        {
            return new ValidationResult(false, ex: ex);
        }

        public static ValidationResult Fail()
        {
            return new ValidationResult(false);
        }

        public static ValidationResult Create(bool successful, string? reason = null)
        {
            return new ValidationResult(successful, reason);
        }
        #endregion

        public static ValidationResult Convert(IValidationResult err, bool nullIsSuccess = true)
        {
            if (err == null) return Create(nullIsSuccess);
            return new ValidationResult(err.Succeeded, err.Reason, err.Exception);
        }

        public static ValidationResult FirstFail(params ValidationResult[] responses)
        {
            foreach (var resp in responses)
            {
                if (resp.Failed) return resp;
            }
            return ValidationResult.Success;
        }

        public static ValidationResult Combine(List<ValidationResult> errors)
        {
            if (errors.All(e => e.Succeeded) || !errors.Any())
                return Success;
            return Fail(string.Join("\n", errors.Where(e => e.Failed).Select(e => e.Reason)));
        }
    }

    public interface IValidationResult
    {
        bool Succeeded { get; }
        Exception? Exception { get; }
        string Reason { get; }
    }
}
