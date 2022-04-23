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

    /// <summary>
    /// Returns the value from the generator function, initializing it if needed.
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
    /// Returns the string representation of the underlying T if it is initialized.
    /// </summary>
    /// <returns></returns>
    public override string? ToString() => _state == INITD ? _value.ToString() : null;

    private T ValueSlow
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get
        {
            var prevState = global::System.Threading.Interlocked.CompareExchange(
                ref _state,
                INITING,
                UNINIT
            );
            switch (prevState)
            {
                case INITD:
                    // Someone has already completed init
                    return _value;
                case INITING:
                    // Wait for someone else to complete
                    var spinWait = default(global::System.Threading.SpinWait);
                    while (global::System.Threading.Interlocked.Read(ref _state) < INITD)
                        spinWait.SpinOnce();
                    return _value;
                case UNINIT:
                    _value = _generator();
                    global::System.Threading.Interlocked.Exchange(ref _state, INITD);
                    return _value;
            }

            return _value;
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
}
