using OCPPServer;
using Xunit;

namespace OCPPServer.Tests;

/// <summary>OCPP 1.6 SetChargingProfile / ClearChargingProfile payloads for the current limit.</summary>
public class ChargingProfilesTests
{
    [Fact]
    public void TxDefault_targets_connector_0_relative_in_amps()
    {
        var p = ChargingProfiles.TxDefault(16);

        Assert.Equal(0, (int)p["connectorId"]!);
        var cs = p["csChargingProfiles"]!;
        Assert.Equal(ChargingProfiles.TxDefaultProfileId, (int)cs["chargingProfileId"]!);
        Assert.Equal(0, (int)cs["stackLevel"]!);
        Assert.Equal("TxDefaultProfile", (string)cs["chargingProfilePurpose"]!);
        Assert.Equal("Relative", (string)cs["chargingProfileKind"]!);
        Assert.Null(cs["transactionId"]);

        var schedule = cs["chargingSchedule"]!;
        Assert.Equal("A", (string)schedule["chargingRateUnit"]!);
        Assert.Null(schedule["startSchedule"]);
        var period = Assert.Single(schedule["chargingSchedulePeriod"]!);
        Assert.Equal(0, (int)period["startPeriod"]!);
        Assert.Equal(16.0, (double)period["limit"]!);
    }

    [Fact]
    public void Tx_profile_carries_connector_and_transaction()
    {
        var p = ChargingProfiles.Tx(connectorId: 1, transactionId: 42, limitA: 10);

        Assert.Equal(1, (int)p["connectorId"]!);
        var cs = p["csChargingProfiles"]!;
        Assert.Equal(ChargingProfiles.TxProfileId, (int)cs["chargingProfileId"]!);
        Assert.Equal("TxProfile", (string)cs["chargingProfilePurpose"]!);
        Assert.Equal(42, (int)cs["transactionId"]!);
    }

    [Fact]
    public void Absolute_fallback_has_utc_start_schedule()
    {
        var now = new DateTime(2026, 10, 9, 13, 5, 7, DateTimeKind.Utc);
        var cs  = ChargingProfiles.TxDefault(13, absolute: true, nowUtc: now)["csChargingProfiles"]!;

        Assert.Equal("Absolute", (string)cs["chargingProfileKind"]!);
        Assert.Equal("2026-10-09T13:05:07Z", (string)cs["chargingSchedule"]!["startSchedule"]!);
    }

    [Fact]
    public void Limit_is_rounded_to_one_decimal()
    {
        var period = ChargingProfiles.TxDefault(13.27)["csChargingProfiles"]!["chargingSchedule"]!["chargingSchedulePeriod"]![0]!;
        Assert.Equal(13.3, (double)period["limit"]!);
    }

    [Theory]
    [InlineData(6, true)]
    [InlineData(32, true)]
    [InlineData(5.9, false)]
    [InlineData(0, false)]
    [InlineData(81, false)]
    [InlineData(double.NaN, false)]
    public void Valid_limit_range(double limitA, bool expected) =>
        Assert.Equal(expected, ChargingProfiles.IsValidLimit(limitA));

    [Fact]
    public void Clear_by_purpose()
    {
        var c = ChargingProfiles.Clear("TxDefaultProfile");
        Assert.Equal("TxDefaultProfile", (string)c["chargingProfilePurpose"]!);
        Assert.Single(c.Properties());
    }
}
