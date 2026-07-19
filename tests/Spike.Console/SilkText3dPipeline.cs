// The Silk.NET equivalent of Text3d.CreatePrimitiveMeshData: TextFormat + TextLayout
// creation, Draw into the managed outline renderer, then extrusion. Mirrors the SharpDX
// code paths exactly (locale "", maxWidth 0, maxHeight 32, NoWrap, property order).

using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Direct2D;
using Silk.NET.DirectWrite;
using Spike.Interop;
using static Spike.Interop.ComCallbackHelper;
using Stride.Graphics;
using D2DFactoryType = Silk.NET.Direct2D.FactoryType;
using DWriteFactoryType = Silk.NET.DirectWrite.FactoryType;
using IDWriteFactory = Silk.NET.DirectWrite.IDWriteFactory;
using IDWriteFontCollection = Silk.NET.DirectWrite.IDWriteFontCollection;
using IDWriteTextFormat = Silk.NET.DirectWrite.IDWriteTextFormat;
using IDWriteTextLayout = Silk.NET.DirectWrite.IDWriteTextLayout;
using IDWriteTextRenderer = Silk.NET.DirectWrite.IDWriteTextRenderer;
using TextAlignment = Silk.NET.DirectWrite.TextAlignment;
using ParagraphAlignment = Silk.NET.DirectWrite.ParagraphAlignment;
using WordWrapping = Silk.NET.DirectWrite.WordWrapping;
using FontWeight = Silk.NET.DirectWrite.FontWeight;
using FontStyle = Silk.NET.DirectWrite.FontStyle;
using FontStretch = Silk.NET.DirectWrite.FontStretch;
using TextRange = Silk.NET.DirectWrite.TextRange;

namespace Spike;

internal static unsafe class SilkText3dPipeline
{
    // Canonical IIDs from d2d1.h / dwrite.h
    private static readonly Guid IID_ID2D1Factory = new("06152247-6f50-465a-9245-118bfd3b6007");
    private static readonly Guid IID_IDWriteFactory = new("b859ee5a-d838-4b5b-a2e8-1adc7d93db48");

    private static ID2D1Factory* d2dFactory;
    private static IDWriteFactory* dwFactory;

    public static void EnsureFactories()
    {
        if (d2dFactory != null)
            return;

        var d2dApi = D2D.GetApi();
        void* d2d = null;
        fixed (Guid* iid = &IID_ID2D1Factory)
            ThrowOnFailure(d2dApi.D2D1CreateFactory(D2DFactoryType.SingleThreaded, iid, null, &d2d));
        d2dFactory = (ID2D1Factory*)d2d;

        var dwApi = DWrite.GetApi();
        IUnknown* dw = null;
        fixed (Guid* iid = &IID_IDWriteFactory)
            ThrowOnFailure(dwApi.DWriteCreateFactory(DWriteFactoryType.Shared, iid, &dw));
        dwFactory = (IDWriteFactory*)dw;
    }

    public static List<VertexPositionNormalTexture> BuildSimple(
        string text, string font, float fontSize,
        TextAlignment textAlignment, ParagraphAlignment paragraphAlignment, float extrude)
    {
        EnsureFactories();

        IDWriteTextFormat* fmt = CreateTextFormat(font, fontSize);

        IDWriteTextLayout* layout = null;
        fixed (char* pText = text)
            ThrowOnFailure(dwFactory->CreateTextLayout(pText, (uint)text.Length, fmt, 0.0f, 32.0f, &layout));

        // Property order as the SharpDX object initializer: WordWrapping, TextAlignment, ParagraphAlignment
        ThrowOnFailure(layout->SetWordWrapping(WordWrapping.NoWrap));
        ThrowOnFailure(layout->SetTextAlignment(textAlignment));
        ThrowOnFailure(layout->SetParagraphAlignment(paragraphAlignment));

        return DrawAndExtrude(layout, fmt, extrude);
    }

    public static List<VertexPositionNormalTexture> BuildAdvancedUnderlineStrike(
        string text, string font, float fontSize, float extrude)
    {
        EnsureFactories();

        IDWriteTextFormat* fmt = CreateTextFormat(font, fontSize);

        IDWriteTextLayout* layout = null;
        fixed (char* pText = text)
            ThrowOnFailure(dwFactory->CreateTextLayout(pText, (uint)text.Length, fmt, 0.0f, 32.0f, &layout));

        ThrowOnFailure(layout->SetWordWrapping(WordWrapping.NoWrap));
        ThrowOnFailure(layout->SetUnderline(true, new TextRange { StartPosition = 0, Length = 5 }));
        ThrowOnFailure(layout->SetStrikethrough(true, new TextRange { StartPosition = 6, Length = 5 }));

        return DrawAndExtrude(layout, fmt, extrude);
    }

    /// <summary>
    /// Creates a TextFormat with SharpDX-convenience-constructor semantics
    /// (Normal weight/style/stretch, system font collection, locale "").
    /// Family name and locale are passed as pinned UTF-16 pointers — do NOT use the
    /// Silk.NET managed-string overloads here: they marshal with the wrong encoding
    /// for DWrite's WCHAR parameters (observed: garbage family name causing font
    /// fallback plus intermittent E_INVALIDARG from unterminated heap reads).
    /// </summary>
    private static IDWriteTextFormat* CreateTextFormat(string font, float fontSize)
    {
        IDWriteTextFormat* fmt = null;
        fixed (char* pFamily = font)
        fixed (char* pLocale = "")
        {
            ThrowOnFailure(dwFactory->CreateTextFormat(pFamily, (IDWriteFontCollection*)null,
                FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, fontSize, pLocale, &fmt));
        }
        return fmt;
    }

    private static List<VertexPositionNormalTexture> DrawAndExtrude(
        IDWriteTextLayout* layout, IDWriteTextFormat* fmt, float extrude)
    {
        var renderer = new SilkOutlineRenderer(d2dFactory);
        var rendererPtr = (IDWriteTextRenderer*)GetComPointer(renderer, typeof(IDWriteTextRendererCallback).GUID);
        var vertices = new List<VertexPositionNormalTexture>(1024);
        try
        {
            ThrowOnFailure(layout->Draw(null, rendererPtr, 0.0f, 0.0f));

            var geometry = renderer.GetGeometry();
            var extruder = new SilkExtruder(d2dFactory);
            extruder.GetVertices(geometry, vertices, extrude);
            if (geometry != null)
                geometry->Release();
        }
        finally
        {
            Marshal.Release((nint)rendererPtr);
            layout->Release();
            fmt->Release();
        }
        return vertices;
    }
}
