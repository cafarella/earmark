using AwesomeAssertions;

using Earmark.Core.Models;

using Xunit;

namespace Earmark.Core.Tests;

public class RuleDefaultsTests
{
    [Fact]
    public void New_action_matches_by_picking_not_regex()
    {
        var action = new RuleAction();

        action.AppMatchMode.Should().Be(PatternMatchMode.Exact);
        action.DeviceMatchMode.Should().Be(PatternMatchMode.Exact);
        action.MixMatchMode.Should().Be(PatternMatchMode.Exact);
    }

    [Fact]
    public void New_condition_matches_by_picking_not_regex()
    {
        var condition = new RuleCondition();

        condition.AppMatchMode.Should().Be(PatternMatchMode.Exact);
        condition.DeviceMatchMode.Should().Be(PatternMatchMode.Exact);
    }

    [Fact]
    public void Saved_rules_keep_the_mode_they_were_written_with()
    {
        // Every field is serialised, so a rule written by an older build carries its own modes and
        // is unaffected by the default above.
        var action = new RuleAction { AppMatchMode = PatternMatchMode.Regex, DeviceMatchMode = PatternMatchMode.Wildcard };
        var clone = action.Clone();

        clone.AppMatchMode.Should().Be(PatternMatchMode.Regex);
        clone.DeviceMatchMode.Should().Be(PatternMatchMode.Wildcard);
    }
}
