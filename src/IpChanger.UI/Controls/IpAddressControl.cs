namespace IpChanger.UI.Controls;

public class IpAddressControl : UserControl
{
    private readonly TextBox[] _octets = new TextBox[4];
    private readonly Label[] _dots = new Label[3];
    private readonly TableLayoutPanel _layout;
    private bool _allowEmpty;

    public IpAddressControl()
    {
        _layout = new TableLayoutPanel
        {
            ColumnCount = 7,
            RowCount = 1,
            Dock = DockStyle.Fill,
            AutoSize = true,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        // Configure columns: 4 octets + 3 dots
        for (int i = 0; i < 7; i++)
        {
            if (i % 2 == 0) // Octet columns
                _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
            else // Dot columns
                _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
        }
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        // Create octets and dots
        for (int i = 0; i < 4; i++)
        {
            _octets[i] = CreateOctetTextBox(i);
            _layout.Controls.Add(_octets[i], i * 2, 0);

            if (i < 3)
            {
                _dots[i] = new Label
                {
                    Text = ".",
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill,
                    AutoSize = false,
                    Margin = Padding.Empty,
                    Padding = Padding.Empty
                };
                _layout.Controls.Add(_dots[i], i * 2 + 1, 0);
            }
        }

        this.Controls.Add(_layout);
        this.Height = 23;
        this.MinimumSize = new Size(180, 23);
        this.AutoSize = false;
    }

    private TextBox CreateOctetTextBox(int index)
    {
        var txt = new TextBox
        {
            MaxLength = 3,
            TextAlign = HorizontalAlignment.Center,
            Dock = DockStyle.Fill,
            Margin = new Padding(1),
            Tag = index
        };

        txt.KeyPress += Octet_KeyPress;
        txt.TextChanged += Octet_TextChanged;
        txt.KeyDown += Octet_KeyDown;
        txt.Leave += Octet_Leave;

        return txt;
    }

    public bool AllowEmpty
    {
        get => _allowEmpty;
        set => _allowEmpty = value;
    }

    public override string Text
    {
        get
        {
            if (_allowEmpty && _octets.All(o => string.IsNullOrEmpty(o.Text)))
                return string.Empty;

            return string.Join(".", _octets.Select(o => o.Text));
        }
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                foreach (var octet in _octets)
                    octet.Text = string.Empty;
                return;
            }

            var parts = value.Split('.');
            for (int i = 0; i < 4 && i < parts.Length; i++)
            {
                _octets[i].Text = parts[i].Trim();
            }
        }
    }

    public bool IsValid
    {
        get
        {
            // If AllowEmpty and all fields are empty, it's valid
            if (_allowEmpty && _octets.All(o => string.IsNullOrEmpty(o.Text)))
                return true;

            // All octets must be valid 0-255 values
            foreach (var octet in _octets)
            {
                if (!int.TryParse(octet.Text, out int val) || val < 0 || val > 255)
                    return false;
            }
            return true;
        }
    }

    public new bool Enabled
    {
        get => base.Enabled;
        set
        {
            base.Enabled = value;
            foreach (var octet in _octets)
                octet.Enabled = value;
        }
    }

    private void Octet_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (sender is not TextBox txt) return;
        int index = (int)txt.Tag!;

        // Allow control characters (backspace, etc.)
        if (char.IsControl(e.KeyChar))
            return;

        // Handle dot/period - move to next octet
        if (e.KeyChar == '.')
        {
            e.Handled = true;
            if (index < 3 && !string.IsNullOrEmpty(txt.Text))
            {
                _octets[index + 1].Focus();
                _octets[index + 1].SelectAll();
            }
            return;
        }

        // Only allow digits
        if (!char.IsDigit(e.KeyChar))
        {
            e.Handled = true;
            return;
        }

        // Prevent values > 255
        string newText = txt.SelectedText.Length > 0
            ? txt.Text.Remove(txt.SelectionStart, txt.SelectionLength).Insert(txt.SelectionStart, e.KeyChar.ToString())
            : txt.Text.Insert(txt.SelectionStart, e.KeyChar.ToString());

        if (int.TryParse(newText, out int val) && val > 255)
        {
            e.Handled = true;
        }
    }

    private void Octet_TextChanged(object? sender, EventArgs e)
    {
        if (sender is not TextBox txt) return;
        int index = (int)txt.Tag!;

        // Update validation visual
        UpdateValidationVisual(txt);

        // Auto-advance when 3 digits entered or value can't be extended
        if (txt.Text.Length == 3 && index < 3)
        {
            _octets[index + 1].Focus();
            _octets[index + 1].SelectAll();
        }
        else if (txt.Text.Length >= 2 && index < 3)
        {
            // Auto-advance if adding another digit would exceed 255
            if (int.TryParse(txt.Text, out int val) && val > 25)
            {
                _octets[index + 1].Focus();
                _octets[index + 1].SelectAll();
            }
        }
    }

    private void Octet_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox txt) return;
        int index = (int)txt.Tag!;

        // Handle Ctrl+V paste
        if (e.Control && e.KeyCode == Keys.V)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            HandlePaste();
            return;
        }

        // Backspace at start of field - go to previous field
        if (e.KeyCode == Keys.Back && txt.SelectionStart == 0 && txt.SelectionLength == 0 && index > 0)
        {
            _octets[index - 1].Focus();
            _octets[index - 1].SelectionStart = _octets[index - 1].Text.Length;
            e.Handled = true;
            return;
        }

        // Left arrow at start - go to previous field
        if (e.KeyCode == Keys.Left && txt.SelectionStart == 0 && index > 0)
        {
            _octets[index - 1].Focus();
            _octets[index - 1].SelectionStart = _octets[index - 1].Text.Length;
            e.Handled = true;
            return;
        }

        // Right arrow at end - go to next field
        if (e.KeyCode == Keys.Right && txt.SelectionStart == txt.Text.Length && index < 3)
        {
            _octets[index + 1].Focus();
            _octets[index + 1].SelectionStart = 0;
            e.Handled = true;
            return;
        }
    }

    private void Octet_Leave(object? sender, EventArgs e)
    {
        if (sender is not TextBox txt) return;
        UpdateValidationVisual(txt);
    }

    private void UpdateValidationVisual(TextBox txt)
    {
        bool valid = string.IsNullOrEmpty(txt.Text) ||
                     (int.TryParse(txt.Text, out int val) && val >= 0 && val <= 255);

        txt.BackColor = valid ? SystemColors.Window : Color.MistyRose;
    }

    private void HandlePaste()
    {
        if (!Clipboard.ContainsText()) return;

        string text = Clipboard.GetText().Trim();

        // Try to parse as IP address
        if (System.Net.IPAddress.TryParse(text, out var ip))
        {
            this.Text = ip.ToString();
            _octets[3].Focus();
            _octets[3].SelectionStart = _octets[3].Text.Length;
        }
    }

    public void Clear()
    {
        foreach (var octet in _octets)
            octet.Text = string.Empty;
    }

    public new void Focus()
    {
        _octets[0].Focus();
        _octets[0].SelectAll();
    }
}
