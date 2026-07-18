namespace CharsetFlow.UI;

internal sealed class ConversionResultDialog : Form
{
    public ConversionResultDialog(int succeeded, int skipped, int failed)
    {
        Text = "CharsetFlow";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(380, 180);
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Font = Theme.Font();

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = Theme.Window
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.Controls.Add(CreateMessage(succeeded, skipped, failed), 0, 0);
        root.Controls.Add(CreateFooter(), 0, 1);
        Controls.Add(root);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        WindowEffects.Apply(Handle);
    }

    private static Control CreateMessage(int succeeded, int skipped, int failed)
    {
        TableLayoutPanel content = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(22, 20, 22, 12),
            BackColor = Theme.Window
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        PictureBox icon = new()
        {
            Size = new Size(32, 32),
            Margin = new Padding(0, 7, 0, 0),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            SizeMode = PictureBoxSizeMode.StretchImage,
            Image = (failed == 0 ? SystemIcons.Information : SystemIcons.Warning).ToBitmap()
        };

        TableLayoutPanel message = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            BackColor = Theme.Window
        };
        message.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
        message.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        message.Controls.Add(new Label
        {
            Text = "处理完成",
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Text,
            Font = Theme.Font(10.5F, FontStyle.Bold)
        }, 0, 0);
        message.Controls.Add(new Label
        {
            Text = $"成功：{succeeded}\r\n无需转换：{skipped}\r\n失败：{failed}",
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Theme.Text,
            Font = Theme.Font(9.5F)
        }, 0, 1);

        content.Controls.Add(icon, 0, 0);
        content.Controls.Add(message, 1, 0);
        return content;
    }

    private Control CreateFooter()
    {
        TableLayoutPanel footer = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0, 8, 0, 8),
            BackColor = Theme.Subtle,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        ModernButton confirm = new()
        {
            Text = "确定",
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            ButtonStyle = ModernButtonStyle.Secondary,
            DialogResult = DialogResult.OK
        };
        AcceptButton = confirm;
        CancelButton = confirm;
        footer.Controls.Add(confirm, 1, 0);
        return footer;
    }
}
