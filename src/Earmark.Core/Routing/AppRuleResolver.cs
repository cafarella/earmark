using Earmark.Core.Models;

namespace Earmark.Core.Routing;

/// <summary>The rule-driven volume / mute target for one app audio session. Null means no enabled,
/// condition-satisfied rule targets that dimension.</summary>
public readonly record struct AppRuleTargets(
    DeviceRuleTarget<float>? Volume,
    DeviceRuleTarget<bool>? Muted);

/// <summary>
/// Per-session counterpart of <see cref="DeviceRuleResolver"/>: the volume / mute an app session is
/// targeted with. First-match-wins over enabled rules whose conditions are met, each dimension
/// independently. Shared by the routing applier, rule status and shadow analysis so they agree.
/// </summary>
public static class AppRuleResolver
{
    public static AppRuleTargets Resolve(
        AudioSession session,
        IReadOnlyList<RoutingRule> rules,
        IReadOnlyList<AudioEndpoint> endpoints,
        IReadOnlyList<AudioSession> sessions,
        IRuleMatcher matcher)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(matcher);

        DeviceRuleTarget<float>? volume = null;
        DeviceRuleTarget<bool>? muted = null;

        foreach (var rule in rules)
        {
            if (volume.HasValue && muted.HasValue) break;
            if (!rule.Enabled) continue;

            var met = matcher.ConditionsMet(rule, endpoints, sessions);
            var label = string.IsNullOrEmpty(rule.Name) ? rule.Id.ToString() : rule.Name;

            foreach (var action in rule.ActiveActions(met))
            {
                if (volume.HasValue && muted.HasValue) break;
                if (!action.IsValid) continue;
                if (!action.IsAppVolumeAction && !action.IsAppMuteAction) continue;
                if (!TargetsSession(action, session, endpoints)) continue;

                if (action.IsAppVolumeAction && !volume.HasValue)
                {
                    volume = new DeviceRuleTarget<float>(action.Volume, label, rule.Id, action.Pinned);
                }
                else if (action.IsAppMuteAction && !muted.HasValue)
                {
                    muted = new DeviceRuleTarget<bool>(action.Muted, label, rule.Id, action.Pinned);
                }
            }
        }

        return new AppRuleTargets(volume, muted);
    }

    /// <summary>True when the action's app pattern matches the session's process name or executable
    /// path, and its device filter is blank or matches the endpoint the session plays on.</summary>
    internal static bool TargetsSession(RuleAction action, AudioSession session, IReadOnlyList<AudioEndpoint> endpoints)
    {
        if (!PatternMatcher.Matches(action.AppMatchMode, action.AppPattern, session.ProcessName) &&
            !PatternMatcher.Matches(action.AppMatchMode, action.AppPattern, session.ExecutablePath))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(action.DevicePattern))
        {
            return true;
        }

        var endpoint = endpoints.FirstOrDefault(e => string.Equals(e.Id, session.CurrentEndpointId, StringComparison.OrdinalIgnoreCase));
        return endpoint is not null
            && (PatternMatcher.Matches(action.DeviceMatchMode, action.DevicePattern, endpoint.FriendlyName)
                || PatternMatcher.Matches(action.DeviceMatchMode, action.DevicePattern, endpoint.DisplayName));
    }
}
