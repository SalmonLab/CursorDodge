using System.Windows.Forms;

namespace CursorDodge;

internal sealed class SettingsForm : Form
{
    private readonly CursorDodgeContext _context;

    private readonly NumericUpDown _distanceInput;
    private readonly NumericUpDown _angleInput;
    private readonly NumericUpDown _frameRateInput;
    private readonly NumericUpDown _moveDurationInput;
    private readonly NumericUpDown _armTimeoutInput;

    public SettingsForm(CursorDodgeContext context)
    {
        _context = context;

        Text = "CursorDodge 設定";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        var settings = _context.CurrentSettings;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(12),
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        for (int i = 0; i < 6; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(LabelFor("移動量（px）"), 0, 0);
        _distanceInput = NumInputInt(10, 3000, settings.DistancePx);
        layout.Controls.Add(_distanceInput, 1, 0);

        layout.Controls.Add(LabelFor("角度（上方向=0°, 右=90°）"), 0, 1);
        _angleInput = NumInputDouble(-360m, 360m, settings.AngleDegrees);
        layout.Controls.Add(_angleInput, 1, 1);

        layout.Controls.Add(LabelFor("フレームレート（fps）"), 0, 2);
        _frameRateInput = NumInputInt(10, 240, settings.FrameRate);
        layout.Controls.Add(_frameRateInput, 1, 2);

        layout.Controls.Add(LabelFor("移動時間（ms）"), 0, 3);
        _moveDurationInput = NumInputInt(30, 3000, settings.MoveDurationMs);
        layout.Controls.Add(_moveDurationInput, 1, 3);

        layout.Controls.Add(LabelFor("クリック後反応待機（ms）"), 0, 4);
        _armTimeoutInput = NumInputInt(50, 3000, settings.ArmTimeoutMs);
        layout.Controls.Add(_armTimeoutInput, 1, 4);

        var buttonPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 16, 0, 0)
        };

        var saveButton = new Button { Text = "保存", AutoSize = true };
        saveButton.Click += OnSaveClicked;

        var cancelButton = new Button { Text = "キャンセル", AutoSize = true };
        cancelButton.Click += (_, _) => Close();

        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Controls.Add(cancelButton);
        layout.Controls.Add(buttonPanel, 0, 5);
        layout.SetColumnSpan(buttonPanel, 2);

        Controls.Add(layout);
        AutoSize = true;
        AcceptButton = saveButton;
        CancelButton = cancelButton;
        MinimumSize = new Size(420, 250);
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        var next = new AppSettings
        {
            DistancePx = (int)_distanceInput.Value,
            AngleDegrees = (double)_angleInput.Value,
            FrameRate = (int)_frameRateInput.Value,
            MoveDurationMs = (int)_moveDurationInput.Value,
            ArmTimeoutMs = (int)_armTimeoutInput.Value
        };

        _context.ApplySettings(next);
        DialogResult = DialogResult.OK;
        Close();
    }

    private static Label LabelFor(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static NumericUpDown NumInputInt(int min, int max, int initial)
    {
        return new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Increment = 1,
            DecimalPlaces = 0,
            Value = ClampDecimal(initial, min, max),
            Dock = DockStyle.Fill
        };
    }

    private static NumericUpDown NumInputDouble(decimal min, decimal max, double initial)
    {
        return new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Increment = 0.1M,
            DecimalPlaces = 1,
            Value = ClampDecimal((decimal)initial, min, max),
            Dock = DockStyle.Fill
        };
    }

    private static decimal ClampDecimal(decimal value, int min, int max)
    {
        return Math.Clamp(value, min, max);
    }

    private static decimal ClampDecimal(decimal value, decimal min, decimal max)
    {
        return Math.Clamp(value, min, max);
    }
}
