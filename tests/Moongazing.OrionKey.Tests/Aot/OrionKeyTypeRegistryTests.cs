namespace Moongazing.OrionKey.Tests.Aot;

using System.Reflection;
using Moongazing.OrionKey.Aot;
using Xunit;

[Collection("OrionKeyTypeRegistry")]
public sealed class OrionKeyTypeRegistryTests : IDisposable
{
    public OrionKeyTypeRegistryTests() => Reset();
    public void Dispose() => Reset();

    private static void Reset()
    {
        // Reset internal state between facts to avoid registration leakage.
        var resetMethod = typeof(OrionKeyTypeRegistry)
            .GetMethod("ResetForTests", BindingFlags.NonPublic | BindingFlags.Static);
        resetMethod!.Invoke(null, null);
    }

    private readonly record struct ShortId(string Value)
    {
        public static ShortId Parse(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                throw new FormatException("ShortId.Parse: empty input.");
            }
            return new ShortId(s);
        }
        public override string ToString() => Value;
    }

    private readonly record struct LongId(long Value)
    {
        public static LongId Parse(string s) => new(long.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public void Register_returns_true_on_first_call_false_on_subsequent_calls()
    {
        Assert.True(OrionKeyTypeRegistry.Register<ShortId>(ShortId.Parse, id => id.ToString()));
        Assert.False(OrionKeyTypeRegistry.Register<ShortId>(ShortId.Parse, id => id.ToString()));
    }

    [Fact]
    public void TryParse_dispatches_via_registered_delegate()
    {
        OrionKeyTypeRegistry.Register<ShortId>(ShortId.Parse, id => id.ToString());

        var ok = OrionKeyTypeRegistry.TryParse(typeof(ShortId), "abc", out var id);

        Assert.True(ok);
        Assert.IsType<ShortId>(id);
        Assert.Equal("abc", ((ShortId)id!).Value);
    }

    [Fact]
    public void TryParse_returns_false_for_unregistered_type()
    {
        var ok = OrionKeyTypeRegistry.TryParse(typeof(ShortId), "abc", out var id);

        Assert.False(ok);
        Assert.Null(id);
    }

    [Fact]
    public void TryParse_returns_false_on_FormatException_from_delegate()
    {
        OrionKeyTypeRegistry.Register<ShortId>(ShortId.Parse, id => id.ToString());

        var ok = OrionKeyTypeRegistry.TryParse(typeof(ShortId), string.Empty, out var id);

        Assert.False(ok);
        Assert.Null(id);
    }

    [Fact]
    public void TryParse_returns_false_on_OverflowException_from_delegate()
    {
        OrionKeyTypeRegistry.Register<LongId>(LongId.Parse, id => id.ToString());

        var ok = OrionKeyTypeRegistry.TryParse(typeof(LongId), "999999999999999999999", out var id);

        Assert.False(ok);
        Assert.Null(id);
    }

    [Fact]
    public void Format_dispatches_via_registered_delegate()
    {
        OrionKeyTypeRegistry.Register<LongId>(LongId.Parse, id => id.ToString());

        var s = OrionKeyTypeRegistry.Format(typeof(LongId), new LongId(42));

        Assert.Equal("42", s);
    }

    [Fact]
    public void Format_throws_when_instance_type_mismatch()
    {
        OrionKeyTypeRegistry.Register<LongId>(LongId.Parse, id => id.ToString());

        Assert.Throws<ArgumentException>(
            () => OrionKeyTypeRegistry.Format(typeof(LongId), new ShortId("nope")));
    }

    [Fact]
    public void Format_throws_when_type_not_registered()
    {
        Assert.Throws<InvalidOperationException>(
            () => OrionKeyTypeRegistry.Format(typeof(ShortId), new ShortId("x")));
    }

    [Fact]
    public void IsRegistered_returns_true_after_Register()
    {
        Assert.False(OrionKeyTypeRegistry.IsRegistered(typeof(ShortId)));
        OrionKeyTypeRegistry.Register<ShortId>(ShortId.Parse, id => id.ToString());
        Assert.True(OrionKeyTypeRegistry.IsRegistered(typeof(ShortId)));
    }

    [Fact]
    public void RegisteredTypes_returns_snapshot_of_registered_keys()
    {
        OrionKeyTypeRegistry.Register<ShortId>(ShortId.Parse, id => id.ToString());
        OrionKeyTypeRegistry.Register<LongId>(LongId.Parse, id => id.ToString());

        var types = OrionKeyTypeRegistry.RegisteredTypes();

        Assert.Contains(typeof(ShortId), types);
        Assert.Contains(typeof(LongId), types);
        Assert.Equal(2, types.Length);
    }

    [Fact]
    public void Register_rejects_null_delegates()
    {
        Assert.Throws<ArgumentNullException>(
            () => OrionKeyTypeRegistry.Register<ShortId>(null!, id => id.ToString()));
        Assert.Throws<ArgumentNullException>(
            () => OrionKeyTypeRegistry.Register<ShortId>(ShortId.Parse, null!));
    }

    [Fact]
    public void TryParse_rejects_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(
            () => OrionKeyTypeRegistry.TryParse(null!, "x", out _));
        Assert.Throws<ArgumentNullException>(
            () => OrionKeyTypeRegistry.TryParse(typeof(ShortId), null!, out _));
    }
}
