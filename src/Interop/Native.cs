// Process-lifetime Direct2D / DirectWrite factory singletons plus creation helpers.
// The factories are created lazily and never released — matching the original SharpDX
// implementation's static factory lifetime. (Statics reset on vvvv live-reload; the
// factories are then lazily recreated, which is fine.)

using Silk.NET.Core.Native;
using Silk.NET.Direct2D;
using Silk.NET.DirectWrite;
using static VL.Stride.Text3d.Interop.ComCallbackHelper;
using D2DFactoryType = Silk.NET.Direct2D.FactoryType;
using DWriteFactoryType = Silk.NET.DirectWrite.FactoryType;
using FontStretch = Silk.NET.DirectWrite.FontStretch;
using FontStyle = Silk.NET.DirectWrite.FontStyle;
using FontWeight = Silk.NET.DirectWrite.FontWeight;
using IDWriteFactory = Silk.NET.DirectWrite.IDWriteFactory;
using IDWriteFontCollection = Silk.NET.DirectWrite.IDWriteFontCollection;
using IDWriteTextFormat = Silk.NET.DirectWrite.IDWriteTextFormat;
using IDWriteTextLayout = Silk.NET.DirectWrite.IDWriteTextLayout;

namespace VL.Stride.Text3d.Interop;

public static unsafe class Native
{
    // Canonical IIDs from d2d1.h / dwrite.h
    private static readonly Guid IID_ID2D1Factory = new("06152247-6f50-465a-9245-118bfd3b6007");
    private static readonly Guid IID_IDWriteFactory = new("b859ee5a-d838-4b5b-a2e8-1adc7d93db48");

    private static readonly object InitLock = new();
    private static ID2D1Factory* d2dFactory;
    private static IDWriteFactory* dwriteFactory;

    public static ID2D1Factory* D2DFactory
    {
        get { EnsureFactories(); return d2dFactory; }
    }

    public static IDWriteFactory* DWriteFactory
    {
        get { EnsureFactories(); return dwriteFactory; }
    }

    private static void EnsureFactories()
    {
        if (d2dFactory != null)
            return;

        lock (InitLock)
        {
            if (d2dFactory != null)
                return;

            var d2dApi = D2D.GetApi();
            void* d2d = null;
            fixed (Guid* iid = &IID_ID2D1Factory)
                ThrowOnFailure(d2dApi.D2D1CreateFactory(D2DFactoryType.SingleThreaded, iid, null, &d2d));

            var dwApi = DWrite.GetApi();
            IUnknown* dw = null;
            fixed (Guid* iid = &IID_IDWriteFactory)
                ThrowOnFailure(dwApi.DWriteCreateFactory(DWriteFactoryType.Shared, iid, &dw));

            dwriteFactory = (IDWriteFactory*)dw;
            d2dFactory = (ID2D1Factory*)d2d;
        }
    }

    /// <summary>
    /// Creates a TextFormat with SharpDX-convenience-constructor semantics
    /// (system font collection, locale "").
    /// Family name and locale are passed as pinned UTF-16 pointers — do NOT use the
    /// Silk.NET managed-string overloads here: they marshal DWrite's WCHAR parameters
    /// with the wrong encoding (observed in the Phase 1 spike: garbage family name
    /// causing silent font fallback plus intermittent E_INVALIDARG).
    /// </summary>
    public static IDWriteTextFormat* CreateTextFormat(string fontFamily, float fontSize,
        FontWeight weight = FontWeight.Normal, FontStyle style = FontStyle.Normal,
        FontStretch stretch = FontStretch.Normal)
    {
        IDWriteTextFormat* fmt = null;
        fixed (char* pFamily = fontFamily)
        fixed (char* pLocale = "")
        {
            ThrowOnFailure(DWriteFactory->CreateTextFormat(pFamily, (IDWriteFontCollection*)null,
                weight, style, stretch, fontSize, pLocale, &fmt));
        }
        return fmt;
    }

    /// <summary>Creates a TextLayout over the given format (pinned UTF-16 text).</summary>
    public static IDWriteTextLayout* CreateTextLayout(string text, IDWriteTextFormat* format,
        float maxWidth, float maxHeight)
    {
        IDWriteTextLayout* layout = null;
        fixed (char* pText = text)
            ThrowOnFailure(DWriteFactory->CreateTextLayout(pText, (uint)text.Length, format, maxWidth, maxHeight, &layout));
        return layout;
    }
}
