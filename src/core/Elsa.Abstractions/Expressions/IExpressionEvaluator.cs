using System;
using System.Threading;
using System.Threading.Tasks;
using Elsa.Services.Models;

namespace Elsa.Expressions
{
    public interface IExpressionEvaluator
    {
        Task<object?> EvaluateAsync(string? expression, string syntax, Type returnType, ActivityExecutionContext context, CancellationToken cancellationToken = default);
        Task<T> EvaluateAsync<T>(string? expression, string syntax, ActivityExecutionContext context, CancellationToken cancellationToken = default);
    }
}