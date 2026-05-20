namespace Moongazing.OrionKey.Testing;

/// <summary>
/// Swaps OrionKey's process-wide generators for deterministic sequences for the lifetime of the
/// scope, then restores normal behaviour on <see cref="Dispose"/>. Tests that use this type must
/// not run in parallel with each other or with code that generates ids.
/// </summary>
public sealed class DeterministicIdScope : IDisposable
{
    /// <summary>Begins a deterministic-id scope.</summary>
    public DeterministicIdScope()
    {
        OrionKey.ResetForTesting();
        OrionKey.UseDeterministicGeneratorsForTesting();
    }

    /// <summary>Restores OrionKey to its default (non-deterministic) generators.</summary>
    public void Dispose() => OrionKey.ResetForTesting();
}
