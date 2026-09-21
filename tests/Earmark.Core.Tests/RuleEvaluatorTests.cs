using AwesomeAssertions;

using Earmark.Core.Models;
using Earmark.Core.Routing;

using Xunit;

namespace Earmark.Core.Tests;

public class RuleEvaluatorTests
{
    private readonly RuleEvaluator _evaluator = new(new RuleMatcher());

    private static readonly AudioEndpoint Speakers =
        new("id:Speakers", "Speakers", "", EndpointFlow.Render, EndpointState.Active, false, false);

    private static AudioSession Session(string proc, uint pid) =>
        new($"i{pid}", $"s{pid}", pid, proc, $@"C:\{proc}.exe", proc, "", Speakers.Id, SessionState.Active, false);

    private static RoutingRule AppVolumeRule(string app, string device = "") => new()
    {
        Name = "app volume",
        Enabled = true,
        Actions = { new RuleAction { Kind = ActionKind.ApplicationVolume, AppPattern = app, DevicePattern = device, Volume = 0.4f } },
    };

    private RuleStatus Status(RoutingRule rule, params AudioSession[] sessions) =>
        _evaluator.Evaluate(rule, new[] { rule }, sessions, new[] { Speakers }).Status;

    [Fact]
    public void App_volume_is_active_when_a_session_matches()
    {
        Status(AppVolumeRule("Discord"), Session("Discord", 1)).Should().Be(RuleStatus.Active);
    }

    [Fact]
    public void App_volume_is_idle_when_no_session_matches()
    {
        Status(AppVolumeRule("Discord"), Session("Spotify", 2)).Should().Be(RuleStatus.Idle);
    }

    [Fact]
    public void App_volume_is_idle_when_device_filter_excludes_the_session()
    {
        Status(AppVolumeRule("Discord", device: "Headphones"), Session("Discord", 1)).Should().Be(RuleStatus.Idle);
    }
}
