using System.Net.NetworkInformation;
using System.IO.Pipes;
using System.Reflection;
using System.Text.Json;
using IpChanger.Common;
using IpChanger.UI.Controls;

namespace IpChanger.UI;

public partial class MainForm : Form
{
    private ComboBox cmbAdapters = null!;
    private CheckBox chkDhcp = null!;
    private IpAddressControl ipAddressControl = null!;
    private IpAddressControl subnetMaskControl = null!;
    private IpAddressControl gatewayControl = null!;
    private IpAddressControl primaryDnsControl = null!;
    private IpAddressControl secondaryDnsControl = null!;
    private Button btnApply = null!;
    private Button btnRefresh = null!;
    private Button btnCopy = null!;
    private Label lblStatus = null!;
    private StatusStrip statusStrip = null!;
    private ToolStripStatusLabel lblServiceStatus = null!;
    private ToolStripStatusLabel lblVersion = null!;
    private ToolTip toolTip = null!;
    private System.Windows.Forms.Timer timerStatus = null!;
    private CheckBox chkPhysicalOnly = null!;

    public MainForm()
    {
        InitializeComponent();
        LoadAdapters();

        // Start connection check timer
        timerStatus = new System.Windows.Forms.Timer();
        timerStatus.Interval = 3000; // 3 seconds
        timerStatus.Tick += async (s, e) => await CheckConnectionStatus();
        timerStatus.Start();

        // Initial check
        _ = CheckConnectionStatus();
    }

    private async Task CheckConnectionStatus()
    {
        try
        {
            // Try to connect to the service without sending data
            await using var client = new NamedPipeClientStream(".", "IpChangerPipe", PipeDirection.InOut);
            await client.ConnectAsync(500); // Short timeout
            lblServiceStatus.Text = "Service: Connected";
            lblServiceStatus.ForeColor = Color.Green;
        }
        catch
        {
            lblServiceStatus.Text = "Service: Disconnected";
            lblServiceStatus.ForeColor = Color.Red;
        }
    }

