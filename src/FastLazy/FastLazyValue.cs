using System.Runtime.CompilerServices;

namespace FastLazy;

/// <summary>
/// Provides support for lazy initialization by generator function/delegate.
/// Lock-free and threadsafe code using atomics.
/// </summary>
/// <typeparam name="T">struct type</typeparam>
public struct FastLazyValue<T> where T : struct
{
    private const long UNINIT = 0;
    private const long INITING = 1;
    private const long INITD = 2;

    private readonly Func<T> _generator;
    private long _state;
    private T _value;

    /// <summary>
    /// Wether or not the value of T has been initialized
    /// </summary>
    public readonly bool IsValueCreated => _state == INITD;

    unsafe private ref readonly T _valueRef => ref Unsafe.AsRef<T>(Unsafe.AsPointer(ref _value));

    /// <summary>
    /// Returns the value from the generator function, or the cached value.
    /// </summary>
    public T Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_state != INITD)
                return ValueSlow;

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
            if (_state != INITD)
                return ref ValueRefSlow;

            return ref _valueRef;
        }
    }

    private T ValueSlow
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get
        {
            var prevState = Interlocked.CompareExchange(ref _state, INITING, UNINIT);
            switch (prevState)
            {
                case INITD:
                    // Someone has already completed init
                    return _value;
                case INITING:
                    // Wait for someone else to complete
                    var spinWait = default(SpinWait);
                    while (Interlocked.Read(ref _state) < INITD)
                        spinWait.SpinOnce();
                    return _value;
                case UNINIT:
                    _value = _generator();
                    Interlocked.Exchange(ref _state, INITD);
                    return _value;
            }

            return _value;
        }
    }

    private ref readonly T ValueRefSlow
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get
        {
            var prevState = Interlocked.CompareExchange(ref _state, INITING, UNINIT);
            switch (prevState)
            {
                case INITD:
                    // Someone has already completed init
                    return ref _valueRef;
                case INITING:
                    // Wait for someone else to complete
                    var spinWait = default(SpinWait);
                    while (Interlocked.Read(ref _state) < INITD)
                        spinWait.SpinOnce();
                    return ref _valueRef;
                case UNINIT:
                    _value = _generator();
                    Interlocked.Exchange(ref _state, INITD);
                    return ref _valueRef;
            }

            return ref _valueRef;
        }
    }

    /// <summary>
    /// Constructs the lazy instance.
    /// </summary>
    /// <param name="generator">Generator function for the instance of T</param>
    public FastLazyValue(Func<T> generator)
    {
        _generator = generator;
        _state = UNINIT;
        _value = default;
    }

    /// <summary>
    /// Returns the string representation of the underlying T if it is initialized.
    /// </summary>
    /// <returns></returns>
    public override readonly string? ToString() => _state == INITD ? _value.ToString() : null;
}
