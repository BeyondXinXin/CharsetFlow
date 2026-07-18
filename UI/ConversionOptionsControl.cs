using CharsetFlow.Models;
using CharsetFlow.Services;

namespace CharsetFlow.UI;

internal sealed class ConversionOptionsControl : UserControl
{
    private readonly RadioButton _filterAll = CreateRadioButton("不过滤");
    private readonly RadioButton _filterSmart = CreateRadioButton("智能识别文本文件");
    private readonly RadioButton _filterExtensions = CreateRadioButton("只扫描指定后缀的文件");
    private readonly TextBox _extensionFilter = new()
    {
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = Theme.Input,
        ForeColor = Theme.Text,
        Font = Theme.Font(9F),
        Height = 24
    };
    private readonly RadioButton _utf8 = CreateRadioButton("UTF-8");
    private readonly RadioButton _utf8Bom = CreateRadioButton("UTF-8 BOM");
    private readonly RadioButton _otherEncoding = CreateRadioButton("其他");
    private readonly ComboBox _otherEncodingList = CreateComboBox();
    private readonly CheckBox _convertLineEndings = CreateCheckBox("转换");
    private readonly RadioButton _crlf = CreateRadioButton("CRLF");
    private readonly RadioButton _lf = CreateRadioButton("LF");
    private readonly RadioButton _inPlace = CreateRadioButton("覆盖");
    private readonly RadioButton _toDirectory = CreateRadioButton("文件夹");
    private readonly TextBox _outputDirectory = new()
    {
        BorderStyle = BorderStyle.FixedSingle,
        Multiline = true,
        BackColor = Theme.Input,
        ForeColor = Theme.Text,
        Font = Theme.Font(9F),
        Height = 24
    };
    private readonly ModernButton _browseOutput = new()
    {
        Text = "...",
        Width = 34,
        Padding = new Padding(0),
        ButtonStyle = ModernButtonStyle.Secondary
    };

    private string _excludeRule = string.Empty;
    private bool _recursive = true;
    private bool _loadingSettings;

    public event EventHandler? SettingsChanged;

    public ConversionOptionsControl()
    {
        BackColor = Theme.Window;
        Padding = new Padding(8, 4, 8, 8);
        AutoScroll = false;

        _otherEncodingList.Items.AddRange(EncodingCatalog.All
            .Where(option => option.Id is not ("utf-8" or "utf-8-bom"))
            .Cast<object>()
            .ToArray());

        TableLayoutPanel content = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            BackColor = Theme.Window,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        content.Controls.Add(CreateSectionLabel("文件过滤"));
        content.Controls.Add(CreateFilterGroup());
        content.Controls.Add(CreateDivider());
        content.Controls.Add(CreateSectionLabel("输出字符集"));
        content.Controls.Add(CreateEncodingGroup());
        content.Controls.Add(CreateDivider());
        content.Controls.Add(CreateSectionLabel("换行符"));
        content.Controls.Add(CreateLineEndingGroup());
        content.Controls.Add(CreateDivider());
        content.Controls.Add(CreateSectionLabel("输出位置"));
        content.Controls.Add(CreateOutputGroup());
        Controls.Add(content);

        foreach (RadioButton radio in new[]
                 {
                     _filterAll, _filterSmart, _filterExtensions,
                     _utf8, _utf8Bom, _otherEncoding,
                     _crlf, _lf, _inPlace, _toDirectory
                 })
        {
            radio.CheckedChanged += SettingControl_Changed;
        }

        _convertLineEndings.CheckedChanged += SettingControl_Changed;
        _otherEncodingList.SelectedIndexChanged += SettingControl_Changed;
        _extensionFilter.TextChanged += SettingControl_Changed;
        _outputDirectory.TextChanged += SettingControl_Changed;
        _browseOutput.Click += BrowseOutput_Click;
    }

