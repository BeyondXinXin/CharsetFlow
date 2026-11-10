namespace CharsetFlow.UI;

internal sealed class ConversionResultDialog : Form
{
    public ConversionResultDialog(int succeeded, int skipped, int failed)
    {
        Text = "转换结果";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        TableLayoutPanel content = new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(300, 0),
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(20)
        };
        content.Controls.Add(new Label
        {
            Text = $"成功 {succeeded}   无需转换 {skipped}   失败 {failed}",
            AutoSize = true,
            Margin = Padding.Empty
        }, 0, 0);

        Button confirm = Theme.CreateButton("确定", primary: true);
        confirm.Anchor = AnchorStyles.Right;
        confirm.Margin = new Padding(0, 16, 0, 0);
        confirm.DialogResult = DialogResult.OK;
        AcceptButton = confirm;
        CancelButton = confirm;
        content.Controls.Add(confirm, 0, 1);
        Controls.Add(content);

        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
    }
}
