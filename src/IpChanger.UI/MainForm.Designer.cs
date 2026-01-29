using System.Reflection;
using IpChanger.UI.Controls;

namespace IpChanger.UI;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private IpAddressControl AddIpField(TableLayoutPanel layout, ref int row, string labelText, bool allowEmpty)
    {
        var label = new Label
        {
            Text = labelText,
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 0)
        };
        layout.Controls.Add(label, 0, row);

        var control = new IpAddressControl
        {
            AllowEmpty = allowEmpty,
            Anchor = AnchorStyles.Left,
            Width = 200
        };
        layout.Controls.Add(control, 1, row);
        row++;
        return control;
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        this.Text = "IPCT - IP Change Tool";
        this.Size = new Size(460, 400);
        this.MinimumSize = new Size(440, 380);
        this.FormBorderStyle = FormBorderStyle.Sizable;
        this.MaximizeBox = true;

        // Set form icon
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "IPCT.ico");
        if (File.Exists(iconPath))
            this.Icon = new Icon(iconPath);

        toolTip = new ToolTip(components);

        // Main layout
        mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 10,
            Padding = new Padding(15, 10, 15, 10),
            AutoSize = false
        };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));  // Row 0: Adapter
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));  // Row 1: MAC Address
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));  // Row 2: Physical only checkbox
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));  // Row 3: DHCP checkbox
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));  // Row 4: IP Address
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));  // Row 5: Subnet Mask
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));  // Row 6: Gateway
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));  // Row 7: Primary DNS
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));  // Row 8: Secondary DNS
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));  // Row 9: Buttons

        int row = 0;

        // Adapter row
        mainLayout.Controls.Add(new Label { Text = "Adapter:", Anchor = AnchorStyles.Left, AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, row);

        cmbAdapters = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbAdapters.SelectedIndexChanged += cmbAdapters_SelectedIndexChanged;
        mainLayout.Controls.Add(cmbAdapters, 1, row++);

        // MAC Address label
        lblMacAddress = new Label { Text = "MAC: N/A", Anchor = AnchorStyles.Left, AutoSize = true, ForeColor = Color.DimGray };
        mainLayout.SetColumnSpan(lblMacAddress, 2);
        mainLayout.Controls.Add(lblMacAddress, 0, row++);

        // Physical only checkbox - spans both columns
        chkPhysicalOnly = new CheckBox { Text = "Physical Adapters Only", Checked = true, Anchor = AnchorStyles.Left, AutoSize = true };
        chkPhysicalOnly.CheckedChanged += (s, e) => LoadAdapters();
        mainLayout.SetColumnSpan(chkPhysicalOnly, 2);
        mainLayout.Controls.Add(chkPhysicalOnly, 0, row++);

        // DHCP checkbox - spans both columns
        chkDhcp = new CheckBox { Text = "Obtain IP Automatically (DHCP)", Anchor = AnchorStyles.Left, AutoSize = true };
        chkDhcp.CheckedChanged += chkDhcp_CheckedChanged;
        mainLayout.SetColumnSpan(chkDhcp, 2);
        mainLayout.Controls.Add(chkDhcp, 0, row++);

        // IP fields using helper
        ipAddressControl = AddIpField(mainLayout, ref row, "IP Address:", allowEmpty: false);
        subnetMaskControl = AddIpField(mainLayout, ref row, "Subnet Mask:", allowEmpty: false);
        gatewayControl = AddIpField(mainLayout, ref row, "Gateway:", allowEmpty: true);
        primaryDnsControl = AddIpField(mainLayout, ref row, "Primary DNS:", allowEmpty: true);
        secondaryDnsControl = AddIpField(mainLayout, ref row, "Secondary DNS:", allowEmpty: true);

        // Buttons
        var buttonPanel = new FlowLayoutPanel { Anchor = AnchorStyles.Left, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 5, 0, 0) };
        btnApply = new Button { Text = "Apply Settings", Width = 100, Height = 35 };
        btnApply.Click += btnApply_Click;
        btnCopy = new Button { Text = "Copy Config", Width = 95, Height = 35 };
        btnCopy.Click += btnCopy_Click;
        btnClear = new Button { Text = "Clear Settings", Width = 100, Height = 35 };
        btnClear.Click += btnClear_Click;
        btnRefresh = new Button { Text = "Refresh", Width = 75, Height = 35 };
        btnRefresh.Click += (s, e) => LoadAdapters();
        buttonPanel.Controls.Add(btnApply);
        buttonPanel.Controls.Add(btnCopy);
        buttonPanel.Controls.Add(btnClear);
        buttonPanel.Controls.Add(btnRefresh);
        mainLayout.SetColumnSpan(buttonPanel, 2);
        mainLayout.Controls.Add(buttonPanel, 0, row++);

        // Status strip (combined status shown here instead of separate label)
        statusStrip = new StatusStrip();
        lblServiceStatus = new ToolStripStatusLabel { Text = "Checking Service..." };
        lblVersion = new ToolStripStatusLabel { Alignment = ToolStripItemAlignment.Right };
        statusStrip.Items.Add(lblServiceStatus);
        statusStrip.Items.Add(new ToolStripStatusLabel { Spring = true });
        statusStrip.Items.Add(lblVersion);

        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Unknown";
        lblVersion.Text = $"v{version}";

        // Tooltips
        toolTip.SetToolTip(ipAddressControl, "Enter IPv4 address (e.g., 192.168.1.100)");
        toolTip.SetToolTip(subnetMaskControl, "Enter subnet mask (e.g., 255.255.255.0)");
        toolTip.SetToolTip(gatewayControl, "Enter gateway address (optional)");
        toolTip.SetToolTip(primaryDnsControl, "Enter primary DNS server (optional)");
        toolTip.SetToolTip(secondaryDnsControl, "Enter secondary DNS server (optional)");
        toolTip.SetToolTip(chkPhysicalOnly, "Show only physical network adapters (Ethernet and Wi-Fi)");
        toolTip.SetToolTip(chkDhcp, "Enable to obtain IP address automatically from DHCP server");
        toolTip.SetToolTip(btnRefresh, "Refresh adapter list");
        toolTip.SetToolTip(btnCopy, "Copy current configuration to clipboard");
        toolTip.SetToolTip(btnClear, "Clear IP configuration fields");

        this.Controls.Add(mainLayout);
        this.Controls.Add(statusStrip);
    }
}