    public void LoadSettings(AppSettings settings)
    {
        _loadingSettings = true;
        try
        {
            _filterAll.Checked = settings.FilterMode == FilterMode.All;
            _filterSmart.Checked = settings.FilterMode == FilterMode.Smart;
            _filterExtensions.Checked = settings.FilterMode == FilterMode.Extensions;
            _extensionFilter.Text = settings.IncludeRule;
            _excludeRule = settings.ExcludeRule;
            _recursive = settings.Recursive;

            EncodingOption selected = EncodingCatalog.FindById(settings.TargetEncodingId) ?? EncodingCatalog.Default;
            _utf8.Checked = selected.Id == "utf-8";
            _utf8Bom.Checked = selected.Id == "utf-8-bom";
            _otherEncoding.Checked = !_utf8.Checked && !_utf8Bom.Checked;
            _otherEncodingList.SelectedItem = _otherEncoding.Checked
                ? selected
                : _otherEncodingList.Items.Cast<object>().FirstOrDefault();

            _convertLineEndings.Checked = settings.LineEnding is LineEndingMode.CrLf or LineEndingMode.Lf;
            _lf.Checked = settings.LineEnding == LineEndingMode.Lf;
            _crlf.Checked = !_lf.Checked;

            _toDirectory.Checked = settings.OutputMode == OutputMode.Directory;
            _inPlace.Checked = !_toDirectory.Checked;
            _outputDirectory.Text = settings.OutputDirectory;
        }
        finally
        {
            _loadingSettings = false;
            UpdateEnabledState();
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        settings.FilterMode = SelectedFilterMode;
        settings.IncludeRule = _extensionFilter.Text.Trim();
        settings.ExcludeRule = _excludeRule;
        settings.Recursive = _recursive;
        settings.TargetEncodingId = SelectedEncoding.Id;
        settings.LineEnding = !_convertLineEndings.Checked
            ? LineEndingMode.Preserve
            : _lf.Checked ? LineEndingMode.Lf : LineEndingMode.CrLf;
        settings.OutputMode = _toDirectory.Checked ? OutputMode.Directory : OutputMode.InPlace;
        settings.OutputDirectory = _outputDirectory.Text.Trim();
        settings.CreateBackup = false;
        settings.VerifyRoundTrip = true;
    }

    public ScanOptions GetScanOptions() => new(
        SelectedFilterMode,
        _extensionFilter.Text.Trim(),
        _excludeRule,
        _recursive);

    public ConversionOptions GetConversionOptions() => new(
        SelectedEncoding,
        !_convertLineEndings.Checked ? LineEndingMode.Preserve : _lf.Checked ? LineEndingMode.Lf : LineEndingMode.CrLf,
        _toDirectory.Checked ? OutputMode.Directory : OutputMode.InPlace,
        _outputDirectory.Text.Trim(),
        false,
        true);

    public bool ValidateConversionOptions(out string message)
    {
        if (_toDirectory.Checked && string.IsNullOrWhiteSpace(_outputDirectory.Text))
        {
            message = "请选择输出文件夹。";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private EncodingOption SelectedEncoding => _utf8.Checked
        ? EncodingCatalog.FindById("utf-8")!
        : _utf8Bom.Checked
            ? EncodingCatalog.FindById("utf-8-bom")!
            : _otherEncodingList.SelectedItem as EncodingOption ?? EncodingCatalog.FindById("gb18030")!;

    private FilterMode SelectedFilterMode => _filterAll.Checked
        ? FilterMode.All
        : _filterExtensions.Checked ? FilterMode.Extensions : FilterMode.Smart;

    private void SettingControl_Changed(object? sender, EventArgs e)
    {
        UpdateEnabledState();
        if (!_loadingSettings)
        {
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateEnabledState()
    {
        bool extensionsEnabled = _filterExtensions.Checked;
        _extensionFilter.Enabled = extensionsEnabled;
        _extensionFilter.BackColor = extensionsEnabled ? Theme.Input : Theme.Disabled;
        _extensionFilter.ForeColor = extensionsEnabled ? Theme.Text : Theme.DisabledText;
        _otherEncodingList.Enabled = _otherEncoding.Checked;
        _otherEncodingList.BackColor = _otherEncoding.Checked ? Theme.Input : Theme.Disabled;
        _otherEncodingList.ForeColor = _otherEncoding.Checked ? Theme.Text : Theme.DisabledText;
        _crlf.Enabled = _convertLineEndings.Checked;
        _lf.Enabled = _convertLineEndings.Checked;
        _outputDirectory.Enabled = _toDirectory.Checked;
        _outputDirectory.BackColor = _toDirectory.Checked ? Theme.Input : Theme.Disabled;
        _outputDirectory.ForeColor = _toDirectory.Checked ? Theme.Text : Theme.DisabledText;
        _browseOutput.Enabled = _toDirectory.Checked;
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

    private Control CreateFilterGroup()
    {
        TableLayoutPanel group = new()
        {
            Dock = DockStyle.Top,
            Height = 104,
            MinimumSize = new Size(0, 104),
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Window,
            Margin = new Padding(0)
        };
        for (int row = 0; row < 4; row++)
        {
            group.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        }

        _filterAll.Dock = DockStyle.Fill;
        _filterSmart.Dock = DockStyle.Fill;
        _filterExtensions.Dock = DockStyle.Fill;
        _extensionFilter.Dock = DockStyle.Fill;
        _extensionFilter.Margin = new Padding(18, 1, 0, 1);
        group.Controls.Add(_filterAll, 0, 0);
        group.Controls.Add(_filterSmart, 0, 1);
        group.Controls.Add(_filterExtensions, 0, 2);
        group.Controls.Add(_extensionFilter, 0, 3);
        return group;
    }

    private Control CreateEncodingGroup()
    {
        TableLayoutPanel group = new()
        {
            Dock = DockStyle.Top,
            Height = 56,
            MinimumSize = new Size(0, 56),
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Theme.Window,
            Margin = new Padding(0)
        };
        group.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
        group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        group.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        group.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        _utf8.Dock = DockStyle.Fill;
        _utf8Bom.Dock = DockStyle.Fill;
        _otherEncoding.Dock = DockStyle.Fill;
        _otherEncodingList.Dock = DockStyle.Fill;
        _otherEncodingList.Margin = new Padding(0, 2, 0, 2);
        group.Controls.Add(_utf8, 0, 0);
        group.Controls.Add(_utf8Bom, 1, 0);
        group.Controls.Add(_otherEncoding, 0, 1);
        group.Controls.Add(_otherEncodingList, 1, 1);
        return group;
    }

    private Control CreateLineEndingGroup()
    {
        TableLayoutPanel group = new()
        {
            Dock = DockStyle.Top,
            Height = 28,
            MinimumSize = new Size(0, 28),
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Theme.Window,
            Margin = new Padding(0)
        };
        group.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        group.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        group.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        _convertLineEndings.Dock = DockStyle.Fill;
        _crlf.Dock = DockStyle.Fill;
        _crlf.Margin = new Padding(0);
        _lf.Dock = DockStyle.Fill;
        _lf.Margin = new Padding(0);
        group.Controls.Add(_convertLineEndings, 0, 0);
        group.Controls.Add(_crlf, 1, 0);
        group.Controls.Add(_lf, 2, 0);
        return group;
    }

    private Control CreateOutputGroup()
    {
        TableLayoutPanel group = new()
        {
            Dock = DockStyle.Top,
            Height = 56,
            MinimumSize = new Size(0, 56),
            ColumnCount = 3,
            RowCount = 2,
            BackColor = Theme.Window,
            Margin = new Padding(0)
        };
        group.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        group.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        group.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        group.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        _inPlace.Dock = DockStyle.Fill;
        _toDirectory.Dock = DockStyle.Fill;
        _outputDirectory.Dock = DockStyle.Fill;
        _outputDirectory.Margin = new Padding(0, 2, 6, 2);
        _browseOutput.Dock = DockStyle.Fill;
        _browseOutput.Margin = new Padding(0, 2, 0, 2);
        group.Controls.Add(_inPlace, 0, 0);
        group.Controls.Add(_toDirectory, 1, 0);
        group.SetColumnSpan(_toDirectory, 2);
        group.Controls.Add(_outputDirectory, 0, 1);
        group.SetColumnSpan(_outputDirectory, 2);
        group.Controls.Add(_browseOutput, 2, 1);
        return group;
    }

    private static Label CreateSectionLabel(string text) => new()
    {
        Text = text,
        AutoSize = false,
        Height = 30,
        MinimumSize = new Size(0, 30),
        Margin = new Padding(0),
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Theme.Text,
        Font = Theme.Font(10F, FontStyle.Bold),
        Dock = DockStyle.Top
    };

    private static Control CreateDivider()
    {
        Panel divider = new()
        {
            Height = 11,
            MinimumSize = new Size(0, 11),
            Dock = DockStyle.Top,
            BackColor = Theme.Window,
            Margin = new Padding(0)
        };
        divider.Paint += (_, e) =>
        {
            using Pen line = new(Theme.Border);
            e.Graphics.DrawLine(line, 0, divider.Height / 2, divider.Width, divider.Height / 2);
        };
        return divider;
    }

    private static RadioButton CreateRadioButton(string text) => new ModernRadioButton
    {
        Text = text,
        AutoSize = false,
        Height = 28,
        Margin = new Padding(0),
        ForeColor = Theme.Text,
        Font = Theme.Font(9.5F),
        Cursor = Cursors.Hand
    };

    private static CheckBox CreateCheckBox(string text) => new ModernCheckBox
    {
        Text = text,
        AutoSize = false,
        Height = 28,
        Margin = new Padding(0),
        ForeColor = Theme.Text,
        Font = Theme.Font(9.5F),
        Cursor = Cursors.Hand
    };

    private static ComboBox CreateComboBox() => new ModernComboBox
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.Flat,
        Height = 24,
        IntegralHeight = false,
        DropDownHeight = 260,
        BackColor = Theme.Input,
        ForeColor = Theme.Text,
        Font = Theme.Font(9F)
    };
}
