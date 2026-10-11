using System;
using System.Drawing;
using System.Windows.Forms;

static class ProbeProgram
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var form = new ProbeForm();
        form.Show();
        Console.WriteLine($"DeviceDpi={form.DeviceDpi}");
        Console.WriteLine($"Table row0 before={form.Row0Height} labelHeightBefore={form.LabelHeight} formWidthBefore={form.Width}");
        form.ApplyManualScale(1.25f);
        Console.WriteLine($"Table row0 after={form.Row0Height} labelHeightAfter={form.LabelHeight} formWidthAfter={form.Width}");
        Console.WriteLine($"Font size after={form.LabelFont.SizeInPoints}");
    }
}

sealed class ProbeForm : Form
{
    private readonly TableLayoutPanel _table = new()
    {
        Dock = DockStyle.Fill,
        ColumnCount = 1,
        RowCount = 2,
        Padding = new Padding(18, 14, 18, 10)
    };
    private readonly Label _label = new()
    {
        Dock = DockStyle.Top,
        Height = 30,
        Font = new Font("Segoe UI", 10),
        Text = "probe"
    };
    private readonly Label _free = new()
    {
        Location = new Point(50, 50),
        Size = new Size(200, 24),
        Font = new Font("Segoe UI", 10),
        Text = "free"
    };

    public ProbeForm()
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        _table.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        _table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _table.Controls.Add(_label, 0, 0);
        Controls.Add(_table);
        Controls.Add(_free);
        Width = 800;
    }

    public float Row0Height => _table.RowStyles[0].Height;
    public int LabelHeight => _label.Height;
    public Font LabelFont => _label.Font;
    public Point FreeLocation => _free.Location;

    // Riproduce esattamente ciò che fa l'AutoScale quando il DPI reale != 96.
    public void ApplyManualScale(float factor) =>
        ScaleControl(new SizeF(factor, factor), BoundsSpecified.All);
}
