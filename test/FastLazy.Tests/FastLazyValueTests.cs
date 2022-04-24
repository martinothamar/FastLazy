using System.Runtime.CompilerServices;

namespace FastLazy.Tests;

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

    public void ValueRef_Should_Return_Correct_Value()
    {
        var lazy = new FastLazyValue<long>(() => 1);

        ref readonly var value = ref lazy.ValueRef;

        value.Should().Be(1);
    }

    unsafe public void ValueRef_Should_Be_Same_Reference()
    {
        var lazy = new FastLazyValue<long>(() => 1);

        ref var value1 = ref Unsafe.AsRef(in lazy.ValueRef);
        ref var value2 = ref Unsafe.AsRef(in lazy.ValueRef);

        Unsafe.AreSame(ref value1, ref value2).Should().BeTrue();
    }
}
