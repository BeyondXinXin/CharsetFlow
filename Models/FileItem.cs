using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CharsetFlow.Models;

internal sealed class FileItem : INotifyPropertyChanged
{
    private bool _selected = true;
    private EncodingOption? _sourceEncoding;
    private string _encodingName = "Unknown";
    private string _confidenceText = "—";
    private string _lineEndingName = "—";
    private FileStatus _status = FileStatus.Ready;
    private string _statusText = "就绪";

    public required string FullPath { get; init; }
    public required string SourceRoot { get; init; }
    public required string RelativePath { get; init; }
    public required long Size { get; init; }
    public bool IsEmpty { get; init; }
    public string FileName => Path.GetFileName(FullPath);
    public string Folder => Path.GetDirectoryName(FullPath) ?? string.Empty;
    public string SizeText => FormatSize(Size);
    public bool IsManualEncoding { get; private set; }

    public bool Selected
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }

    public EncodingOption? SourceEncoding
    {
        get => _sourceEncoding;
        set => SetField(ref _sourceEncoding, value);
    }

    public string EncodingName
    {
        get => _encodingName;
        set => SetField(ref _encodingName, value);
    }

    public string ConfidenceText
    {
        get => _confidenceText;
        set => SetField(ref _confidenceText, value);
    }

    public string LineEndingName
    {
        get => _lineEndingName;
        set => SetField(ref _lineEndingName, value);
    }

    public FileStatus Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public void SetManualEncoding(EncodingOption option)
    {
        SourceEncoding = option;
        EncodingName = option.DisplayName;
        ConfidenceText = "手动";
        IsManualEncoding = true;
        Status = FileStatus.Ready;
        StatusText = "就绪";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{value:0.##} {units[unit]}";
    }
}
