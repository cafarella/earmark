using AwesomeAssertions;

using Earmark.Core.Models;
using Earmark.Core.Routing;

using Xunit;

namespace Earmark.Core.Tests;

public class RouteExplainerTests
{
    private readonly RuleMatcher _matcher = new();

    private static readonly AudioEndpoint Speakers = Endpoint("Speakers");
    private static readonly AudioEndpoint Headphones = Endpoint("Headphones");

    private static AudioEndpoint Endpoint(string name) =>
        new($"id:{name}", name, "", EndpointFlow.Render, EndpointState.Active, false, false);

    private static AudioSession Session(string proc, uint pid, AudioEndpoint endpoint) =>
        new($"i{pid}", $"s{pid}", pid, proc, $@"C:\Apps\{proc}.exe", proc, "", endpoint.Id, SessionState.Active, false);

    private static RoutingRule RouteRule(string name, string app, string device, bool enabled = true) =>
        new()
        {
            Name = name,
            Enabled = enabled,
            Actions = { new RuleAction { Kind = ActionKind.ApplicationDevice, AppPattern = app, DevicePattern = device } },
        };

    private RouteExplanation Explain(AudioSession session, params RoutingRule[] rules) =>
        RouteExplainer.Explain(session, EndpointFlow.Render, rules, new[] { Speakers, Headphones }, new[] { session }, _matcher);

    [Fact]
    public void Winning_rule_names_the_device_and_the_rule()
    {
        var discord = Session("Discord", 10, Speakers);
        var rule = RouteRule("Gaming", "Discord", "Headphones");

        var explanation = Explain(discord, rule);

        explanation.Summary.Should().Be("On Headphones because rule \"Gaming\" matched.");
        explanation.RuleId.Should().Be(rule.Id);
        explanation.NearMisses.Should().BeEmpty();
    }

    [Fact]
    public void Winning_rule_without_a_name_reads_as_unnamed()
    {
        var discord = Session("Discord", 10, Speakers);

        Explain(discord, RouteRule("", "Discord", "Headphones")).Summary
            .Should().Be("On Headphones because rule \"Unnamed rule\" matched.");
    }

    [Fact]
    public void No_rules_means_windows_decides()
    {
        var discord = Session("Discord", 10, Speakers);

        var explanation = Explain(discord);

        explanation.Summary.Should().Be("No rule targets this app, so Windows decides where it plays.");
        explanation.RuleId.Should().BeNull();
        explanation.NearMisses.Should().BeEmpty();
    }

    [Fact]
    public void Disabled_rule_is_a_near_miss()
    {
        var discord = Session("Discord", 10, Speakers);

        var explanation = Explain(discord, RouteRule("Gaming", "Discord", "Headphones", enabled: false));

        explanation.RuleId.Should().BeNull();
        explanation.NearMisses.Should().ContainSingle()
            .Which.Should().Be(new RouteNearMiss("Gaming", "disabled"));
    }

    [Fact]
    public void Unfinished_action_is_a_near_miss()
    {
        var discord = Session("Discord", 10, Speakers);

        var explanation = Explain(discord, RouteRule("Half done", "Discord", ""));

        explanation.NearMisses.Should().ContainSingle()
            .Which.Should().Be(new RouteNearMiss("Half done", "not finished being set up"));
    }

    [Fact]
    public void Rule_targeting_a_missing_device_says_it_is_not_connected()
    {
        var discord = Session("Discord", 10, Speakers);

        var explanation = Explain(discord, RouteRule("Gaming", "Discord", "Bluetooth Speaker"));

        explanation.RuleId.Should().BeNull();
        explanation.NearMisses.Should().ContainSingle()
            .Which.Should().Be(new RouteNearMiss("Gaming", "its device \"Bluetooth Speaker\" is not connected"));
    }

    [Fact]
    public void Rule_behind_an_earlier_rule_says_an_earlier_rule_routes_the_app()
    {
        var discord = Session("Discord", 10, Speakers);
        var first = RouteRule("First", "Discord", "Headphones");
        var second = RouteRule("Second", "Discord", "Speakers");

        var explanation = Explain(discord, first, second);

        explanation.RuleId.Should().Be(first.Id);
        explanation.NearMisses.Should().ContainSingle()
            .Which.Should().Be(new RouteNearMiss("Second", "an earlier rule already routes this app"));
    }

    [Fact]
    public void Rule_whose_conditions_are_not_met_says_so()
    {
        var discord = Session("Discord", 10, Speakers);
        var rule = new RoutingRule
        {
            Name = "When Teams runs",
            Enabled = true,
            Conditions = { new RuleCondition { Kind = ConditionKind.Application, AppPattern = "Teams" } },
            Actions = { new RuleAction { Kind = ActionKind.ApplicationDevice, AppPattern = "Discord", DevicePattern = "Headphones" } },
        };

        var explanation = Explain(discord, rule);

        explanation.RuleId.Should().BeNull();
        explanation.NearMisses.Should().ContainSingle()
            .Which.Should().Be(new RouteNearMiss("When Teams runs", "its conditions are not met"));
    }

    [Fact]
    public void Near_misses_are_capped_at_three_in_rule_order()
    {
        var discord = Session("Discord", 10, Speakers);
        var rules = new[]
        {
            RouteRule("Winner", "Discord", "Headphones"),
            RouteRule("Second", "Discord", "Speakers"),
            RouteRule("Third", "Discord", "Speakers"),
            RouteRule("Fourth", "Discord", "Speakers"),
            RouteRule("Fifth", "Discord", "Speakers"),
        };

        var explanation = Explain(discord, rules);

        explanation.NearMisses.Should().HaveCount(RouteExplainer.MaxNearMisses);
        explanation.NearMisses.Select(m => m.RuleName).Should().Equal("Second", "Third", "Fourth");
    }

    [Fact]
    public void Rule_that_does_not_mention_the_app_is_never_a_near_miss()
    {
        var discord = Session("Discord", 10, Speakers);

        var explanation = Explain(discord, RouteRule("Music", "Spotify", "Headphones"));

        explanation.RuleId.Should().BeNull();
        explanation.NearMisses.Should().BeEmpty();
    }

    [Fact]
    public void Rule_routing_the_apps_microphone_is_not_a_near_miss_for_playback()
    {
        var discord = Session("Discord", 10, Speakers);
        var micRule = new RoutingRule
        {
            Name = "Mic",
            Enabled = true,
            Actions =
            {
                new RuleAction
                {
                    Kind = ActionKind.ApplicationDevice,
                    Flow = EndpointFlow.Capture,
                    AppPattern = "Discord",
                    DevicePattern = "Headset mic",
                },
            },
        };

        // Explaining playback: a rule routing the same app's input never competed for this.
        Explain(discord, micRule).NearMisses.Should().BeEmpty();
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        var discord = Session("Discord", 10, Speakers);
        var rules = Array.Empty<RoutingRule>();
        var endpoints = new[] { Speakers };
        var sessions = new[] { discord };

        var explainNullSession = () => RouteExplainer.Explain(null!, EndpointFlow.Render, rules, endpoints, sessions, _matcher);
        var explainNullMatcher = () => RouteExplainer.Explain(discord, EndpointFlow.Render, rules, endpoints, sessions, null!);

        explainNullSession.Should().Throw<ArgumentNullException>();
        explainNullMatcher.Should().Throw<ArgumentNullException>();
    }
}
