using System;

namespace ARI.Application.Common
{
    public class Result
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public string Error { get; }

        /// <summary>
        /// Mã lỗi tùy chọn (machine-readable) để controller map failure → đúng HTTP status
        /// như trước refactor CQRS (401 vs 403 vs 404...). Null = lỗi validation/mặc định (400).
        /// Bổ sung additive — không đổi semantics Error/IsFailure sẵn có.
        /// </summary>
        public string? ErrorCode { get; }

        protected Result(bool isSuccess, string error, string? errorCode = null)
        {
            if (isSuccess && !string.IsNullOrEmpty(error))
                throw new InvalidOperationException("A successful result cannot have an error.");
            if (!isSuccess && string.IsNullOrEmpty(error))
                throw new InvalidOperationException("A failed result must have an error.");

            IsSuccess = isSuccess;
            Error = error;
            ErrorCode = errorCode;
        }

        public static Result Success() => new(true, string.Empty);
        public static Result Failure(string error) => new(false, error);
        public static Result Failure(string error, string errorCode) => new(false, error, errorCode);

        public static Result<T> Success<T>(T value) => Result<T>.Success(value);
        public static Result<T> Failure<T>(string error) => Result<T>.Failure(error);
        public static Result<T> Failure<T>(string error, string errorCode) => Result<T>.Failure(error, errorCode);
    }

    public class Result<T> : Result
    {
        private readonly T? _value;

        public T Value
        {
            get
            {
                if (IsFailure)
                    throw new InvalidOperationException("Cannot access the value of a failed result.");
                return _value!;
            }
        }

        private Result(bool isSuccess, string error, T? value, string? errorCode = null) : base(isSuccess, error, errorCode)
        {
            _value = value;
        }

        public static Result<T> Success(T value) => new(true, string.Empty, value);
        public static new Result<T> Failure(string error) => new(false, error, default);
        public static new Result<T> Failure(string error, string errorCode) => new(false, error, default, errorCode);

        public static implicit operator Result<T>(T value) => Success(value);
    }
}
