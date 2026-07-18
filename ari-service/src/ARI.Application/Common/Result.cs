using System;

namespace ARISP.Application.Common
{
    public class Result
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public string Error { get; }

        protected Result(bool isSuccess, string error)
        {
            if (isSuccess && !string.IsNullOrEmpty(error))
                throw new InvalidOperationException("A successful result cannot have an error.");
            if (!isSuccess && string.IsNullOrEmpty(error))
                throw new InvalidOperationException("A failed result must have an error.");

            IsSuccess = isSuccess;
            Error = error;
        }

        public static Result Success() => new(true, string.Empty);
        public static Result Failure(string error) => new(false, error);

        public static Result<T> Success<T>(T value) => Result<T>.Success(value);
        public static Result<T> Failure<T>(string error) => Result<T>.Failure(error);
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

        private Result(bool isSuccess, string error, T? value) : base(isSuccess, error)
        {
            _value = value;
        }

        public static Result<T> Success(T value) => new(true, string.Empty, value);
        public static new Result<T> Failure(string error) => new(false, error, default);

        public static implicit operator Result<T>(T value) => Success(value);
    }
}
