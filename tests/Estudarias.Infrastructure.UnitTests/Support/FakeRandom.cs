namespace Estudarias.Infrastructure.UnitTests.Support;

/// <summary>Devolve os valores combinados, na ordem, no lugar de sortear. Esgotados, falha: o teste sorteou mais do que devia.</summary>
internal sealed class ScriptedRandom(params int[] values) : Random
{
    private readonly Queue<int> _values = new(values);

    public override int Next(int maxValue) =>
        _values.Count > 0
            ? _values.Dequeue()
            : throw new InvalidOperationException("The random source was asked for more draws than the test scripted.");
}

/// <summary>Falha se for usado: prova que o código não sorteou.</summary>
internal sealed class ForbiddenRandom : Random
{
    public override int Next(int maxValue) =>
        throw new InvalidOperationException("The assignment must not draw when the flag is off.");
}