    private void LoadAdapters()
    {
        cmbAdapters.Items.Clear();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            // Filter by physical adapter types when checkbox is checked (or during initial load)
            if (chkPhysicalOnly?.Checked != false)
            {
                if (nic.NetworkInterfaceType != NetworkInterfaceType.Ethernet &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Wireless80211)
                    continue;
            }
            cmbAdapters.Items.Add(new AdapterItem(nic));
        }
        if (cmbAdapters.Items.Count > 0) cmbAdapters.SelectedIndex = 0;
    }

    private bool ValidateInputs(out string errorMessage)
    {
        errorMessage = "";

        if (!chkDhcp.Checked)
        {
            // Validate IP Address
            if (!ipAddressControl.IsValid || string.IsNullOrWhiteSpace(ipAddressControl.Text) || ipAddressControl.Text == "...")
            {
                errorMessage = "Invalid IP address format.";
                return false;
            }

            // Validate Subnet Mask
            if (!subnetMaskControl.IsValid || string.IsNullOrWhiteSpace(subnetMaskControl.Text) || subnetMaskControl.Text == "...")
            {
                errorMessage = "Invalid subnet mask format.";
                return false;
            }

            // Validate Gateway (optional)
            if (!gatewayControl.IsValid)
            {
                errorMessage = "Invalid gateway address format.";
                return false;
            }

            // Validate Primary DNS (optional)
            if (!primaryDnsControl.IsValid)
            {
                errorMessage = "Invalid primary DNS server format.";
                return false;
            }

            // Validate Secondary DNS (optional)
            if (!secondaryDnsControl.IsValid)
            {
                errorMessage = "Invalid secondary DNS server format.";
                return false;
            }
        }

        return true;
    }

    private string BuildDnsString()
    {
        var dns = new List<string>();
        if (!string.IsNullOrWhiteSpace(primaryDnsControl.Text) && primaryDnsControl.Text != "...")
            dns.Add(primaryDnsControl.Text);
        if (!string.IsNullOrWhiteSpace(secondaryDnsControl.Text) && secondaryDnsControl.Text != "...")
            dns.Add(secondaryDnsControl.Text);
        return string.Join(",", dns);
    }

    private void btnApply_Click(object? sender, EventArgs e)
    {
        if (cmbAdapters.SelectedItem is not AdapterItem selected) return;

        if (!ValidateInputs(out string validationError))
        {
            MessageBox.Show(validationError, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var request = new IpConfigRequest
        {
            AdapterId = selected.Id,
            UseDhcp = chkDhcp.Checked,
            IpAddress = ipAddressControl.Text,
            SubnetMask = subnetMaskControl.Text,
            Gateway = gatewayControl.Text == "..." ? "" : gatewayControl.Text,
            Dns = BuildDnsString()
        };

        ApplySettings(request);
    }

    private async void ApplySettings(IpConfigRequest request)
    {
        btnApply.Enabled = false;
        lblStatus.Text = "Connecting to service...";
        try
        {
            await using var client = new NamedPipeClientStream(".", "IpChangerPipe", PipeDirection.InOut);
            await client.ConnectAsync(3000);

            // Use leaveOpen: true to prevent StreamWriter/StreamReader from closing the pipe prematurely
            await using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, leaveOpen: true);

            await writer.WriteLineAsync(JsonSerializer.Serialize(request));

            var responseJson = await reader.ReadLineAsync();
            if (responseJson != null)
            {
                var response = JsonSerializer.Deserialize<IpConfigResponse>(responseJson);
                lblStatus.Text = response?.Message ?? "Unknown response";
                MessageBox.Show(response?.Message, response?.Success == true ? "Success" : "Error");
            }
        }
        catch (Exception ex)
        {
            lblStatus.Text = "Error: " + ex.Message;
            MessageBox.Show("Could not connect to service. Is it installed and running?\n" + ex.Message, "Connection Error");
        }
        finally
        {
            btnApply.Enabled = true;
        }
    }

    private void chkDhcp_CheckedChanged(object? sender, EventArgs e)
    {
        bool enabled = !chkDhcp.Checked;
        ipAddressControl.Enabled = enabled;
        subnetMaskControl.Enabled = enabled;
        gatewayControl.Enabled = enabled;
        primaryDnsControl.Enabled = enabled;
        secondaryDnsControl.Enabled = enabled;
    }

    private void cmbAdapters_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (cmbAdapters.SelectedItem is AdapterItem item)
            LoadAdapterConfig(item);
    }

    private void LoadAdapterConfig(AdapterItem item)
    {
        try
        {
            var nic = item.Nic;
            var ipProps = nic.GetIPProperties();

            // Get IPv4 address and subnet mask
            var ipv4Address = ipProps.UnicastAddresses
                .FirstOrDefault(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (ipv4Address != null)
            {
                ipAddressControl.Text = ipv4Address.Address.ToString();
                subnetMaskControl.Text = ipv4Address.IPv4Mask?.ToString() ?? "255.255.255.0";
            }
            else
            {
                ipAddressControl.Text = string.Empty;
                subnetMaskControl.Text = "255.255.255.0";
            }

            // Get gateway
            var gateway = ipProps.GatewayAddresses
                .FirstOrDefault(gw => gw.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            gatewayControl.Text = gateway?.Address.ToString() ?? string.Empty;

            // Get DNS servers - split into primary and secondary
            var dnsServers = ipProps.DnsAddresses
                .Where(dns => dns.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(dns => dns.ToString())
                .ToList();

            primaryDnsControl.Text = dnsServers.Count > 0 ? dnsServers[0] : string.Empty;
            secondaryDnsControl.Text = dnsServers.Count > 1 ? dnsServers[1] : string.Empty;

            // Check DHCP status
            try
            {
                var ipv4Props = ipProps.GetIPv4Properties();
                chkDhcp.Checked = ipv4Props.IsDhcpEnabled;
            }
            catch
            {
                // Some adapters may not support IPv4 properties
                chkDhcp.Checked = false;
            }
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"Error loading config: {ex.Message}";
        }
    }

    private void btnCopy_Click(object? sender, EventArgs e)
    {
        var adapter = cmbAdapters.SelectedItem as AdapterItem;
        var dnsText = BuildDnsString();
        var config = $"Adapter: {adapter?.Name ?? "None"}\n" +
                     $"DHCP: {(chkDhcp.Checked ? "Enabled" : "Disabled")}\n" +
                     $"IP Address: {ipAddressControl.Text}\n" +
                     $"Subnet Mask: {subnetMaskControl.Text}\n" +
                     $"Gateway: {gatewayControl.Text}\n" +
                     $"Primary DNS: {primaryDnsControl.Text}\n" +
                     $"Secondary DNS: {secondaryDnsControl.Text}";

        Clipboard.SetText(config);
        lblStatus.Text = "Configuration copied to clipboard.";
    }

    private class AdapterItem
    {
        public string Name { get; }
        public string Id { get; }
        public NetworkInterface Nic { get; }
        public AdapterItem(NetworkInterface nic)
        {
            Nic = nic;
            Name = $"{nic.Name} ({nic.Description})";
            Id = nic.Id;
        }
        public override string ToString() => Name;
    }

    private void InitializeComponent()
    {
        this.Text = "IPCT - IP Change Tool";
        this.Size = new Size(460, 400);
        this.MinimumSize = new Size(440, 380);
        this.FormBorderStyle = FormBorderStyle.Sizable;
        this.MaximizeBox = true;

        // Set form icon from embedded resource
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "IPCT.ico");
        if (File.Exists(iconPath))
        {
            this.Icon = new Icon(iconPath);
        }

        // Initialize tooltip
        toolTip = new ToolTip();

        // Main TableLayoutPanel
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 10,
            Padding = new Padding(15, 10, 15, 10),
            AutoSize = false
        };

        // Configure columns: Label (100px) | Control (fill)
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Configure rows - no extra fill row at the end
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // Adapter row
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); // Physical only checkbox
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // DHCP checkbox
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // IP Address
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Subnet Mask
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Gateway
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Primary DNS
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // Secondary DNS
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); // Buttons
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); // Status

        int row = 0;

        // Row 0: Adapter
        var lblAdapter = new Label
        {
            Text = "Adapter:",
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            Padding = new Padding(0, 6, 0, 0)
        };
        mainLayout.Controls.Add(lblAdapter, 0, row);

        var adapterPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        adapterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // ComboBox fills available space
        adapterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));  // Button fixed width
        adapterPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        cmbAdapters = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 0, 5, 0)
        };
        cmbAdapters.SelectedIndexChanged += cmbAdapters_SelectedIndexChanged;

        btnRefresh = new Button { Text = "Refresh", Dock = DockStyle.Fill };
        btnRefresh.Click += (s, e) => LoadAdapters();

        adapterPanel.Controls.Add(cmbAdapters, 0, 0);
        adapterPanel.Controls.Add(btnRefresh, 1, 0);
        mainLayout.Controls.Add(adapterPanel, 1, row);
        row++;

        // Row 1: Physical Adapters Only checkbox
        chkPhysicalOnly = new CheckBox
        {
            Text = "Physical Adapters Only",
            Checked = true,
            Anchor = AnchorStyles.Left,
            AutoSize = true
        };
        chkPhysicalOnly.CheckedChanged += (s, e) => LoadAdapters();
        mainLayout.Controls.Add(chkPhysicalOnly, 1, row);
        row++;

        // Row 2: DHCP checkbox
        chkDhcp = new CheckBox
        {
            Text = "Obtain IP Automatically (DHCP)",
            Anchor = AnchorStyles.Left,
            AutoSize = true
        };
        chkDhcp.CheckedChanged += chkDhcp_CheckedChanged;
        mainLayout.Controls.Add(chkDhcp, 1, row);
        row++;

        // Row 3: IP Address
        var lblIp = new Label
        {
            Text = "IP Address:",
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 0)
        };
        mainLayout.Controls.Add(lblIp, 0, row);

        ipAddressControl = new IpAddressControl
        {
            AllowEmpty = false,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        mainLayout.Controls.Add(ipAddressControl, 1, row);
        row++;

        // Row 4: Subnet Mask
        var lblSubnet = new Label
        {
            Text = "Subnet Mask:",
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 0)
        };
        mainLayout.Controls.Add(lblSubnet, 0, row);

        subnetMaskControl = new IpAddressControl
        {
            AllowEmpty = false,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        mainLayout.Controls.Add(subnetMaskControl, 1, row);
        row++;

        // Row 5: Gateway
        var lblGateway = new Label
        {
            Text = "Gateway:",
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 0)
        };
        mainLayout.Controls.Add(lblGateway, 0, row);

        gatewayControl = new IpAddressControl
        {
            AllowEmpty = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        mainLayout.Controls.Add(gatewayControl, 1, row);
        row++;

        // Row 6: Primary DNS
        var lblPrimaryDns = new Label
        {
            Text = "Primary DNS:",
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 0)
        };
        mainLayout.Controls.Add(lblPrimaryDns, 0, row);

        primaryDnsControl = new IpAddressControl
        {
            AllowEmpty = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        mainLayout.Controls.Add(primaryDnsControl, 1, row);
        row++;

        // Row 7: Secondary DNS
        var lblSecondaryDns = new Label
        {
            Text = "Secondary DNS:",
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            Padding = new Padding(0, 4, 0, 0)
        };
        mainLayout.Controls.Add(lblSecondaryDns, 0, row);

        secondaryDnsControl = new IpAddressControl
        {
            AllowEmpty = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        mainLayout.Controls.Add(secondaryDnsControl, 1, row);
        row++;

        // Row 8: Buttons
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 5, 0, 0)
        };

        btnApply = new Button { Text = "Apply Settings", Width = 110, Height = 35 };
        btnApply.Click += btnApply_Click;

        btnCopy = new Button { Text = "Copy Config", Width = 100, Height = 35 };
        btnCopy.Click += btnCopy_Click;

        buttonPanel.Controls.Add(btnApply);
        buttonPanel.Controls.Add(btnCopy);
        mainLayout.Controls.Add(buttonPanel, 1, row);
        row++;

        // Row 9: Status
        lblStatus = new Label
        {
            Text = "Ready",
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill
        };
        mainLayout.SetColumnSpan(lblStatus, 2);
        mainLayout.Controls.Add(lblStatus, 0, row);

        // Status strip with service status and version
        statusStrip = new StatusStrip();
        lblServiceStatus = new ToolStripStatusLabel { Text = "Checking Service..." };
        lblVersion = new ToolStripStatusLabel { Alignment = ToolStripItemAlignment.Right };
        statusStrip.Items.Add(lblServiceStatus);
        statusStrip.Items.Add(new ToolStripStatusLabel { Spring = true }); // Spacer
        statusStrip.Items.Add(lblVersion);

        // Set version from assembly info
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "Unknown";
        lblVersion.Text = $"v{version}";

        // Set tooltips
        toolTip.SetToolTip(ipAddressControl, "Enter IPv4 address (e.g., 192.168.1.100)");
        toolTip.SetToolTip(subnetMaskControl, "Enter subnet mask (e.g., 255.255.255.0)");
        toolTip.SetToolTip(gatewayControl, "Enter gateway address (optional)");
        toolTip.SetToolTip(primaryDnsControl, "Enter primary DNS server (optional)");
        toolTip.SetToolTip(secondaryDnsControl, "Enter secondary DNS server (optional)");
        toolTip.SetToolTip(chkPhysicalOnly, "Show only physical network adapters (Ethernet and Wi-Fi)");
        toolTip.SetToolTip(chkDhcp, "Enable to obtain IP address automatically from DHCP server");
        toolTip.SetToolTip(btnRefresh, "Refresh adapter list");
        toolTip.SetToolTip(btnCopy, "Copy current configuration to clipboard");

        this.Controls.Add(mainLayout);
        this.Controls.Add(statusStrip);
    }
}
