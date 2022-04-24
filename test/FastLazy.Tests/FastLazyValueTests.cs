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

    public sealed record SmokeTest
    {
        public FastLazyValue<long> Lazy = new FastLazyValue<long>(() => 1);
        public TaskCompletionSource Start = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
    };

    unsafe public void Smoke_Test_Initialization()
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
                    threads[i] = Task.Factory
                        .StartNew(
                            s => Thread(s),
                            test,
                            default,
                            TaskCreationOptions.DenyChildAttach,
                            TaskScheduler.Default
                        )
                        .Unwrap();

                test.Start.SetResult();
                var results = Task.WhenAll(threads).GetAwaiter().GetResult();
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
                    result.PreviousState.Should().NotBe(FastLazyValue<long>.INVALID);
                    result.Value.Should().Be(1);
                    result.Address.Should().Be(address);

                    switch (result.PreviousState)
                    {
                        case FastLazyValue<long>.UNITIALIZED:
                            wasUnitializedCount++;
                            break;
                        case FastLazyValue<long>.INITIALIZING:
                            wasInitializingCount++;
                            break;
                        case FastLazyValue<long>.INITIALIZED:
                            wasInitializedCount++;
                            break;
                        case FastLazyValue<long>.CACHED:
                            wasCachedCount++;
                            break;
                        default:
                            throw new Exception();
                    }
                }

                wasUnitializedCount.Should().Be(1);
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
