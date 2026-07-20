using System.ComponentModel;
using System.Diagnostics;
using CharsetFlow.Models;
using CharsetFlow.Services;
using CharsetFlow.UI;

namespace CharsetFlow;

internal sealed class MainForm : Form
{
    private readonly string[] _startupArguments;
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly EncodingDetectionService _detector = new();
    private readonly FileConversionService _converter = new();
    private readonly BindingList<FileItem> _files = [];
    private readonly BindingSource _bindingSource = new();
    private readonly SplitContainer _mainSplit = new();

    private readonly ConversionOptionsControl _options = new();
    private readonly DataGridView _grid = new();
    private readonly Panel _emptyState = new();
    private readonly RichTextBox _preview = new();
    private readonly Label _previewTitle = new();
    private readonly Label _fileCount = new();
    private readonly Label _status = new();
    private readonly SlimProgressBar _progress = new();
    private readonly ModernButton _addFilesButton = CreateButton("添加文件", ModernButtonStyle.Secondary, 100);
    private readonly ModernButton _addFolderButton = CreateButton("添加文件夹", ModernButtonStyle.Secondary, 112);
    private readonly ModernButton _removeButton = CreateButton("移除", ModernButtonStyle.Ghost, 72);
    private readonly ModernButton _clearButton = CreateButton("清空", ModernButtonStyle.Ghost, 72);
    private readonly ModernButton _settingsButton = CreateButton("转换设置", ModernButtonStyle.Secondary, 104);
    private readonly ModernButton _convertButton = CreateButton("开始转换", ModernButtonStyle.Primary, 126);
    private readonly ModernButton _cancelButton = CreateButton("取消", ModernButtonStyle.Secondary, 82);

    private CancellationTokenSource? _operationCancellation;
    private CancellationTokenSource? _previewCancellation;
    private bool _busy;

    public MainForm(string[] startupArguments)
    {
        _startupArguments = startupArguments;
        Text = "CharsetFlow";
        if (LoadApplicationIcon() is { } applicationIcon)
        {
            Icon = applicationIcon;
        }

        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Font = Theme.Font();
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 520);
        ClientSize = new Size(960, 620);
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;
        AllowDrop = true;

