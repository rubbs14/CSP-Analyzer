using PdfSharp.Fonts;

namespace CspAnalyzer.Desktop.Services;

/// <summary>
/// PDFsharp 6's Core build has no bundled fonts or OS font discovery - see
/// Task 5's note in docs/superpowers/plans/2026-07-23-s10-results-view.md.
/// Always serves the one bundled DejaVu Sans face regardless of the
/// requested family/weight/style, since the PDF report (Task 5's
/// ExportPdfAsync) only ever asks for one face.
/// </summary>
public sealed class DejaVuFontResolver(byte[] fontBytes) : IFontResolver
{
    private const string FaceName = "DejaVuSans";

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(FaceName, false, false);

    public byte[] GetFont(string faceName) => fontBytes;
}
