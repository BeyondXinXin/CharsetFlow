using CharsetFlow.Models;
using CharsetFlow.Services;

namespace CharsetFlow.UI;

internal sealed class ConversionOptionsControl : UserControl
{
    private readonly ComboBox _filter = CreateChoice("文件过滤", "智能识别", "指定后缀", "不过滤");
    private readonly ComboBox _encoding = CreateChoice("目标编码");
    private readonly ComboBox _lineEnding = CreateChoice("换行符", "保持原样", "CRLF", "LF");
    private readonly ComboBox _output = CreateChoice("输出位置", "覆盖原文件", "指定文件夹");
    private readonly TextBox _extensionFilter = new()
    {
        AccessibleName = "文件后缀",
        PlaceholderText = "例如 txt md json"
    };
    private readonly TextBox _outputDirectory = new()
    {
        AccessibleName = "输出文件夹",
        PlaceholderText = "选择或输入文件夹路径"
    };
    private readonly TableLayoutPanel _outputPath = new();
    private string _excludeRule = string.Empty;
    private bool _recursive = true;
    private bool _loadingSettings;

    public event EventHandler? SettingsChanged;

    public ConversionOptionsControl()
    {
        BackColor = Theme.Subtle;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _encoding.Items.AddRange(EncodingCatalog.All.Cast<object>().ToArray());

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = new Padding(16, 8, 16, 10)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 29));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 19));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        for (int row = 0; row < layout.RowCount; row++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        AddField(layout, _filter, 0);
        AddField(layout, _encoding, 1);
        AddField(layout, _lineEnding, 2);
        AddField(layout, _output, 3);

        _extensionFilter.Dock = DockStyle.Top;
        _extensionFilter.Margin = new Padding(0, 6, 12, 0);
        layout.Controls.Add(_extensionFilter, 0, 2);
        layout.SetColumnSpan(_extensionFilter, 2);
        BuildOutputPath();
        layout.Controls.Add(_outputPath, 2, 2);
        layout.SetColumnSpan(_outputPath, 2);
        Controls.Add(layout);

        foreach (ComboBox choice in new[] { _filter, _encoding, _lineEnding, _output })
        {
            choice.SelectedIndexChanged += SettingControl_Changed;
        }

        _extensionFilter.TextChanged += SettingControl_Changed;
        _outputDirectory.TextChanged += SettingControl_Changed;
    }

    public void LoadSettings(AppSettings settings)
    {
        _loadingSettings = true;
        try
        {
            _filter.SelectedIndex = settings.FilterMode switch
            {
                FilterMode.Extensions => 1,
                FilterMode.All => 2,
                _ => 0
            };
            _extensionFilter.Text = settings.IncludeRule;
            _excludeRule = settings.ExcludeRule;
            _recursive = settings.Recursive;
            _encoding.SelectedItem = EncodingCatalog.FindById(settings.TargetEncodingId) ?? EncodingCatalog.Default;
            _lineEnding.SelectedIndex = settings.LineEnding switch
            {
                LineEndingMode.CrLf => 1,
                LineEndingMode.Lf => 2,
                _ => 0
            };
            _output.SelectedIndex = settings.OutputMode == OutputMode.Directory ? 1 : 0;
            _outputDirectory.Text = settings.OutputDirectory;
        }
        finally
        {
            _loadingSettings = false;
            UpdateVisibleFields();
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        ScanOptions scan = GetScanOptions();
        ConversionOptions conversion = GetConversionOptions();
        settings.FilterMode = scan.FilterMode;
        settings.IncludeRule = scan.IncludeRule;
        settings.ExcludeRule = scan.ExcludeRule;
        settings.Recursive = scan.Recursive;
        settings.TargetEncodingId = conversion.TargetEncoding.Id;
        settings.LineEnding = conversion.LineEnding;
        settings.OutputMode = conversion.OutputMode;
        settings.OutputDirectory = conversion.OutputDirectory;
        settings.CreateBackup = conversion.CreateBackup;
        settings.VerifyRoundTrip = conversion.VerifyRoundTrip;
    }

    public ScanOptions GetScanOptions() => new(
        _filter.SelectedIndex switch { 1 => FilterMode.Extensions, 2 => FilterMode.All, _ => FilterMode.Smart },
        _extensionFilter.Text.Trim(),
        _excludeRule,
        _recursive);

    public ConversionOptions GetConversionOptions() => new(
        _encoding.SelectedItem as EncodingOption ?? EncodingCatalog.Default,
        _lineEnding.SelectedIndex switch { 1 => LineEndingMode.CrLf, 2 => LineEndingMode.Lf, _ => LineEndingMode.Preserve },
        _output.SelectedIndex == 1 ? OutputMode.Directory : OutputMode.InPlace,
        _outputDirectory.Text.Trim(),
        false,
        true);

    public bool ValidateConversionOptions(out string message)
    {
        if (_output.SelectedIndex == 1 && string.IsNullOrWhiteSpace(_outputDirectory.Text))
        {
            message = "请选择输出文件夹。";
            _outputDirectory.Focus();
            return false;
        }

        message = string.Empty;
        return true;
    }

    private void SettingControl_Changed(object? sender, EventArgs e)
    {
        if (!_loadingSettings)
        {
            UpdateVisibleFields();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateVisibleFields()
    {
        _extensionFilter.Visible = _filter.SelectedIndex == 1;
        _outputPath.Visible = _output.SelectedIndex == 1;
    }

    private void BuildOutputPath()
    {
        _outputPath.AutoSize = true;
        _outputPath.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _outputPath.Dock = DockStyle.Top;
        _outputPath.Margin = new Padding(0, 6, 0, 0);
        _outputPath.ColumnCount = 2;
        _outputPath.RowCount = 1;
        _outputPath.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _outputPath.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _outputDirectory.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _outputDirectory.Margin = new Padding(0, 0, 6, 0);
        Button browse = Theme.CreateButton("浏览");
        browse.Margin = Padding.Empty;
        browse.Click += BrowseOutput_Click;
        _outputPath.Controls.Add(_outputDirectory, 0, 0);
        _outputPath.Controls.Add(browse, 1, 0);
    }

    private void BrowseOutput_Click(object? sender, EventArgs e)
    {
        using FolderBrowserDialog dialog = new()
        {
            Description = "选择输出文件夹",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_outputDirectory.Text) ? _outputDirectory.Text : string.Empty,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
        {
            _outputDirectory.Text = dialog.SelectedPath;
        }
    }

    private static void AddField(TableLayoutPanel layout, ComboBox choice, int column)
    {
        Label label = new()
        {
            Text = choice.AccessibleName,
            AutoSize = true,
            ForeColor = Theme.Muted,
            Margin = new Padding(0, 0, 0, 4)
        };
        choice.Dock = DockStyle.Top;
        choice.Margin = new Padding(0, 0, column < layout.ColumnCount - 1 ? 12 : 0, 0);
        layout.Controls.Add(label, column, 0);
        layout.Controls.Add(choice, column, 1);
    }

    private static ComboBox CreateChoice(string name, params string[] items)
    {
        ComboBox choice = new()
        {
            AccessibleName = name,
            DropDownStyle = ComboBoxStyle.DropDownList,
            MaxDropDownItems = 12
        };
        choice.Items.AddRange(items);
        return choice;
    }
}
