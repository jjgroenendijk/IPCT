using System.Text.Json.Serialization;

namespace IpChanger.Common;

public record IpConfigRequest(
    string AdapterId = "",
    bool UseDhcp = false,
    string IpAddress = "",
    string SubnetMask = "255.255.255.0",
    string Gateway = "",
    string Dns = "");

public record IpConfigResponse(bool Success, string Message = "");

/// <summary>
/// Source-generated JSON serializer context for trim-safe and AOT-compatible serialization.
/// </summary>
[JsonSerializable(typeof(IpConfigRequest))]
[JsonSerializable(typeof(IpConfigResponse))]
[JsonSourceGenerationOptions(WriteIndented = false)]
public partial class IpChangerJsonContext : JsonSerializerContext
{
}
