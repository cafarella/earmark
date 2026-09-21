using AwesomeAssertions;

using Earmark.Core.Models;
using Earmark.Core.Routing;

using Xunit;

namespace Earmark.Core.Tests;

public class SessionRuleFactoryTests
{
    private static readonly AudioEndpoint Headphones = Endpoint("Sony Headphones", "4-WH-1000XM5");

    private static AudioEndpoint Endpoint(string name, string description = "") =>
        new($"id:{name}", name, description, EndpointFlow.Render, EndpointState.Active, false, false);

    private static AudioSession Session(string proc, string displayName) =>
        new("i1", "s1", 10, proc, $@"C:\Apps\{proc}.exe", displayName, "", Headphones.Id, SessionState.Active, false);

    [Fact]
    public void Action_pins_the_process_name_to_the_endpoint_picker_name()
    {
        var action = SessionRuleFactory.CreateAction(Session("Discord", "Discord"), Headphones);

        action.Kind.Should().Be(ActionKind.ApplicationDevice);
        action.Flow.Should().Be(EndpointFlow.Render);
        action.AppPattern.Should().Be("Discord");
        action.AppMatchMode.Should().Be(PatternMatchMode.Exact);
        action.DevicePattern.Should().Be(Headphones.PickerName);
        action.DeviceMatchMode.Should().Be(PatternMatchMode.Exact);
        action.Pinned.Should().BeTrue();
    }

    [Fact]
    public void Rule_is_enabled_with_one_action_named_after_the_display_name()
    {
        var rule = SessionRuleFactory.CreateRule(Session("Discord", "Discord Voice"), Headphones);

        rule.Enabled.Should().BeTrue();
        rule.Name.Should().Be("Discord Voice");
        rule.Actions.Should().ContainSingle();
        rule.ElseActions.Should().BeEmpty();
        rule.Conditions.Should().BeEmpty();
    }

    [Fact]
    public void Blank_display_name_falls_back_to_the_process_name()
    {
        SessionRuleFactory.CreateRule(Session("Discord", ""), Headphones).Name.Should().Be("Discord");
        SessionRuleFactory.CreateRule(Session("Discord", "   "), Headphones).Name.Should().Be("Discord");
        SessionRuleFactory.CreateRule(Session("", ""), Headphones).Name.Should().Be("New rule");
    }

    [Fact]
    public void Null_endpoint_yields_an_empty_device_pattern()
    {
        var action = SessionRuleFactory.CreateAction(Session("Discord", "Discord"), null);

        action.DevicePattern.Should().BeEmpty();
        action.Kind.Should().Be(ActionKind.ApplicationDevice);
        action.AppPattern.Should().Be("Discord");
        action.AppMatchMode.Should().Be(PatternMatchMode.Exact);
    }

    [Fact]
    public void Rule_action_matches_a_standalone_created_action()
    {
        var session = Session("Discord", "Discord");
        var standalone = SessionRuleFactory.CreateAction(session, Headphones);
        var inRule = SessionRuleFactory.CreateRule(session, Headphones).Actions.Single();

        inRule.Kind.Should().Be(standalone.Kind);
        inRule.Flow.Should().Be(standalone.Flow);
        inRule.AppPattern.Should().Be(standalone.AppPattern);
        inRule.AppMatchMode.Should().Be(standalone.AppMatchMode);
        inRule.DevicePattern.Should().Be(standalone.DevicePattern);
        inRule.DeviceMatchMode.Should().Be(standalone.DeviceMatchMode);
        inRule.Pinned.Should().Be(standalone.Pinned);
    }

    [Fact]
    public void Null_session_throws()
    {
        var createRule = () => SessionRuleFactory.CreateRule(null!, Headphones);
        var createAction = () => SessionRuleFactory.CreateAction(null!, Headphones);

        createRule.Should().Throw<ArgumentNullException>();
        createAction.Should().Throw<ArgumentNullException>();
    }
}
