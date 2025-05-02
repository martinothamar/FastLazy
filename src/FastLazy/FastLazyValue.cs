using System.Runtime.CompilerServices;

namespace FastLazy;

/// <summary>
/// Provides support for lazy initialization by generator function/delegate.
/// Lock-free and threadsafe code using atomics.
/// </summary>
/// <typeparam name="T">struct type</typeparam>
/// <typeparam name="TArg">Argument type</typeparam>
public struct FastLazyValue<T, TArg>
    where T : struct
{
    internal const long INVALID = -1;
    internal const long UNITIALIZED = 0;
    internal const long INITIALIZING = 1;
    internal const long INITIALIZED = 2;
    internal const long CACHED = 3;

    internal readonly Func<TArg, T> _generator;
    internal long _state;
    internal T _value;
    internal TArg _arg;

    private unsafe ref T _valueRef => ref Unsafe.AsRef<T>(Unsafe.AsPointer(ref _value));

    /// <summary>
    /// Wether or not the value of T has been initialized
    /// </summary>
    public readonly bool IsValueCreated => _state == INITIALIZED;

    private unsafe ref T _valueRefAndAddress(out long address)
    {
        var ptr = Unsafe.AsPointer(ref _value);
        address = (long)ptr;
        return ref Unsafe.AsRef<T>(ptr);
    }

    /// <summary>
    /// Returns the value from the generator function, or the cached value.
    /// </summary>
    public T Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_state != INITIALIZED)
                return ValueSlow;

            return _value;
        }
    }

    internal T ValueSlow
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get
        {
            TryInit(out _);
            return _value;
        }
    }

    /// <summary>
    /// Returns the value from the generator function, or the cached value.
    /// This property returns by `ref readonly`, i.e. a pointer that cant be reassigned.
    /// This is currently not allowed by the language, and so you are responsible for
    /// avoiding use-after-free bugs - FastLazyValue may be deallocated while you hold on to the ref.
    /// </summary>
    public ref readonly T ValueRef
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_state != INITIALIZED)
                return ref ValueRefSlow;

            return ref _valueRef;
        }
    }

    internal ref T ValueRefSlow
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get
        {
            TryInit(out _);
            return ref _valueRef;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref T GetValueInstrumented(out long previousState, out long address)
    {
        if (_state != INITIALIZED)
            return ref GetValueInstrumentedSlow(out previousState, out address);

        previousState = CACHED;
        return ref _valueRefAndAddress(out address);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal ref T GetValueInstrumentedSlow(out long previousState, out long address)
    {
        TryInit(out previousState);
        return ref _valueRefAndAddress(out address);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void TryInit(out long previousState)
    {
        previousState = Interlocked.CompareExchange(ref _state, INITIALIZING, UNITIALIZED);
        switch (previousState)
        {
            case INITIALIZED:
                // Someone has already completed init
                return;
            case INITIALIZING:
                // Wait for someone else to complete
                var spinWait = default(SpinWait);
                while (Interlocked.Read(ref _state) < INITIALIZED)
                    spinWait.SpinOnce();
                return;
            case UNITIALIZED:
                _value = _generator(_arg);
                Interlocked.Exchange(ref _state, INITIALIZED);
                return;
        }
    }

    /// <summary>
    /// Constructs the lazy instance.
    /// </summary>
    /// <param name="generator">Generator function for the instance of T</param>
    /// <param name="arg">Argument to the generator function</param>
    public FastLazyValue(Func<TArg, T> generator, TArg arg)
    {
        _generator = generator;
        _state = UNITIALIZED;
        _value = default;
        _arg = arg;
    }

    /// <summary>
    /// Returns the string representation of the underlying T if it is initialized.
    /// </summary>
    /// <returns></returns>
    public override readonly string? ToString() => _state == INITIALIZED ? _value.ToString() : null;
}
