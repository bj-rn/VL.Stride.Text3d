// Managed COM callback interfaces for the DirectWrite/Direct2D pipeline, declared with
// .NET 8 source-generated COM interop. These mirror the native vtables exactly:
//  - IDWritePixelSnapping   slots 3-5
//  - IDWriteTextRenderer    slots 6-9 (inherits IDWritePixelSnapping)
//  - ID2D1SimplifiedGeometrySink slots 3-9
//  - ID2D1TessellationSink  slots 3-4
// All methods are [PreserveSig] and use blittable signatures only. Parameters we never
// read (glyph run description, drawing effects, inline objects) are typed void*.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Silk.NET.DirectWrite;

[assembly: DisableRuntimeMarshalling]

namespace Spike.Interop;

/// <summary>D2D1_POINT_2F — 8-byte by-value point used by BeginFigure.</summary>
public struct Point2F
{
    public float X;
    public float Y;
}

[GeneratedComInterface]
[Guid("eaf3a2da-ecf4-4d24-b644-b34f6842024b")]
public unsafe partial interface IDWritePixelSnappingCallback
{
    [PreserveSig] int IsPixelSnappingDisabled(void* clientDrawingContext, int* isDisabled);
    [PreserveSig] int GetCurrentTransform(void* clientDrawingContext, Matrix* transform);
    [PreserveSig] int GetPixelsPerDip(void* clientDrawingContext, float* pixelsPerDip);
}

[GeneratedComInterface]
[Guid("ef8a8135-5cc6-45fe-8825-c5a0724eb819")]
public unsafe partial interface IDWriteTextRendererCallback : IDWritePixelSnappingCallback
{
    [PreserveSig]
    int DrawGlyphRun(void* clientDrawingContext, float baselineOriginX, float baselineOriginY,
        int measuringMode, GlyphRun* glyphRun, void* glyphRunDescription, void* clientDrawingEffect);

    [PreserveSig]
    int DrawUnderline(void* clientDrawingContext, float baselineOriginX, float baselineOriginY,
        Underline* underline, void* clientDrawingEffect);

    [PreserveSig]
    int DrawStrikethrough(void* clientDrawingContext, float baselineOriginX, float baselineOriginY,
        Strikethrough* strikethrough, void* clientDrawingEffect);

    [PreserveSig]
    int DrawInlineObject(void* clientDrawingContext, float originX, float originY,
        void* inlineObject, int isSideways, int isRightToLeft, void* clientDrawingEffect);
}

[GeneratedComInterface]
[Guid("2cd9069e-12e2-11dc-9fed-001143a055f9")]
public unsafe partial interface ID2D1SimplifiedGeometrySinkCallback
{
    [PreserveSig] void SetFillMode(int fillMode);
    [PreserveSig] void SetSegmentFlags(int vertexFlags);
    [PreserveSig] void BeginFigure(Point2F startPoint, int figureBegin);
    [PreserveSig] void AddLines(Point2F* points, uint pointsCount);
    [PreserveSig] void AddBeziers(void* beziers, uint beziersCount);
    [PreserveSig] void EndFigure(int figureEnd);
    [PreserveSig] int Close();
}

[GeneratedComInterface]
[Guid("2cd906c1-12e2-11dc-9fed-001143a055f9")]
public unsafe partial interface ID2D1TessellationSinkCallback
{
    [PreserveSig] void AddTriangles(Silk.NET.Direct2D.Triangle* triangles, uint trianglesCount);
    [PreserveSig] int Close();
}

/// <summary>Bridges managed callback objects to native COM interface pointers.</summary>
public static unsafe class ComCallbackHelper
{
    private static readonly StrategyBasedComWrappers Wrappers = new();

    public const int S_OK = 0;
    public const int E_NOTIMPL = unchecked((int)0x80004001);

    /// <summary>
    /// Returns a native COM interface pointer (AddRef'd) for the given managed object and
    /// interface IID. Caller must release it via <see cref="Marshal.Release(nint)"/>.
    /// </summary>
    public static void* GetComPointer(object callback, in Guid iid)
    {
        nint unknown = Wrappers.GetOrCreateComInterfaceForObject(callback, CreateComInterfaceFlags.None);
        Guid localIid = iid;
        int hr = Marshal.QueryInterface(unknown, ref localIid, out nint ptr);
        Marshal.Release(unknown);
        if (hr != S_OK)
            throw new InvalidOperationException($"QueryInterface for {iid} failed: 0x{hr:X8}");
        return (void*)ptr;
    }

    public static void ThrowOnFailure(int hr, [CallerArgumentExpression(nameof(hr))] string? call = null)
    {
        if (hr < 0)
            throw new InvalidOperationException($"{call} failed with HRESULT 0x{hr:X8}");
    }
}
