namespace FastLazy.Tests;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Compiler",
    "CS1591:Missing XML comment for publicly visible type or member",
    Justification = "<Pending>"
)]
public sealed class FastLazyValueTests
{
    public FastLazyValue<long> Should_Initialize()
    {
        return new FastLazyValue<long>(() => 1);
    }

    public void IsValueCreated_Should_Return_Correct_Value()
    {
        var lazy = new FastLazyValue<long>(() => 1);
        lazy.IsValueCreated.Should().BeFalse();

        _ = lazy.Value;

        lazy.IsValueCreated.Should().BeTrue();
    }

    public void Value_Should_Return_Correct_Value()
    {
        var lazy = new FastLazyValue<long>(() => 1);

        var value = lazy.Value;

        value.Should().Be(1);
    }
}
