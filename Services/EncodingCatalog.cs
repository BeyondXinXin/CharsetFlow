using CharsetFlow.Models;

namespace CharsetFlow.Services;

internal static class EncodingCatalog
{
    public static IReadOnlyList<EncodingOption> All { get; } =
    [
        new("utf-8", "UTF-8", 65001),
        new("utf-8-bom", "UTF-8 BOM", 65001, true),
        new("gb18030", "GB18030", 54936),
        new("utf-16le", "UTF-16 LE", 1200),
        new("utf-16le-bom", "UTF-16 LE BOM", 1200, true),
        new("utf-16be", "UTF-16 BE", 1201, false, true),
        new("utf-16be-bom", "UTF-16 BE BOM", 1201, true, true),
        new("utf-32le", "UTF-32 LE", 12000),
        new("utf-32le-bom", "UTF-32 LE BOM", 12000, true),
        new("utf-32be", "UTF-32 BE", 12001, false, true),
        new("utf-32be-bom", "UTF-32 BE BOM", 12001, true, true),
        new("big5", "Big5", 950),
        new("shift-jis", "SHIFT-JIS", 932),
        new("euc-jp", "EUC-JP", 51932),
        new("euc-kr", "EUC-KR", 51949),
        new("windows-1250", "Windows-1250", 1250),
        new("windows-1251", "Windows-1251", 1251),
        new("windows-1252", "Windows-1252", 1252),
        new("windows-1253", "Windows-1253", 1253),
        new("windows-1254", "Windows-1254", 1254),
        new("windows-1255", "Windows-1255", 1255),
        new("windows-1256", "Windows-1256", 1256),
        new("windows-1257", "Windows-1257", 1257),
        new("windows-1258", "Windows-1258", 1258),
        new("iso-8859-1", "ISO-8859-1", 28591),
        new("iso-8859-2", "ISO-8859-2", 28592),
        new("iso-8859-3", "ISO-8859-3", 28593),
        new("iso-8859-4", "ISO-8859-4", 28594),
        new("iso-8859-5", "ISO-8859-5", 28595),
        new("iso-8859-6", "ISO-8859-6", 28596),
        new("iso-8859-7", "ISO-8859-7", 28597),
        new("iso-8859-8", "ISO-8859-8", 28598),
        new("iso-8859-9", "ISO-8859-9", 28599),
        new("iso-8859-13", "ISO-8859-13", 28603),
        new("iso-8859-15", "ISO-8859-15", 28605),
        new("ibm852", "IBM 852", 852),
        new("ibm855", "IBM 855", 855),
        new("ibm865", "IBM 865", 865),
        new("ibm866", "IBM 866", 866),
        new("koi8-r", "KOI8-R", 20866),
        new("mac-ce", "Central Europe (Mac)", 10029),
        new("mac-cyrillic", "Mac Cyrillic", 10007)
    ];

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ascii"] = "utf-8",
        ["ansi"] = "utf-8",
        ["utf8"] = "utf-8",
        ["utf-8"] = "utf-8",
        ["utf-16"] = "utf-16le",
        ["utf-16le"] = "utf-16le",
        ["utf-16be"] = "utf-16be",
        ["utf-32"] = "utf-32le",
        ["utf-32le"] = "utf-32le",
        ["utf-32be"] = "utf-32be",
        ["gb"] = "gb18030",
        ["gbk"] = "gb18030",
        ["gb2312"] = "gb18030",
        ["gb18030"] = "gb18030",
        ["big5"] = "big5",
        ["big5-hkscs"] = "big5",
        ["shift_jis"] = "shift-jis",
        ["shift-jis"] = "shift-jis",
        ["sjis"] = "shift-jis",
        ["euc-jp"] = "euc-jp",
        ["euc-kr"] = "euc-kr",
        ["ks_c_5601-1987"] = "euc-kr",
        ["cp949"] = "euc-kr",
        ["x-mac-ce"] = "mac-ce",
        ["mac-centraleurope"] = "mac-ce",
        ["x-mac-cyrillic"] = "mac-cyrillic"
    };

    static EncodingCatalog()
    {
        foreach (EncodingOption option in All)
        {
            Aliases.TryAdd(option.Id, option.Id);
            Aliases.TryAdd(option.DisplayName, option.Id);
            try
            {
                Aliases.TryAdd(option.CreateEncoding(false).WebName, option.Id);
            }
            catch
            {
                // Keep the catalog usable even when an optional Windows code page is unavailable.
            }
        }
    }

    public static EncodingOption Default => FindById("utf-8")!;

    public static EncodingOption? FindById(string? id) =>
        All.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));

    public static EncodingOption? Resolve(string? detectedName, int? codePage = null)
    {
        if (!string.IsNullOrWhiteSpace(detectedName) && Aliases.TryGetValue(detectedName.Trim(), out string? id))
        {
            return FindById(id);
        }

        return codePage is null ? null : All.FirstOrDefault(item => item.CodePage == codePage.Value && !item.EmitBom);
    }

    public static EncodingOption WithBomVariant(EncodingOption option, bool hasBom)
    {
        if (!hasBom)
        {
            return FindById(option.Id.Replace("-bom", string.Empty, StringComparison.OrdinalIgnoreCase)) ?? option;
        }

        return FindById(option.Id.EndsWith("-bom", StringComparison.OrdinalIgnoreCase)
            ? option.Id
            : option.Id + "-bom") ?? option;
    }
}
