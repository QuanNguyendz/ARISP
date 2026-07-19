using ARI.Application.Common;
using ARI.Application.Common.Behaviours;
using FluentValidation;
using MediatR;
using Xunit;

namespace ARI.Application.UnitTests.Common;

/// <summary>
/// ValidationBehaviour là deviation có chủ đích so với template JT: khi validator fail và
/// response là Result/Result&lt;T&gt; thì short-circuit bằng Result.Failure (KHÔNG throw)
/// để controller trả 400 với body y hệt trước refactor.
/// </summary>
public class ValidationBehaviourTests
{
    private record TestCommand(string? Name) : IRequest<Result>;

    private record TestCommandWithValue(string? Name) : IRequest<Result<string>>;

    private class TestCommandValidator : AbstractValidator<TestCommand>
    {
        public TestCommandValidator()
        {
            RuleFor(x => x.Name).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Name là bắt buộc.");
        }
    }

    private class TestCommandWithValueValidator : AbstractValidator<TestCommandWithValue>
    {
        public TestCommandWithValueValidator()
        {
            RuleFor(x => x.Name).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Name là bắt buộc.");
        }
    }

    [Fact]
    public async Task Invalid_request_returns_failure_result_without_throwing()
    {
        var behaviour = new ValidationBehaviour<TestCommand, Result>(new[] { new TestCommandValidator() });

        var result = await behaviour.Handle(
            new TestCommand(null),
            _ => Task.FromResult(Result.Success()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Name là bắt buộc.", result.Error);
        Assert.Null(result.ErrorCode); // lỗi validation không có ErrorCode → controller map 400
    }

    [Fact]
    public async Task Invalid_request_with_generic_result_returns_typed_failure()
    {
        var behaviour = new ValidationBehaviour<TestCommandWithValue, Result<string>>(new[] { new TestCommandWithValueValidator() });

        var result = await behaviour.Handle(
            new TestCommandWithValue(""),
            _ => Task.FromResult(Result.Success("ok")),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Name là bắt buộc.", result.Error);
    }

    [Fact]
    public async Task Valid_request_invokes_next_handler()
    {
        var behaviour = new ValidationBehaviour<TestCommand, Result>(new[] { new TestCommandValidator() });

        var result = await behaviour.Handle(
            new TestCommand("Alice"),
            _ => Task.FromResult(Result.Success()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task No_validators_invokes_next_handler()
    {
        var behaviour = new ValidationBehaviour<TestCommand, Result>(Array.Empty<IValidator<TestCommand>>());

        var result = await behaviour.Handle(
            new TestCommand(null),
            _ => Task.FromResult(Result.Success()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }
}
