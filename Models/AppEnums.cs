namespace CharsetFlow.Models;

internal enum FilterMode
{
    Smart,
    Extensions,
    All
}

internal enum OutputMode
{
    InPlace,
    Directory
}

internal enum LineEndingMode
{
    Preserve,
    CrLf,
    Lf,
    Cr
}

internal enum FileStatus
{
    Ready,
    Unknown,
    Converting,
    Success,
    Skipped,
    Failed
}
