using Xunit;
using IpChanger.Service;

namespace IpChanger.Tests;

public class IpHelperTests
{
    [Fact]
    public void GetWmiQuery_ReturnsOptimizedQuery()
    {
        // Arrange
        var adapterId = "{B5C4D89A-0E12-4C3F-9A8B-7E6D5F4C3B2A}";
        // Optimized query selects only SettingID
        var expectedQuery = $"SELECT SettingID FROM Win32_NetworkAdapterConfiguration WHERE SettingID = '{adapterId}'";

        // Act
        var actualQuery = IpHelper.GetWmiQuery(adapterId);

        // Assert
        Assert.Equal(expectedQuery, actualQuery);
    }

    [Fact]
    public void GetWmiQuery_EscapesSingleQuotes()
    {
        // Arrange
        var adapterId = "Adapter'Id";
        var expectedQuery = "SELECT SettingID FROM Win32_NetworkAdapterConfiguration WHERE SettingID = 'Adapter''Id'";

        // Act
        var actualQuery = IpHelper.GetWmiQuery(adapterId);

        // Assert
        Assert.Equal(expectedQuery, actualQuery);
    }
}
