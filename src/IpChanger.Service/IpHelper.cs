using IpChanger.Common;
using System.Management;
using System.Net;

namespace IpChanger.Service;

public static class IpHelper
{
    private static bool IsValidIpAddress(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return false;
        return IPAddress.TryParse(ip, out var parsed) && 
               parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
    }

    public static IpConfigResponse ApplyConfig(IpConfigRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.AdapterId))
            {
                return new IpConfigResponse(false, "Adapter not found.");
            }

            // Server-side validation for static IP configuration
            if (!request.UseDhcp)
            {
                if (!IsValidIpAddress(request.IpAddress))
                {
                    return new IpConfigResponse(false, "Invalid IP Address format.");
                }
                if (!IsValidIpAddress(request.SubnetMask))
                {
                    return new IpConfigResponse(false, "Invalid Subnet Mask format.");
                }
                if (!string.IsNullOrWhiteSpace(request.Gateway) && !IsValidIpAddress(request.Gateway))
                {
                    return new IpConfigResponse(false, "Invalid Gateway address format.");
                }
                if (!string.IsNullOrWhiteSpace(request.Dns))
                {
                    var dnsServers = request.Dns.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var dns in dnsServers)
                    {
                        if (!IsValidIpAddress(dns.Trim()))
                        {
                            return new IpConfigResponse(false, $"Invalid DNS server format: {dns.Trim()}");
                        }
                    }
                }
            }

            // WMI Optimization: Filter by SettingID at the source
            var safeId = request.AdapterId.Replace("'", "''");
            using var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_NetworkAdapterConfiguration WHERE SettingID = '{safeId}'");
            using var collection = searcher.Get();

            foreach (ManagementObject obj in collection)
            {
                // Query already filters by ID, so we proceed directly
                if (request.UseDhcp)
                {
                    obj.InvokeMethod("EnableDHCP", null);
                    obj.InvokeMethod("SetDNSServerSearchOrder", null); // Clear DNS
                    return new IpConfigResponse(true, "DHCP Enabled");
                }
                else
                {
                    // Set IP and Subnet
                    var newIP = obj.GetMethodParameters("EnableStatic");
                    newIP["IPAddress"] = new[] { request.IpAddress };
                    newIP["SubnetMask"] = new[] { request.SubnetMask };
                    var resIp = obj.InvokeMethod("EnableStatic", newIP, null);

                    // Set Gateway
                    if (!string.IsNullOrWhiteSpace(request.Gateway))
                    {
                        var newGateway = obj.GetMethodParameters("SetGateways");
                        newGateway["DefaultIPGateway"] = new[] { request.Gateway };
                        newGateway["GatewayCostMetric"] = new[] { 1 };
                        obj.InvokeMethod("SetGateways", newGateway, null);
                    }

                    // Set DNS
                    if (!string.IsNullOrWhiteSpace(request.Dns))
                    {
                        var newDns = obj.GetMethodParameters("SetDNSServerSearchOrder");
                        newDns["DNSServerSearchOrder"] = request.Dns.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        obj.InvokeMethod("SetDNSServerSearchOrder", newDns, null);
                    }

                    return new IpConfigResponse(true, "Static IP configured successfully.");
                }
            }
            return new IpConfigResponse(false, "Adapter not found.");
        }
        catch (Exception ex)
        {
            return new IpConfigResponse(false, ex.Message);
        }
    }
}
