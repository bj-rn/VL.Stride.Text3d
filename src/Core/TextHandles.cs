// Opaque managed wrappers around native DirectWrite COM pointers. These are the types
// that flow through VL patches (as pins) instead of raw pointers or SharpDX objects.
// They live in the unimported Core namespace: user patches only ever see them as opaque
// connections produced by FontAndParagraph and consumed by the Text3d nodes.

using System.Runtime.InteropServices;
using static VL.Stride.Text3d.Interop.ComCallbackHelper;
using IDWriteTextFormat = Silk.NET.DirectWrite.IDWriteTextFormat;
using IDWriteTextLayout = Silk.NET.DirectWrite.IDWriteTextLayout;

namespace VL.Stride.Text3d.Core;

/// <summary>Owns a native IDWriteTextFormat reference.</summary>
public sealed unsafe class TextFormatHandle : IDisposable
{
    private IDWriteTextFormat* pointer;

    private TextFormatHandle(IDWriteTextFormat* pointer)
    {
        this.pointer = pointer;
    }

    /// <summary>Wraps a native pointer. When addRef is false the handle takes over the caller's reference.</summary>
    public static TextFormatHandle FromPointer(nint textFormat, bool addRef)
    {
        if (textFormat == 0)
            throw new ArgumentNullException(nameof(textFormat));
        if (addRef)
            Marshal.AddRef(textFormat);
        return new TextFormatHandle((IDWriteTextFormat*)textFormat);
    }

    public IDWriteTextFormat* Pointer
        => pointer != null ? pointer : throw new ObjectDisposedException(nameof(TextFormatHandle));

    public void Dispose()
    {
        var p = pointer;
        pointer = null;
        if (p != null)
            p->Release();
        GC.SuppressFinalize(this);
    }

    ~TextFormatHandle()
    {
        // DWrite shared factory objects are free-threaded; releasing from the finalizer
        // thread is safe and prevents leaks when Dispose is missed in a live patch.
        var p = pointer;
        pointer = null;
        if (p != null)
            p->Release();
    }
}

/// <summary>Owns a native IDWriteTextLayout reference.</summary>
public sealed unsafe class TextLayoutHandle : IDisposable
{
    private IDWriteTextLayout* pointer;

    private TextLayoutHandle(IDWriteTextLayout* pointer)
    {
        this.pointer = pointer;
    }

    /// <summary>Wraps a native pointer. When addRef is false the handle takes over the caller's reference.</summary>
    public static TextLayoutHandle FromPointer(nint textLayout, bool addRef)
    {
        if (textLayout == 0)
            throw new ArgumentNullException(nameof(textLayout));
        if (addRef)
            Marshal.AddRef(textLayout);
        return new TextLayoutHandle((IDWriteTextLayout*)textLayout);
    }

    public IDWriteTextLayout* Pointer
        => pointer != null ? pointer : throw new ObjectDisposedException(nameof(TextLayoutHandle));

    /// <summary>QIs to a versioned layout interface (e.g. IDWriteTextLayout1/2). Caller must Marshal.Release the result.</summary>
    public nint QueryInterface(in Guid iid)
    {
        Guid localIid = iid;
        ThrowOnFailure(Marshal.QueryInterface((nint)Pointer, ref localIid, out nint ptr));
        return ptr;
    }

    public void Dispose()
    {
        var p = pointer;
        pointer = null;
        if (p != null)
            p->Release();
        GC.SuppressFinalize(this);
    }

    ~TextLayoutHandle()
    {
        var p = pointer;
        pointer = null;
        if (p != null)
            p->Release();
    }
}
