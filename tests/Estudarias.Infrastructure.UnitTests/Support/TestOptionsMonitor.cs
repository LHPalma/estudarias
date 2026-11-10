using Microsoft.Extensions.Options;

namespace Estudarias.Infrastructure.UnitTests.Support;

/// <summary>Um <see cref="IOptionsMonitor{TOptions}"/> cujo valor o teste troca na hora, como uma configuração que recarrega.</summary>
internal sealed class TestOptionsMonitor<T>(T initial) : IOptionsMonitor<T>
{
    public T CurrentValue { get; set; } = initial;

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
