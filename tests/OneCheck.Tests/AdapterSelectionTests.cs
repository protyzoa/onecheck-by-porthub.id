using System.Net;
using OneCheck.Core.Checks;
using OneCheck.Core.Localization;
using OneCheck.Core.Reporting;
using static OneCheck.Tests.TestReports;

namespace OneCheck.Tests;

public class AdapterSelectionTests
{
    private static (IPAddress, int) Ip(string s, int prefix = 24) => (IPAddress.Parse(s), prefix);

    // Kasus nyata: satu adapter Ethernet dengan dua IP; Windows keluar lewat 192.168.7.55 (gateway 192.168.7.1).
    private static readonly List<(IPAddress Address, int Prefix)> TwoIps = [Ip("192.168.0.10"), Ip("192.168.7.55")];

    [Fact]
    public void Source_ip_chosen_by_windows_wins()
        => Assert.Equal(IPAddress.Parse("192.168.7.55"),
            AdapterCheck.SelectPrimaryIPv4(TwoIps, IPAddress.Parse("192.168.7.55"), IPAddress.Parse("192.168.7.1"))!.Value.Address);

    [Fact]
    public void Without_source_ip_the_one_in_gateway_subnet_wins()
        => Assert.Equal(IPAddress.Parse("192.168.7.55"),
            AdapterCheck.SelectPrimaryIPv4(TwoIps, null, IPAddress.Parse("192.168.7.1"))!.Value.Address);

    [Fact]
    public void Source_ip_of_another_adapter_falls_back_to_gateway_subnet()
        => Assert.Equal(IPAddress.Parse("192.168.7.55"),
            AdapterCheck.SelectPrimaryIPv4(TwoIps, IPAddress.Parse("10.77.90.2"), IPAddress.Parse("192.168.7.1"))!.Value.Address);

    [Fact]
    public void Apipa_is_avoided_when_a_normal_ip_exists()
        => Assert.Equal(IPAddress.Parse("10.0.0.5"),
            AdapterCheck.SelectPrimaryIPv4([Ip("169.254.1.1", 16), Ip("10.0.0.5")], null, null)!.Value.Address);

    [Fact]
    public void No_addresses_gives_null() => Assert.Null(AdapterCheck.SelectPrimaryIPv4([], null, null));

    [Theory]
    [InlineData("192.168.7.55", 24, "192.168.7.1", true)]
    [InlineData("192.168.0.10", 24, "192.168.7.1", false)]
    [InlineData("10.1.2.3", 8, "10.200.0.1", true)]
    [InlineData("172.16.5.10", 30, "172.16.5.9", true)]
    [InlineData("172.16.5.10", 30, "172.16.5.13", false)]
    public void Subnet_check(string ip, int prefix, string other, bool expected)
        => Assert.Equal(expected, AdapterCheck.IsInSubnet(IPAddress.Parse(ip), prefix, IPAddress.Parse(other)));

    [Fact]
    public void Report_shows_other_ips_and_flags_gateway_outside_subnet()
    {
        var r = Healthy(Lan() with { IPv4 = "192.168.0.10", Gateway = "192.168.7.1" });
        r.PrimaryAdapter = r.PrimaryAdapter! with { OtherIPv4 = ["192.168.7.55/24"] };
        Assert.True(r.PrimaryAdapter.GatewayOutsideSubnet);

        var lines = ReportFormatter.SummaryLines(r, Language.En);
        Assert.Contains(lines, l => l.Key == "IP / DNS" && l.Value.Contains("+1 more IP"));
        Assert.Contains(lines, l => l.Key == "Gateway" && l.Value.Contains("not in the IP's subnet"));

        var dx = OneCheck.Core.Diagnosis.DiagnosisEngine.Evaluate(r, new OneCheck.Core.Models.ThresholdsConfig());
        Assert.Contains("IPv4 lain: 192.168.7.55/24", ReportFormatter.FullText(r, dx, "PortHub"));
    }

    [Fact]
    public void Normal_adapter_has_no_extra_flags()
    {
        var a = Lan();
        Assert.False(a.GatewayOutsideSubnet);
        Assert.Empty(a.OtherIPv4);
    }
}
