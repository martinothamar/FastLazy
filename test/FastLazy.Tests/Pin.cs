using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FastLazy.Tests;

/// <summary>
/// Stolen from https://stackoverflow.com/a/70917852
/// </summary>
public static class Pin
{
    /// <summary>
    /// use to obtain raw access to a managed object.  allowing pinning.
    /// <para>
    /// Usage:<code>fixed (byte* data = [AND_OPERATOR]GetRawObjectData(managed)){  }</code>
    /// </para>
    /// </summary>
    public static ref byte GetRawObjectData(object o)
    {
        //usage:  fixed (byte* data = &GetRawObjectData(managed)) { }
        return ref new PinnableUnion(o).Pinnable.Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    sealed class Pinnable
    {
        public byte Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct PinnableUnion
    {
        [FieldOffset(0)]
        public object Object;

        [FieldOffset(0)]
        public Pinnable Pinnable;

        public PinnableUnion(object o)
        {
            Unsafe.SkipInit(out this);
            Object = o;
        }
    }
}
