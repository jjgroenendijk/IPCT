using System.Net.NetworkInformation;
using System.IO.Pipes;
using System.Text.Json;
using IpChanger.Common;
using IpChanger.UI.Controls;

namespace IpChanger.UI;

public partial class MainForm : Form
{
    private const string EmptyIpPlaceholder = "...";
    
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
    private Button btnClear = null!;
    private Label lblMacAddress = null!;
    private StatusStrip statusStrip = null!;
    private ToolStripStatusLabel lblServiceStatus = null!;
    private ToolStripStatusLabel lblVersion = null!;
    private ToolTip toolTip = null!;
    private string _appStatus = "Ready";
    private string _serviceStatus = "Checking Service...";
    private System.Windows.Forms.Timer timerStatus = null!;
    private CheckBox chkPhysicalOnly = null!;
    private TableLayoutPanel mainLayout = null!;

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
            _serviceStatus = "Service: Connected";
            UpdateCombinedStatus(Color.Green);
        }
        catch
        {
            _serviceStatus = "Service: Disconnected";
            UpdateCombinedStatus(Color.Red);
        }
    }

    private void UpdateCombinedStatus(Color? serviceColor = null)
    {
        lblServiceStatus.Text = $"{_appStatus} | {_serviceStatus}";
        if (serviceColor.HasValue)
            lblServiceStatus.ForeColor = serviceColor.Value;
    }

    private void SetAppStatus(string status)
    {
        _appStatus = status;
        UpdateCombinedStatus();
    }

    private static string FormatMacAddress(string mac)
    {
        if (string.IsNullOrEmpty(mac) || mac.Length != 12)
            return "N/A";
        return string.Join("-", Enumerable.Range(0, 6).Select(i => mac.Substring(i * 2, 2)));
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

    private static bool IsValidIp(IpAddressControl ctrl, bool required) =>
        ctrl.IsValid && (!required || (!string.IsNullOrWhiteSpace(ctrl.Text) && ctrl.Text != EmptyIpPlaceholder));

    private void ClearAllValidationErrors()
    {
        ipAddressControl.BackColor = SystemColors.Window;
        subnetMaskControl.BackColor = SystemColors.Window;
        gatewayControl.BackColor = SystemColors.Window;
        primaryDnsControl.BackColor = SystemColors.Window;
        secondaryDnsControl.BackColor = SystemColors.Window;
        cmbAdapters.BackColor = SystemColors.Window;
    }

    private void HighlightError(Control control)
    {
        control.BackColor = Color.MistyRose;
        control.Focus();
    }

    private bool ValidateInputs(out string errorMessage)
    {
        errorMessage = "";
        ClearAllValidationErrors();

        // Validate adapter selection
        if (cmbAdapters.SelectedItem is not AdapterItem)
        {
            errorMessage = "Please select a network adapter.";
            HighlightError(cmbAdapters);
            return false;
        }

        if (chkDhcp.Checked) return true;

        if (!IsValidIp(ipAddressControl, true))
        {
            errorMessage = "IP Address is required and must be a valid format (e.g., 192.168.1.100).";
            HighlightError(ipAddressControl);
            return false;
        }
        if (!IsValidIp(subnetMaskControl, true))
        {
            errorMessage = "Subnet Mask is required and must be a valid format (e.g., 255.255.255.0).";
            HighlightError(subnetMaskControl);
            return false;
        }
        if (!IsValidIp(gatewayControl, false))
        {
            errorMessage = "Gateway address must be a valid IP format (e.g., 192.168.1.1) or left empty.";
            HighlightError(gatewayControl);
            return false;
        }
        if (!IsValidIp(primaryDnsControl, false))
        {
            errorMessage = "Primary DNS must be a valid IP format (e.g., 8.8.8.8) or left empty.";
            HighlightError(primaryDnsControl);
            return false;
        }
        if (!IsValidIp(secondaryDnsControl, false))
        {
            errorMessage = "Secondary DNS must be a valid IP format (e.g., 8.8.4.4) or left empty.";
            HighlightError(secondaryDnsControl);
            return false;
        }
        return true;
    }

    private string BuildDnsString()
    {
        var dns = new List<string>();
        if (!string.IsNullOrWhiteSpace(primaryDnsControl.Text) && primaryDnsControl.Text != EmptyIpPlaceholder)
            dns.Add(primaryDnsControl.Text);
        if (!string.IsNullOrWhiteSpace(secondaryDnsControl.Text) && secondaryDnsControl.Text != EmptyIpPlaceholder)
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

        var request = new IpConfigRequest(
            AdapterId: selected.Id,
            UseDhcp: chkDhcp.Checked,
            IpAddress: ipAddressControl.Text,
            SubnetMask: subnetMaskControl.Text,
            Gateway: gatewayControl.Text == EmptyIpPlaceholder ? "" : gatewayControl.Text,
            Dns: BuildDnsString()
        );

        ApplySettings(request);
    }

    private async void ApplySettings(IpConfigRequest request)
    {
        btnApply.Enabled = false;
        SetAppStatus("Connecting to service...");
        try
        {
            await using var client = new NamedPipeClientStream(".", "IpChangerPipe", PipeDirection.InOut);
            await client.ConnectAsync(3000);

            // Use leaveOpen: true to prevent StreamWriter/StreamReader from closing the pipe prematurely
            await using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(client, leaveOpen: true);

            await writer.WriteLineAsync(JsonSerializer.Serialize(request, IpChangerJsonContext.Default.IpConfigRequest));

            var responseJson = await reader.ReadLineAsync();
            if (responseJson != null)
            {
                var response = JsonSerializer.Deserialize(responseJson, IpChangerJsonContext.Default.IpConfigResponse);
                SetAppStatus(response?.Message ?? "Unknown response");
                
                // Only show MessageBox for errors, success is shown in status bar
                if (response?.Success != true)
                {
                    MessageBox.Show(response?.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    // Refresh adapter list to show updated settings
                    LoadAdapters();
                }
            }
        }
        catch (Exception ex)
        {
            SetAppStatus("Error: " + ex.Message);
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
        btnClear.Enabled = enabled;
    }

    private void cmbAdapters_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (cmbAdapters.SelectedItem is AdapterItem item)
        {
            LoadAdapterConfig(item);
            var mac = item.Nic.GetPhysicalAddress().ToString();
            lblMacAddress.Text = $"MAC: {FormatMacAddress(mac)}";
        }
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
            SetAppStatus($"Error loading config: {ex.Message}");
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
        SetAppStatus("Configuration copied to clipboard.");
    }

    private void btnClear_Click(object? sender, EventArgs e)
    {
        ipAddressControl.Text = string.Empty;
        subnetMaskControl.Text = string.Empty;
        gatewayControl.Text = string.Empty;
        primaryDnsControl.Text = string.Empty;
        secondaryDnsControl.Text = string.Empty;
        SetAppStatus("Configuration cleared.");
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
}
