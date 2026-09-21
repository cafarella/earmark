using Earmark.Core.Models;

namespace Earmark.Core.Routing;

/// <summary>A rule that mentions this app but did not place it, and why.</summary>
public sealed record RouteNearMiss(string RuleName, string Reason);

public sealed record RouteExplanation(string Summary, Guid? RuleId, IReadOnlyList<RouteNearMiss> NearMisses);

/// <summary>
/// Puts one app session's routing into words: which rule placed it, or - when nothing did - which
/// rules mention the app and what stopped each of them. Reads the same first-match-wins view of the
/// rule list as <see cref="RuleMatcher"/>, so the explanation can never disagree with the routing.
/// </summary>
public static class RouteExplainer
{
    public const int MaxNearMisses = 3;

    public static RouteExplanation Explain(
        AudioSession session,
        EndpointFlow flow,
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

        var match = matcher.FindAppRoute(session, flow, rules, endpoints, sessions);

        var summary = match is null
            ? "No rule targets this app, so Windows decides where it plays."
            : $"On {match.Endpoint.DisplayName} because rule \"{DisplayName(match.Rule)}\" matched.";

        var nearMisses = new List<RouteNearMiss>();
        foreach (var rule in rules)
        {
            if (nearMisses.Count == MaxNearMisses)
            {
                break;
            }

            var reason = MissReason(rule, session, flow, endpoints, sessions, matcher, match?.Action);
            if (reason is not null)
            {
                nearMisses.Add(new RouteNearMiss(DisplayName(rule), reason));
            }
        }

        return new RouteExplanation(summary, match?.Rule.Id, nearMisses);
    }

    /// <summary>Why this rule's first app action naming the session didn't place it, or null when
    /// the rule never mentions the app (or only does so through the action that won).</summary>
    private static string? MissReason(
        RoutingRule rule,
        AudioSession session,
        EndpointFlow flow,
        IReadOnlyList<AudioEndpoint> endpoints,
        IReadOnlyList<AudioSession> sessions,
        IRuleMatcher matcher,
        RuleAction? winningAction)
    {
        var met = matcher.ConditionsMet(rule, endpoints, sessions);

        foreach (var (action, branchIsLive) in AppActions(rule, met))
        {
            // Only the flow being explained: a rule routing this app's microphone never competed for
            // where its playback went, so listing it would read as a race it lost.
            if (action.EffectiveFlow != flow || ReferenceEquals(action, winningAction) || !MatchesApp(action, session))
            {
                continue;
            }

            if (!rule.Enabled)
            {
                return "disabled";
            }

            if (!action.IsValid)
            {
                return "not finished being set up";
            }

            if (!branchIsLive)
            {
                return "its conditions are not met";
            }

            if (!AnyEndpointMatches(action, endpoints))
            {
                return $"its device \"{action.DevicePattern}\" is not connected";
            }

            return "an earlier rule already routes this app";
        }

        return null;
    }

    /// <summary>Both branches' app-routing actions, each tagged with whether its branch is the live
    /// one - a rule parked in the dormant branch is still worth explaining.</summary>
    private static IEnumerable<(RuleAction Action, bool BranchIsLive)> AppActions(RoutingRule rule, bool conditionsMet)
    {
        foreach (var action in rule.Actions)
        {
            if (action.Kind == ActionKind.ApplicationDevice)
            {
                yield return (action, conditionsMet);
            }
        }

        foreach (var action in rule.ElseActions)
        {
            if (action.Kind == ActionKind.ApplicationDevice)
            {
                yield return (action, !conditionsMet);
            }
        }
    }

    private static bool MatchesApp(RuleAction action, AudioSession session) =>
        PatternMatcher.Matches(action.AppMatchMode, action.AppPattern, session.ProcessName) ||
        PatternMatcher.Matches(action.AppMatchMode, action.AppPattern, session.ExecutablePath);

    private static bool AnyEndpointMatches(RuleAction action, IReadOnlyList<AudioEndpoint> endpoints) =>
        endpoints.Any(e =>
            e.Flow == action.EffectiveFlow && e.State == EndpointState.Active &&
            (PatternMatcher.Matches(action.DeviceMatchMode, action.DevicePattern, e.FriendlyName) ||
             PatternMatcher.Matches(action.DeviceMatchMode, action.DevicePattern, e.DisplayName)));

    private static string DisplayName(RoutingRule rule) =>
        string.IsNullOrWhiteSpace(rule.Name) ? "Unnamed rule" : rule.Name;
}
