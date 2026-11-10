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
    private readonly ConversionOptionsControl _options = new();
    private readonly DataGridView _grid = new();
    private readonly Label _emptyState = new();
    private readonly Label _fileCount = new();
    private readonly Label _status = new();
    private readonly SlimProgressBar _progress = new();
    private readonly Button _addFilesButton = Theme.CreateButton("添加文件");
    private readonly Button _addFolderButton = Theme.CreateButton("添加文件夹");
    private readonly Button _removeButton = Theme.CreateButton("移除");
    private readonly Button _clearButton = Theme.CreateButton("清空");
    private readonly Button _convertButton = Theme.CreateButton("开始转换", primary: true);

    private CancellationTokenSource? _operationCancellation;
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
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(700, 360);
        ClientSize = new Size(980, 560);
        KeyPreview = true;
        AllowDrop = true;

        SuspendLayout();
        BuildLayout();
        ConfigureGrid();
        WireEvents();
        _options.LoadSettings(_settings);
        UpdateFileState();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ResumeLayout(true);
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
        ApplyGridMetrics();

        string[] paths = _startupArguments.Where(path => File.Exists(path) || Directory.Exists(path)).ToArray();
        if (paths.Length > 0)
        {
            BeginInvoke(async () => await AddPathsAsync(paths));
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _operationCancellation?.Cancel();
        SaveSettings();
        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            WindowState = FormWindowState.Minimized;
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
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
            RowCount = 5,
            Margin = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Panel list = new() { Dock = DockStyle.Fill, Margin = Padding.Empty };
        _grid.Dock = DockStyle.Fill;
        _emptyState.Dock = DockStyle.Fill;
        _emptyState.Text = "拖放文件或文件夹";
        _emptyState.TextAlign = ContentAlignment.MiddleCenter;
        _emptyState.ForeColor = Theme.Muted;
        list.Controls.Add(_grid);
        list.Controls.Add(_emptyState);

        _options.Dock = DockStyle.Top;
        _options.Margin = Padding.Empty;
        _progress.Dock = DockStyle.Top;
        _progress.Margin = Padding.Empty;
        _progress.Visible = false;
        root.Controls.Add(CreateToolbar(), 0, 0);
        root.Controls.Add(_options, 0, 1);
        root.Controls.Add(list, 0, 2);
        root.Controls.Add(_progress, 0, 3);
        root.Controls.Add(CreateStatusBar(), 0, 4);
        Controls.Add(root);
    }

    private Control CreateToolbar()
    {
        TableLayoutPanel toolbar = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(16, 10, 16, 10)
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        FlowLayoutPanel actions = new()
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            WrapContents = false,
            Margin = Padding.Empty
        };
        foreach (Button button in new[] { _addFilesButton, _addFolderButton, _removeButton, _clearButton })
        {
            button.Margin = new Padding(0, 0, 8, 0);
            actions.Controls.Add(button);
        }

        _convertButton.Margin = Padding.Empty;
        _convertButton.MinimumSize = new Size(104, 0);
        toolbar.Controls.Add(actions, 0, 0);
        toolbar.Controls.Add(_convertButton, 1, 0);
        return toolbar;
    }

    private Control CreateStatusBar()
    {
        TableLayoutPanel footer = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Subtle,
            Margin = Padding.Empty,
            Padding = new Padding(16, 6, 16, 6)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _status.Dock = DockStyle.Fill;
        _status.Margin = new Padding(0, 0, 12, 0);
        _status.AutoEllipsis = true;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.ForeColor = Theme.Muted;
        _status.UseMnemonic = false;
        _fileCount.AutoSize = true;
        _fileCount.Margin = Padding.Empty;
        _fileCount.ForeColor = Theme.Muted;
        footer.Controls.Add(_status, 0, 0);
        footer.Controls.Add(_fileCount, 1, 0);
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
        _grid.ReadOnly = true;
        _grid.BackgroundColor = Theme.Window;
        _grid.BorderStyle = BorderStyle.None;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.GridColor = Theme.Border;
        _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = true;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Subtle,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.Subtle,
            Alignment = DataGridViewContentAlignment.MiddleLeft
        };
        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Window,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.AccentLight,
            SelectionForeColor = Theme.Text
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "RowNumber",
            HeaderText = "序号",
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(CreateTextColumn("文件 / 路径", nameof(FileItem.FullPath), DataGridViewAutoSizeColumnMode.Fill));
        _grid.Columns.Add(CreateTextColumn("编码", nameof(FileItem.EncodingName)));
        _grid.Columns.Add(CreateTextColumn("换行符", nameof(FileItem.LineEndingName)));
        _grid.Columns.Add(CreateTextColumn("状态", nameof(FileItem.StatusText)));
        _grid.ContextMenuStrip = CreateGridContextMenu();
    }

    private void ApplyGridMetrics()
    {
        int Scale(int value) => _grid.LogicalToDeviceUnits(value);
        using Graphics graphics = _grid.CreateGraphics();
        int textHeight = TextRenderer.MeasureText(graphics, "文件", _grid.Font).Height;
        int rowHeight = Math.Max(Scale(32), textHeight + Scale(8));
        _grid.RowTemplate.Height = rowHeight;
        foreach (DataGridViewRow row in _grid.Rows)
        {
            row.Height = rowHeight;
        }

        Padding padding = new(Scale(8), Scale(4), Scale(8), Scale(4));
        _grid.DefaultCellStyle.Padding = padding;
        _grid.ColumnHeadersDefaultCellStyle.Padding = padding;
        _grid.Columns[0].MinimumWidth = Scale(58);
        _grid.Columns[0].Width = Scale(58);
        _grid.Columns[1].MinimumWidth = Scale(160);
        _grid.Columns[2].MinimumWidth = Scale(80);
        _grid.Columns[2].Width = Scale(130);
        _grid.Columns[3].MinimumWidth = Scale(64);
        _grid.Columns[3].Width = Scale(86);
        _grid.Columns[4].MinimumWidth = Scale(64);
        _grid.Columns[4].Width = Scale(132);
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
        _grid.DpiChangedAfterParent += (_, _) => ApplyGridMetrics();
        _grid.CellFormatting += Grid_CellFormatting;
        _grid.CellToolTipTextNeeded += Grid_CellToolTipTextNeeded;
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

    private void SetBusy(bool busy, string? statusText = null)
    {
        _busy = busy;
        _addFilesButton.Enabled = !busy;
        _addFolderButton.Enabled = !busy;
        UpdateFileState();
        _convertButton.Text = busy ? "取消" : "开始转换";
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
        bool hasFiles = _files.Count > 0;
        _fileCount.Text = $"{_files.Count} 个文件";
        _emptyState.Visible = !hasFiles;
        _grid.Visible = hasFiles;
        _convertButton.Enabled = _busy || hasFiles;
        _clearButton.Enabled = !_busy && hasFiles;
        _removeButton.Enabled = !_busy && hasFiles;
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

    private ContextMenuStrip CreateGridContextMenu()
    {
        ContextMenuStrip menu = new()
        {
            Font = Font,
            ShowImageMargin = false,
            BackColor = Theme.Window,
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
        }
    }

    private void Grid_CellToolTipTextNeeded(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || _grid.Rows[e.RowIndex].DataBoundItem is not FileItem item)
        {
            return;
        }

        e.ToolTipText = _grid.Columns[e.ColumnIndex].DataPropertyName switch
        {
            nameof(FileItem.FullPath) => item.FullPath,
            nameof(FileItem.StatusText) when item.Status == FileStatus.Failed => item.StatusText,
            _ => e.ToolTipText
        };
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
        if (e.KeyCode == Keys.Delete && !_busy && _grid.ContainsFocus)
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
        DataGridViewAutoSizeColumnMode autoSizeMode = DataGridViewAutoSizeColumnMode.None) => new()
        {
            HeaderText = header,
            DataPropertyName = property,
            AutoSizeMode = autoSizeMode,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.Automatic
        };
}