        BuildLayout();
        ConfigureGrid();
        WireEvents();
        _options.LoadSettings(_settings);
        UpdateFileState();
    }

    private static System.Drawing.Icon? LoadApplicationIcon()
    {
        using Stream? iconStream = typeof(MainForm).Assembly.GetManifestResourceStream("CharsetFlow.Assets.CharsetFlow.ico");
        if (iconStream is null)
        {
            return null;
        }

        using System.Drawing.Icon embeddedIcon = new(iconStream);
        return (System.Drawing.Icon)embeddedIcon.Clone();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        WindowEffects.Apply(Handle);
        if (_mainSplit.Width > 460)
        {
            _mainSplit.Panel1MinSize = 252;
            _mainSplit.SplitterDistance = 252;
        }

        string[] paths = _startupArguments.Where(path => File.Exists(path) || Directory.Exists(path)).ToArray();
        if (paths.Length > 0)
        {
            BeginInvoke(async () => await AddPathsAsync(paths));
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _operationCancellation?.Cancel();
        _previewCancellation?.Cancel();
        SaveSettings();
        base.OnFormClosing(e);
    }

    private void SaveSettings()
    {
        _options.SaveSettings(_settings);
        try
        {
            _settings.Save();
        }
        catch
        {
            // A settings write failure should never block closing the app.
        }
    }

    private void BuildLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0),
            BackColor = Theme.Window
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Theme.Border }, 0, 0);
        root.Controls.Add(CreateContent(), 0, 1);
        Controls.Add(root);
    }

    private Control CreateHeader()
    {
        Panel header = new() { Dock = DockStyle.Fill, BackColor = Theme.Window };

        RoundedPanel logo = new()
        {
            Location = new Point(0, 8),
            Size = new Size(48, 48),
            Radius = 12,
            FillColor = Theme.Accent,
            BorderColor = Theme.Accent
        };
        logo.Controls.Add(new Label
        {
            Text = "Aa",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Font = Theme.Font(15F, FontStyle.Bold)
        });

        Label title = new()
        {
            Text = "CharsetFlow",
            Location = new Point(64, 4),
            AutoSize = true,
            ForeColor = Theme.Text,
            Font = Theme.Font(22F, FontStyle.Bold)
        };
        Label subtitle = new()
        {
            Text = "智能识别 · 批量转换 · 无损校验",
            Location = new Point(66, 42),
            AutoSize = true,
            ForeColor = Theme.Muted,
            Font = Theme.Font(9F)
        };

        Label safety = new()
        {
            Text = "本地处理  ·  文件不会上传",
            Dock = DockStyle.Right,
            Width = 220,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Theme.Muted,
            Font = Theme.Font(8.5F)
        };

        header.Controls.Add(safety);
        header.Controls.Add(subtitle);
        header.Controls.Add(title);
        header.Controls.Add(logo);
        return header;
    }

    private Control CreateToolbar()
    {
        Panel toolbar = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Window,
            Padding = new Padding(8, 6, 8, 5)
        };
        FlowLayoutPanel actions = new()
        {
            Dock = DockStyle.Left,
            Width = 320,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Window,
            Padding = new Padding(0)
        };
        foreach (ModernButton button in new[] { _addFilesButton, _addFolderButton, _removeButton })
        {
            button.Margin = new Padding(0, 0, 6, 0);
            actions.Controls.Add(button);
        }

        _fileCount.Dock = DockStyle.Right;
        _fileCount.Width = 180;
        _fileCount.TextAlign = ContentAlignment.MiddleRight;
        _fileCount.ForeColor = Theme.Muted;
        _fileCount.Font = Theme.Font(9F);

        toolbar.Controls.Add(_fileCount);
        toolbar.Controls.Add(actions);
        toolbar.Controls.Add(new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 1,
            BackColor = Theme.Border
        });
        return toolbar;
    }

    private Control CreateContent()
    {
        _mainSplit.Dock = DockStyle.Fill;
        _mainSplit.Orientation = Orientation.Vertical;
        _mainSplit.SplitterWidth = 1;
        _mainSplit.FixedPanel = FixedPanel.Panel1;
        _mainSplit.IsSplitterFixed = true;
        _mainSplit.BackColor = Theme.Border;
        _mainSplit.Panel1.BackColor = Theme.Window;
        _mainSplit.Panel2.BackColor = Theme.Card;
        _mainSplit.Panel1.Controls.Add(CreateLeftSettings());
        _mainSplit.Panel2.Controls.Add(CreateListWorkspace());
        return _mainSplit;
    }

    private Control CreateLeftSettings()
    {
        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Window,
            Padding = new Padding(12, 8, 12, 4)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 5));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        TableLayoutPanel buttons = new()
        {
            Dock = DockStyle.Top,
            Height = 28,
            Margin = new Padding(0),
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Theme.Window
        };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 6));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        buttons.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        _convertButton.Dock = DockStyle.Fill;
        _convertButton.Margin = new Padding(0);
        _clearButton.ButtonStyle = ModernButtonStyle.Secondary;
        _clearButton.Dock = DockStyle.Fill;
        _clearButton.Margin = new Padding(0);
        buttons.Controls.Add(_convertButton, 0, 0);
        buttons.Controls.Add(_clearButton, 2, 0);

        _progress.Dock = DockStyle.Fill;
        _progress.Margin = new Padding(0);
        _progress.Visible = false;
        _options.Dock = DockStyle.Fill;
        _options.Margin = new Padding(0);
        layout.Controls.Add(buttons, 0, 0);
        layout.Controls.Add(_progress, 0, 1);
        layout.Controls.Add(_options, 0, 2);
        return layout;
    }

    private Control CreateListWorkspace()
    {
        TableLayoutPanel workspace = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Card,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        Panel gridHost = new() { Dock = DockStyle.Fill, BackColor = Theme.Card };
        _grid.Dock = DockStyle.Fill;
        gridHost.Controls.Add(_grid);
        workspace.Controls.Add(CreateToolbar(), 0, 0);
        workspace.Controls.Add(gridHost, 0, 1);
        return workspace;
    }

    private void BuildEmptyState()
    {
        _emptyState.BackColor = Theme.Card;
        TableLayoutPanel center = new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            Anchor = AnchorStyles.None,
            BackColor = Theme.Card
        };
        center.Controls.Add(new Label
        {
            Text = "＋",
            AutoSize = false,
            Size = new Size(220, 56),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Theme.Accent,
            Font = Theme.Font(30F, FontStyle.Regular)
        });
        center.Controls.Add(new Label
        {
            Text = "拖放文件或文件夹",
            AutoSize = false,
            Size = new Size(220, 28),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Theme.Text,
            Font = Theme.Font(11F, FontStyle.Bold)
        });
        center.Controls.Add(new Label
        {
            Text = "松开后自动识别文本编码",
            AutoSize = false,
            Size = new Size(300, 30),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Theme.Muted,
            Font = Theme.Font(8.8F)
        });
        _emptyState.Controls.Add(center);
        _emptyState.Resize += (_, _) => center.Location = new Point(
            Math.Max(0, (_emptyState.Width - center.Width) / 2),
            Math.Max(0, (_emptyState.Height - center.Height) / 2));
    }

    private Control CreateFooter()
    {
        Panel footer = new() { Dock = DockStyle.Fill, BackColor = Theme.Window, Padding = new Padding(0, 12, 0, 0) };
        _progress.Dock = DockStyle.Top;
        _progress.Visible = false;

        _status.Text = "准备就绪";
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.ForeColor = Theme.Muted;
        _status.Font = Theme.Font(9F);

        FlowLayoutPanel actions = new()
        {
            Dock = DockStyle.Right,
            Width = 340,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Theme.Window,
            Padding = new Padding(0, 7, 0, 0)
        };
        _convertButton.Margin = new Padding(8, 0, 0, 0);
        _settingsButton.Margin = new Padding(8, 0, 0, 0);
        _cancelButton.Margin = new Padding(0);
        _cancelButton.Visible = false;
        actions.Controls.Add(_convertButton);
        actions.Controls.Add(_settingsButton);
        actions.Controls.Add(_cancelButton);

        footer.Controls.Add(_status);
        footer.Controls.Add(actions);
        footer.Controls.Add(_progress);
        return footer;
    }

    private void ConfigureGrid()
    {
        _grid.AutoGenerateColumns = false;
        _bindingSource.DataSource = _files;
        _grid.DataSource = _bindingSource;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.BackgroundColor = Theme.Card;
        _grid.BorderStyle = BorderStyle.None;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.GridColor = Theme.Border;
        _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        _grid.ColumnHeadersHeight = 30;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.RowHeadersVisible = false;
        _grid.RowTemplate.Height = 28;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = true;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Subtle,
            ForeColor = Theme.Text,
            Font = Theme.Font(9F, FontStyle.Bold),
            SelectionBackColor = Theme.Subtle,
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 6, 0)
        };
        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Card,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.AccentLight,
            SelectionForeColor = Theme.Text,
            Font = Theme.Font(9F),
            Padding = new Padding(6, 0, 6, 0)
        };
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Theme.Card;

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "RowNumber",
            HeaderText = "序号",
            Width = 54,
            MinimumWidth = 48,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(CreateTextColumn("文件名 / 路径", nameof(FileItem.FullPath), 260, DataGridViewAutoSizeColumnMode.Fill, 100F));
        _grid.Columns.Add(CreateTextColumn("编码", nameof(FileItem.EncodingName), 130));
        _grid.Columns.Add(CreateTextColumn("换行符", nameof(FileItem.LineEndingName), 92));
        _grid.ContextMenuStrip = CreateGridContextMenu();
    }

    private void WireEvents()
    {
        _addFilesButton.Click += AddFilesButton_Click;
        _addFolderButton.Click += AddFolderButton_Click;
        _removeButton.Click += (_, _) => RemoveHighlightedFiles();
        _clearButton.Click += (_, _) => ClearFiles();
        _convertButton.Click += ConvertButton_Click;
        _options.SettingsChanged += (_, _) => SaveSettings();
        _files.ListChanged += (_, _) => UpdateFileState();
        _grid.CellFormatting += Grid_CellFormatting;
        _grid.CellMouseDown += Grid_CellMouseDown;
        _grid.DataError += (_, _) => { };

        DragEnter += MainForm_DragEnter;
        DragDrop += MainForm_DragDrop;
        KeyDown += MainForm_KeyDown;
    }

    private async void AddFilesButton_Click(object? sender, EventArgs e)
    {
        using OpenFileDialog dialog = new()
        {
            Title = "选择要识别的文件",
            Multiselect = true,
            Filter = "所有文件 (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            await AddPathsAsync(dialog.FileNames);
        }
    }

    private async void AddFolderButton_Click(object? sender, EventArgs e)
    {
        using FolderBrowserDialog dialog = new()
        {
            Description = "选择要扫描的文件夹",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            await AddPathsAsync([dialog.SelectedPath]);
        }
    }

    private async Task AddPathsAsync(IEnumerable<string> paths)
    {
        if (_busy)
        {
            return;
        }

        string[] validPaths = paths.Where(path => File.Exists(path) || Directory.Exists(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (validPaths.Length == 0)
        {
            return;
        }

        SetBusy(true, "正在枚举并识别文件…");
        _operationCancellation = new CancellationTokenSource();
        try
        {
            HashSet<string> existing = _files.Select(file => file.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            FileScanner scanner = new(_detector);
            Progress<ScanProgress> progress = new(value =>
            {
                _progress.Maximum = Math.Max(1, value.Total);
                _progress.Value = value.Completed;
                _status.Text = $"正在识别 {value.Completed}/{value.Total}  ·  {Path.GetFileName(value.CurrentFile)}";
            });

            IReadOnlyList<FileItem> files = await scanner.ScanAsync(
                validPaths,
                _options.GetScanOptions(),
                existing,
                progress,
                _operationCancellation.Token);

            foreach (FileItem file in files)
            {
                _files.Add(file);
            }

            _status.Text = files.Count == 0
                ? "没有发现符合当前识别规则的文本文件"
                : $"已添加 {files.Count} 个文件";
        }
        catch (OperationCanceledException)
        {
            _status.Text = "已取消添加";
        }
        catch (Exception exception)
        {
            _status.Text = "添加文件失败";
            MessageBox.Show(this, exception.Message, "CharsetFlow", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _operationCancellation.Dispose();
            _operationCancellation = null;
            SetBusy(false);
        }
    }

    private async void ConvertButton_Click(object? sender, EventArgs e)
    {
        if (_busy)
        {
            _operationCancellation?.Cancel();
            return;
        }

        _grid.EndEdit();
        List<FileItem> selected = _files.Where(file => file.Selected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, "请先勾选至少一个文件。", "CharsetFlow", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!_options.ValidateConversionOptions(out string validationMessage))
        {
            MessageBox.Show(this, validationMessage, "CharsetFlow", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        List<FileItem> unknown = selected.Where(item => item.SourceEncoding is null && !item.IsEmpty).ToList();
        if (unknown.Count > 0)
        {
            MessageBox.Show(
                this,
                $"有 {unknown.Count} 个文件无法可靠识别源编码。请在文件列表中右键并指定源编码。",
                "需要源编码",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        ConversionOptions options = _options.GetConversionOptions();
        SetBusy(true, $"准备转换 {selected.Count} 个文件…");
        _operationCancellation = new CancellationTokenSource();
        int succeeded = 0;
        int skipped = 0;
        int failed = 0;
        _progress.Maximum = selected.Count;
        _progress.Value = 0;

        try
        {
            for (int index = 0; index < selected.Count; index++)
            {
                _operationCancellation.Token.ThrowIfCancellationRequested();
                FileItem item = selected[index];
                item.Status = FileStatus.Converting;
                item.StatusText = "转换中";
                _status.Text = $"正在转换 {index + 1}/{selected.Count}  ·  {item.FileName}";

                ConversionResult result = await Task.Run(
                    () => _converter.ConvertAsync(item, options, _operationCancellation.Token),
                    _operationCancellation.Token);

                if (result.Success)
                {
                    if (result.Skipped)
                    {
                        skipped++;
                        item.Status = FileStatus.Skipped;
                        item.StatusText = "无需转换";
                    }
                    else
                    {
                        succeeded++;
                        item.Status = FileStatus.Success;
                        item.StatusText = "已完成";
                        if (options.OutputMode == OutputMode.InPlace)
                        {
                            item.SourceEncoding = options.TargetEncoding;
                            item.EncodingName = options.TargetEncoding.DisplayName;
                            item.ConfidenceText = "已转换";
                            item.LineEndingName = options.LineEnding switch
                            {
                                LineEndingMode.CrLf => "CRLF",
                                LineEndingMode.Lf => "LF",
                                LineEndingMode.Cr => "CR",
                                _ => item.LineEndingName
                            };
                        }
                    }
                }
                else
                {
                    failed++;
                    item.Status = FileStatus.Failed;
                    item.StatusText = string.IsNullOrWhiteSpace(result.Error) ? "失败" : $"失败：{result.Error}";
                }

                _progress.Value = index + 1;
            }

            _status.Text = $"转换完成  ·  成功 {succeeded}  ·  跳过 {skipped}  ·  失败 {failed}";
            using ConversionResultDialog dialog = new(succeeded, skipped, failed);
            dialog.ShowDialog(this);
        }
        catch (OperationCanceledException)
        {
            foreach (FileItem item in selected.Where(item => item.Status == FileStatus.Converting))
            {
                item.Status = FileStatus.Ready;
                item.StatusText = "已取消";
            }

            _status.Text = "转换已取消";
        }
        finally
        {
            _operationCancellation.Dispose();
            _operationCancellation = null;
            SetBusy(false);
            _grid.Refresh();
        }
    }

    private async Task UpdatePreviewAsync()
    {
        _previewCancellation?.Cancel();
        _previewCancellation?.Dispose();
        _previewCancellation = new CancellationTokenSource();

        if (_grid.SelectedRows.Count == 0 || _grid.SelectedRows[0].DataBoundItem is not FileItem item)
        {
            _previewTitle.Text = "内容预览";
            _preview.Text = "选择一个文件以预览内容";
            return;
        }

        _previewTitle.Text = $"内容预览  ·  {item.FileName}";
        _preview.Text = "正在读取预览…";
        try
        {
            string text = await _detector.GetPreviewAsync(item, _previewCancellation.Token);
            if (!_previewCancellation.IsCancellationRequested)
            {
                _preview.Text = text;
                _preview.SelectionStart = 0;
                _preview.ScrollToCaret();
            }
        }
        catch (OperationCanceledException)
        {
            // Selecting another row cancels the previous preview.
        }
        catch (Exception exception)
        {
            _preview.Text = $"无法预览：{exception.Message}";
        }
    }

    private void SetBusy(bool busy, string? statusText = null)
    {
        _busy = busy;
        _addFilesButton.Enabled = !busy;
        _addFolderButton.Enabled = !busy;
        _removeButton.Enabled = !busy && _files.Count > 0;
        _clearButton.Enabled = !busy;
        _convertButton.Enabled = busy || _files.Count > 0;
        _convertButton.Text = busy ? "取消" : "开始转换";
        _convertButton.ButtonStyle = busy ? ModernButtonStyle.Danger : ModernButtonStyle.Primary;
        _options.Enabled = !busy;
        _progress.Visible = busy;
        if (busy)
        {
            _progress.Value = 0;
            _status.Text = statusText ?? "处理中…";
        }
    }

    private void UpdateFileState()
    {
        int selected = _files.Count;
        _fileCount.Text = _files.Count == 0
            ? "0 个文件"
            : $"{_files.Count} 个文件";
        _emptyState.Visible = false;
        _grid.Visible = true;
        _convertButton.Enabled = !_busy && selected > 0;
        _clearButton.Enabled = !_busy && _files.Count > 0;
        _removeButton.Enabled = !_busy && _files.Count > 0;
    }

    private void RemoveHighlightedFiles()
    {
        if (_busy)
        {
            return;
        }

        FileItem[] items = _grid.SelectedRows.Cast<DataGridViewRow>()
            .Select(row => row.DataBoundItem)
            .OfType<FileItem>()
            .Distinct()
            .ToArray();
        if (items.Length == 0)
        {
            items = _files.Where(file => file.Selected).ToArray();
        }

        foreach (FileItem item in items)
        {
            _files.Remove(item);
        }
    }

    private void ClearFiles()
    {
        if (!_busy)
        {
            _files.Clear();
            _status.Text = "列表已清空";
        }
    }

    private void ShowSettingsDialog()
    {
        using Form dialog = new()
        {
            Text = "转换设置",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.Sizable,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            BackColor = Theme.Window,
            ForeColor = Theme.Text,
            Font = Theme.Font(),
            ClientSize = new Size(420, 680),
            MinimumSize = new Size(380, 560)
        };
        _options.Dock = DockStyle.Fill;
        dialog.Controls.Add(_options);
        dialog.FormClosing += (_, _) =>
        {
            _options.SaveSettings(_settings);
            dialog.Controls.Remove(_options);
        };
        dialog.Shown += (_, _) => WindowEffects.Apply(dialog.Handle);
        dialog.ShowDialog(this);
    }

    private ContextMenuStrip CreateGridContextMenu()
    {
        ContextMenuStrip menu = new()
        {
            Font = Theme.Font(9F),
            ShowImageMargin = false,
            BackColor = Theme.Card,
            ForeColor = Theme.Text
        };
        ToolStripMenuItem sourceEncoding = new("指定源编码");
        AddEncodingGroup(sourceEncoding, "Unicode", option => option.Id.StartsWith("utf-", StringComparison.Ordinal));
        AddEncodingGroup(sourceEncoding, "中日韩", option => option.Id is "gb18030" or "big5" or "shift-jis" or "euc-jp" or "euc-kr");
        AddEncodingGroup(sourceEncoding, "Windows", option => option.Id.StartsWith("windows-", StringComparison.Ordinal));
        AddEncodingGroup(sourceEncoding, "ISO / 其他", option =>
            !option.Id.StartsWith("utf-", StringComparison.Ordinal) &&
            !option.Id.StartsWith("windows-", StringComparison.Ordinal) &&
            option.Id is not ("gb18030" or "big5" or "shift-jis" or "euc-jp" or "euc-kr"));

        ToolStripMenuItem reveal = new("在文件资源管理器中显示");
        reveal.Click += (_, _) => RevealCurrentFile();
        ToolStripMenuItem remove = new("从列表移除");
        remove.Click += (_, _) => RemoveHighlightedFiles();

        menu.Items.AddRange([sourceEncoding, new ToolStripSeparator(), reveal, remove]);
        return menu;
    }

    private void AddEncodingGroup(ToolStripMenuItem parent, string title, Func<EncodingOption, bool> predicate)
    {
        ToolStripMenuItem group = new(title);
        foreach (EncodingOption option in EncodingCatalog.All.Where(predicate))
        {
            ToolStripMenuItem item = new(option.DisplayName) { Tag = option };
            item.Click += (_, _) => ApplyManualEncoding((EncodingOption)item.Tag!);
            group.DropDownItems.Add(item);
        }

        parent.DropDownItems.Add(group);
    }

    private void ApplyManualEncoding(EncodingOption option)
    {
        foreach (DataGridViewRow row in _grid.SelectedRows)
        {
            if (row.DataBoundItem is FileItem item)
            {
                item.SetManualEncoding(option);
            }
        }

        _grid.Refresh();
        _ = UpdatePreviewAsync();
    }

    private void RevealCurrentFile()
    {
        if (_grid.CurrentRow?.DataBoundItem is not FileItem item)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.FullPath}\"") { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "CharsetFlow", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex >= 0 && _grid.Columns[e.ColumnIndex].Name == "RowNumber")
        {
            e.Value = (e.RowIndex + 1).ToString();
            e.FormattingApplied = true;
            return;
        }

        if (e.RowIndex < 0 || _grid.Rows[e.RowIndex].DataBoundItem is not FileItem item)
        {
            return;
        }

        if (_grid.Columns[e.ColumnIndex].DataPropertyName == nameof(FileItem.FullPath))
        {
            e.Value = $"{item.FileName}    {item.Folder}";
            e.FormattingApplied = true;
            return;
        }

        if (_grid.Columns[e.ColumnIndex].DataPropertyName == nameof(FileItem.StatusText))
        {
            e.CellStyle.ForeColor = item.Status switch
            {
                FileStatus.Success => Theme.Success,
                FileStatus.Failed => Theme.Danger,
                FileStatus.Unknown => Theme.Warning,
                FileStatus.Converting => Theme.Accent,
                _ => Theme.Muted
            };
            e.CellStyle.Font = Theme.Font(8.5F, item.Status is FileStatus.Success or FileStatus.Failed
                ? FontStyle.Bold
                : FontStyle.Regular);
        }
    }

    private void Grid_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right && e.RowIndex >= 0 && !_grid.Rows[e.RowIndex].Selected)
        {
            _grid.ClearSelection();
            _grid.Rows[e.RowIndex].Selected = true;
            _grid.CurrentCell = _grid.Rows[e.RowIndex].Cells[Math.Max(1, e.ColumnIndex)];
        }
    }

    private void MainForm_DragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private async void MainForm_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
        {
            await AddPathsAsync(paths);
        }
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete && !_busy)
        {
            RemoveHighlightedFiles();
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.A && _grid.Focused)
        {
            _grid.SelectAll();
            e.Handled = true;
        }
    }

    private static DataGridViewTextBoxColumn CreateTextColumn(
        string header,
        string property,
        int width,
        DataGridViewAutoSizeColumnMode autoSizeMode = DataGridViewAutoSizeColumnMode.None,
        float fillWeight = 100F) => new()
        {
            HeaderText = header,
            DataPropertyName = property,
            Width = width,
            MinimumWidth = Math.Min(width, 60),
            AutoSizeMode = autoSizeMode,
            FillWeight = fillWeight,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.Automatic
        };

    private static ModernButton CreateButton(string text, ModernButtonStyle style, int width) => new()
    {
        Text = text,
        ButtonStyle = style,
        Width = width
    };

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
