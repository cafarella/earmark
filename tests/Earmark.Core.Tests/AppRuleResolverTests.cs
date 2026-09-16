using AwesomeAssertions;

using Earmark.Core.Models;
using Earmark.Core.Routing;

using Xunit;

namespace Earmark.Core.Tests;

public class AppRuleResolverTests
{
    private readonly RuleMatcher _matcher = new();

    private static readonly AudioEndpoint Speakers = Endpoint("Speakers");
    private static readonly AudioEndpoint Headphones = Endpoint("Headphones");

    private static AudioEndpoint Endpoint(string name) =>
        new($"id:{name}", name, "", EndpointFlow.Render, EndpointState.Active, false, false);

    private static AudioSession Session(string proc, uint pid, AudioEndpoint endpoint) =>
        new($"i{pid}", $"s{pid}", pid, proc, $@"C:\Apps\{proc}.exe", proc, "", endpoint.Id, SessionState.Active, false);

    private static RoutingRule VolumeRule(string name, string app, float volume, string device = "", bool pinned = true, bool enabled = true) =>
        new()
        {
            Name = name,
            Enabled = enabled,
            Actions = { new RuleAction { Kind = ActionKind.ApplicationVolume, AppPattern = app, DevicePattern = device, Volume = volume, Pinned = pinned } },
        };

    private static RoutingRule MuteRule(string name, string app, bool muted, string device = "") =>
        new()
        {
            Name = name,
            Enabled = true,
            Actions = { new RuleAction { Kind = ActionKind.ApplicationMute, AppPattern = app, DevicePattern = device, Muted = muted } },
        };

    private AppRuleTargets Resolve(AudioSession session, params RoutingRule[] rules) =>
        AppRuleResolver.Resolve(session, rules, new[] { Speakers, Headphones }, new[] { session }, _matcher);

    [Fact]
    public void Resolves_volume_and_mute_independently()
    {
        var discord = Session("Discord", 10, Speakers);
        var targets = Resolve(discord, VolumeRule("v", "Discord", 0.6f), MuteRule("m", "Discord", muted: true));

        targets.Volume!.Value.Value.Should().Be(0.6f);
        targets.Muted!.Value.Value.Should().BeTrue();
    }

    [Fact]
    public void First_matching_rule_wins_each_dimension()
    {
        var discord = Session("Discord", 10, Speakers);
        var targets = Resolve(discord, VolumeRule("first", "Discord", 0.3f), VolumeRule("second", "Discord", 0.9f));

        targets.Volume!.Value.Value.Should().Be(0.3f);
        targets.Volume!.Value.SourceName.Should().Be("first");
    }

    [Fact]
    public void Pinned_flag_is_threaded_through()
    {
        var discord = Session("Discord", 10, Speakers);

        Resolve(discord, VolumeRule("pinned", "Discord", 0.5f, pinned: true)).Volume!.Value.Pinned.Should().BeTrue();
        Resolve(discord, VolumeRule("oneshot", "Discord", 0.5f, pinned: false)).Volume!.Value.Pinned.Should().BeFalse();
    }

    [Fact]
    public void Disabled_rule_does_not_target()
    {
        var discord = Session("Discord", 10, Speakers);
        Resolve(discord, VolumeRule("off", "Discord", 0.5f, enabled: false)).Volume.Should().BeNull();
    }

    [Fact]
    public void Non_matching_app_pattern_yields_no_target()
    {
        var discord = Session("Discord", 10, Speakers);
        Resolve(discord, VolumeRule("v", "Spotify", 0.5f)).Volume.Should().BeNull();
    }

    [Fact]
    public void Else_branch_supplies_the_target_when_conditions_unmet()
    {
        var discord = Session("Discord", 10, Speakers);
        var rule = new RoutingRule
        {
            Name = "cond",
            Enabled = true,
            Conditions = { new RuleCondition { Kind = ConditionKind.Application, AppPattern = "Teams" } },
            Actions = { new RuleAction { Kind = ActionKind.ApplicationMute, AppPattern = "Discord", Muted = true } },
            ElseActions = { new RuleAction { Kind = ActionKind.ApplicationMute, AppPattern = "Discord", Muted = false } },
        };

        // Teams isn't running -> else branch -> unmuted target
        Resolve(discord, rule).Muted!.Value.Value.Should().BeFalse();
    }

    [Fact]
    public void Blank_device_filter_matches_session_on_any_device()
    {
        Resolve(Session("Discord", 10, Speakers), VolumeRule("v", "Discord", 0.5f)).Volume.Should().NotBeNull();
        Resolve(Session("Discord", 11, Headphones), VolumeRule("v", "Discord", 0.5f)).Volume.Should().NotBeNull();
    }

    [Fact]
    public void Device_filter_matching_session_endpoint_targets()
    {
        var discord = Session("Discord", 10, Headphones);
        Resolve(discord, VolumeRule("v", "Discord", 0.5f, device: "Headphones")).Volume.Should().NotBeNull();
    }

    [Fact]
    public void Device_filter_not_matching_session_endpoint_yields_no_target()
    {
        var discord = Session("Discord", 10, Speakers);
        Resolve(discord, VolumeRule("v", "Discord", 0.5f, device: "Headphones")).Volume.Should().BeNull();
    }

    [Fact]
    public void App_pattern_can_match_executable_path()
    {
        var discord = Session("Discord", 10, Speakers);
        var rule = new RoutingRule
        {
            Name = "path",
            Enabled = true,
            Actions =
            {
                new RuleAction
                {
                    Kind = ActionKind.ApplicationVolume,
                    AppPattern = @"\\Apps\\Discord\.exe$",
                    AppMatchMode = PatternMatchMode.Regex,
                    Volume = 0.5f,
                },
            },
        };

        Resolve(discord, rule).Volume.Should().NotBeNull();
    }

    [Fact]
    public void Action_without_app_pattern_is_invalid()
    {
        new RuleAction { Kind = ActionKind.ApplicationVolume, Volume = 0.5f }.IsValid.Should().BeFalse();
        new RuleAction { Kind = ActionKind.ApplicationMute }.IsValid.Should().BeFalse();
        new RuleAction { Kind = ActionKind.ApplicationVolume, AppPattern = "x", Volume = 0.5f }.IsValid.Should().BeTrue();
        new RuleAction { Kind = ActionKind.ApplicationMute, AppPattern = "x" }.IsValid.Should().BeTrue();
    }
}
