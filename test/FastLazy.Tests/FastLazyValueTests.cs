using System.Runtime.CompilerServices;
using Xunit;

namespace FastLazy.Tests;

public class FastLazyValueTests
{
    [Fact]
    public void Should_Initialize()
    {
        _ = new FastLazyValue<long, Guid>(_ => 1, default);
    }

    [Fact]
    public void IsValueCreated_Should_Return_Correct_Value()
    {
        var lazy = new FastLazyValue<long, Guid>(_ => 1, default);
        Assert.False(lazy.IsValueCreated);

        _ = lazy.Value;

        Assert.True(lazy.IsValueCreated);
    }

    [Fact]
    public void Value_Should_Return_Correct_Value()
    {
        var lazy = new FastLazyValue<long, Guid>(_ => 1, default);

        var value = lazy.Value;

        Assert.Equal(1, value);
    }

    [Fact]
    public void ToString_Should_Return_Correct_Value()
    {
        var lazy = new FastLazyValue<long, Guid>(_ => 1, default);

        var str = lazy.ToString();
        Assert.Null(str);

        _ = lazy.Value;
        str = lazy.ToString();

        Assert.Equal("1", str);
    }

    [Fact]
    public void Passes_Arg()
    {
        var expectedArg = Guid.NewGuid();
        var lazy = new FastLazyValue<long, Guid>(
            arg =>
            {
                Assert.Equal(expectedArg, arg);
                return 1;
            },
            expectedArg
        );

        var value = lazy.Value;

        Assert.Equal(1, value);
    }

    [Fact]
    public void ValueRef_Should_Return_Correct_Value()
    {
        var lazy = new FastLazyValue<long, Guid>(_ => 1, default);

        ref readonly var value = ref lazy.ValueRef;

        Assert.Equal(1, value);
    }

    [Fact]
    public unsafe void ValueRef_Should_Be_Same_Reference()
    {
        var lazy = new FastLazyValue<long, Guid>(_ => 1, default);

        ref var value1 = ref Unsafe.AsRef(in lazy.ValueRef);
        ref var value2 = ref Unsafe.AsRef(in lazy.ValueRef);

        Assert.True(Unsafe.AreSame(ref value1, ref value2));
    }

    public sealed record SmokeTest
    {
        public FastLazyValue<long, Guid> Lazy = new FastLazyValue<long, Guid>(_ => 1, default);
        public TaskCompletionSource Start = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
    };

    [Fact]
    public unsafe void Smoke_Test_Initialization()
    {
        var concurrency = Environment.ProcessorCount;
        var threads = new Task<(long Value, long Address, long PreviousState)>[concurrency];

        const int maxIterations = 100_000;

        Console.WriteLine(
            $"Starting smoketests - "
                + $"{nameof(concurrency)}={concurrency}"
                + $", {nameof(maxIterations)}={maxIterations}"
        );

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var test = new SmokeTest();
            // Pin test object to make sure we get stable addresses
            // If object moves around in memory, so will the address of the value
            fixed (byte* data = &Pin.GetRawObjectData(test))
            {
                for (int i = 0; i < concurrency; i++)
                    threads[i] = Task
                        .Factory.StartNew(
                            s => Thread(s),
                            test,
                            default,
                            TaskCreationOptions.DenyChildAttach,
                            TaskScheduler.Default
                        )
                        .Unwrap();

                test.Start.SetResult();
#pragma warning disable xUnit1031 // Do not use blocking task operations in test method
                var results = Task.WhenAll(threads).GetAwaiter().GetResult();
#pragma warning restore xUnit1031 // Do not use blocking task operations in test method
                // We need to pin the SmokeTest object to get stable results for value addresses.
                // which means we have to use unsafe context, which means we cant use await.

                ProcessResults(results);
            }

            static void ProcessResults(
                ReadOnlySpan<(long Value, long Address, long PreviousState)> results
            )
            {
                var wasUnitializedCount = 0;
                var wasInitializingCount = 0;
                var wasInitializedCount = 0;
                var wasCachedCount = 0;

                var address = results[0].Address;

                foreach (ref readonly var result in results)
                {
                    Assert.NotEqual(FastLazyValue<long, Guid>.INVALID, result.PreviousState);
                    Assert.Equal(1, result.Value);
                    Assert.Equal(address, result.Address);

                    switch (result.PreviousState)
                    {
                        case FastLazyValue<long, Guid>.UNITIALIZED:
                            wasUnitializedCount++;
                            break;
                        case FastLazyValue<long, Guid>.INITIALIZING:
                            wasInitializingCount++;
                            break;
                        case FastLazyValue<long, Guid>.INITIALIZED:
                            wasInitializedCount++;
                            break;
                        case FastLazyValue<long, Guid>.CACHED:
                            wasCachedCount++;
                            break;
                        default:
                            throw new Exception();
                    }
                }

                Assert.Equal(1, wasUnitializedCount);
            }
        }

        Console.WriteLine("------------------");
        Console.WriteLine(
            $"Done smoketesting! - "
                + $"{nameof(concurrency)}={concurrency}, {nameof(maxIterations)}={maxIterations}"
        );
    }

    static async Task<(long Value, long Address, long PreviousState)> Thread(object? state)
    {
        var test = (SmokeTest)state!;
        await test.Start.Task;

        return CreateValue(test);
    }

    static (long Value, long Address, long PreviousState) CreateValue(SmokeTest test)
    {
        ref var value = ref test.Lazy.GetValueInstrumented(out var previouState, out var address);

        return (value, address, previouState);
    }
}
