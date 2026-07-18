using System.Text;

namespace CharsetFlow.Models;

internal sealed record EncodingOption(
    string Id,
    string DisplayName,
    int CodePage,
    bool EmitBom = false,
    bool BigEndian = false)
{
    public override string ToString() => DisplayName;

    public Encoding CreateEncoding(bool strict = true)
    {
        EncoderFallback encoderFallback = strict
            ? EncoderFallback.ExceptionFallback
            : EncoderFallback.ReplacementFallback;
        DecoderFallback decoderFallback = strict
            ? DecoderFallback.ExceptionFallback
            : DecoderFallback.ReplacementFallback;

        return Id switch
        {
            "utf-8" => new UTF8Encoding(false, strict),
            "utf-8-bom" => new UTF8Encoding(true, strict),
            "utf-16le" => new UnicodeEncoding(false, false, strict),
            "utf-16le-bom" => new UnicodeEncoding(false, true, strict),
            "utf-16be" => new UnicodeEncoding(true, false, strict),
            "utf-16be-bom" => new UnicodeEncoding(true, true, strict),
            "utf-32le" => new UTF32Encoding(false, false, strict),
            "utf-32le-bom" => new UTF32Encoding(false, true, strict),
            "utf-32be" => new UTF32Encoding(true, false, strict),
            "utf-32be-bom" => new UTF32Encoding(true, true, strict),
            _ => Encoding.GetEncoding(CodePage, encoderFallback, decoderFallback)
        };
    }

    public byte[] GetPreamble() => EmitBom ? CreateEncoding().GetPreamble() : [];
}
