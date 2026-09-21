using Earmark.Core.Models;

namespace Earmark.Core.Routing;

/// <summary>
/// Builds the "route this app here" rule behind the Sessions page shortcut: one
/// <see cref="ActionKind.ApplicationDevice"/> action pinning a running app's session to the endpoint
/// it currently plays on. Exact match mode on both patterns, so the produced rule reads in the rules
/// editor exactly as if it had been built with the app and device pickers.
/// </summary>
public static class SessionRuleFactory
{
    private const string FallbackRuleName = "New rule";

    public static RoutingRule CreateRule(AudioSession session, AudioEndpoint? endpoint)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new RoutingRule
        {
            Name = RuleName(session),
            Enabled = true,
            Actions = { CreateAction(session, endpoint) },
        };
    }

    public static RuleAction CreateAction(AudioSession session, AudioEndpoint? endpoint)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new RuleAction
        {
            Kind = ActionKind.ApplicationDevice,
            Flow = EndpointFlow.Render,
            AppPattern = session.ProcessName,
            AppMatchMode = PatternMatchMode.Exact,
            // PickerName, not FriendlyName: it is what the Exact-mode device picker stores, and it
            // always equals the endpoint's FriendlyName or DisplayName - both of which the matcher
            // compares against - so the Exact match still resolves.
            DevicePattern = endpoint?.PickerName ?? string.Empty,
            DeviceMatchMode = PatternMatchMode.Exact,
        };
    }

    private static string RuleName(AudioSession session)
    {
        if (!string.IsNullOrWhiteSpace(session.DisplayName)) return session.DisplayName;
        if (!string.IsNullOrWhiteSpace(session.ProcessName)) return session.ProcessName;
        return FallbackRuleName;
    }
}
