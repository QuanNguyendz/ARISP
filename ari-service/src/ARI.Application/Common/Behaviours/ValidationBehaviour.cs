using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;

namespace ARI.Application.Common.Behaviours
{
    /// <summary>
    /// Chạy mọi <see cref="IValidator{TRequest}"/> của request trước handler.
    /// Khác template JT (throw ValidationException): dự án dùng Result Pattern (rule CLAUDE.md) —
    /// khi TResponse là <see cref="Result"/>/<see cref="Result{T}"/> thì short-circuit bằng
    /// <c>Result.Failure(lỗi đầu tiên)</c> để controller map ra 400 với body y hệt các chuỗi
    /// validate inline trước refactor. Response không phải Result mới throw.
    /// </summary>
    public class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        // Factory tạo Result.Failure<T> cache theo closed generic type (tránh reflection mỗi request).
        private static readonly Func<string, TResponse>? FailureFactory = BuildFailureFactory();

        private readonly IEnumerable<IValidator<TRequest>> _validators;

        public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (!_validators.Any())
                return await next();

            var context = new ValidationContext<TRequest>(request);
            var results = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken)));
            var failures = results.SelectMany(r => r.Errors).Where(f => f != null).ToList();

            if (failures.Count == 0)
                return await next();

            if (FailureFactory != null)
                return FailureFactory(failures[0].ErrorMessage);

            throw new ValidationException(failures);
        }

        private static Func<string, TResponse>? BuildFailureFactory()
        {
            var responseType = typeof(TResponse);

            if (responseType == typeof(Common.Result))
                return error => (TResponse)(object)Common.Result.Failure(error);

            if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Common.Result<>))
            {
                var method = responseType.GetMethod(
                    nameof(Common.Result<object>.Failure),
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(string) },
                    modifiers: null);
                if (method != null)
                    return error => (TResponse)method.Invoke(null, new object[] { error })!;
            }

            return null;
        }
    }
}
