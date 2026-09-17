using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Earmark.App.Services;
using Earmark.Core.Audio;
using Earmark.Core.Models;
using Earmark.Core.Routing;
using Earmark.Core.Services;

namespace Earmark.App.ViewModels;

public partial class SessionsViewModel : ObservableObject, IDisposable
{
    private const string UntitledRuleName = "Untitled rule";

    private static readonly TimeSpan DebounceWindow = TimeSpan.FromMilliseconds(250);

    private readonly IAudioSessionService _sessions;
    private readonly IAudioEndpointService _endpoints;
    private readonly IRoutingApplier _applier;
    private readonly IDispatcherQueueProvider _dispatcher;
    private readonly IRulesService _rules;
    private readonly Lock _gate = new();

    private CancellationTokenSource? _refreshCts;

    public SessionsViewModel(
        IAudioSessionService sessions,
        IAudioEndpointService endpoints,
        IRoutingApplier applier,
        IDispatcherQueueProvider dispatcher,
        IRulesService rules)
    {
        _sessions = sessions;
        _endpoints = endpoints;
        _applier = applier;
        _dispatcher = dispatcher;
        _rules = rules;

        _sessions.SessionsChanged += OnSessionsChanged;
        QueueRefresh();
    }

    public ObservableCollection<SessionRow> Items { get; } = new();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    public bool HasItems => Items.Count > 0;
    public bool IsEmpty => !IsLoading && Items.Count == 0;

    [RelayCommand]
    private void Refresh() => QueueRefresh();

    [RelayCommand]
    private async Task ReapplyAllAsync() => await _applier.ApplyAllAsync(force: true);

    /// <summary>
    /// Every rule, the ones already targeting this app first. OrderByDescending is stable, so each
    /// group keeps the list order the Rules page shows - which is also the order rules apply in.
    /// </summary>
    public IReadOnlyList<RuleMenuEntry> RulesForMenu(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return _rules.Rules
            .Select(rule => new RuleMenuEntry(
                rule.Id,
                string.IsNullOrWhiteSpace(rule.Name) ? UntitledRuleName : rule.Name,
                TargetsApp(rule, row.Session)))
            .OrderByDescending(entry => entry.AlreadyMatches)
            .ToList();
    }

    /// <summary>Creates a rule pinning this session's app to the endpoint it plays on today.</summary>
    public async Task<Guid?> CreateRuleAsync(SessionRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var rule = SessionRuleFactory.CreateRule(row.Session, row.CurrentEndpoint);
        await _rules.UpsertAsync(rule);
        return rule.Id;
    }

    /// <summary>Appends that same action to an existing rule. Null if the rule was deleted meanwhile.</summary>
    public async Task<Guid?> AddToRuleAsync(SessionRow row, Guid ruleId)
    {
        ArgumentNullException.ThrowIfNull(row);

        var rule = _rules.Rules.FirstOrDefault(r => r.Id == ruleId);
        if (rule is null) return null;

        rule.Actions.Add(SessionRuleFactory.CreateAction(row.Session, row.CurrentEndpoint));
        await _rules.UpsertAsync(rule);
        return rule.Id;
    }

    private static bool TargetsApp(RoutingRule rule, AudioSession session) =>
        rule.Actions.Concat(rule.ElseActions).Any(action => MatchesApp(action, session));

    // App patterns are tested against both the process name and the full exe path, the way every
    // other app match in this codebase works.
    private static bool MatchesApp(RuleAction action, AudioSession session) =>
        !string.IsNullOrWhiteSpace(action.AppPattern)
        && (PatternMatcher.Matches(action.AppMatchMode, action.AppPattern, session.ProcessName)
            || PatternMatcher.Matches(action.AppMatchMode, action.AppPattern, session.ExecutablePath));

    private void OnSessionsChanged(object? sender, EventArgs e) => QueueRefresh();

    private void QueueRefresh()
    {
        CancellationToken token;
        lock (_gate)
        {
            _refreshCts?.Cancel();
            _refreshCts = new CancellationTokenSource();
            token = _refreshCts.Token;
        }

        _ = RefreshAsync(token);
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(DebounceWindow, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _dispatcher.Enqueue(() =>
        {
            IsLoading = true;
            Items.Clear();
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(IsEmpty));
        });

        await Task.Run(() =>
        {
            var endpointById = _endpoints.GetEndpoints()
                .GroupBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var session in _sessions.GetSessions())
            {
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                // The System Sounds session attaches to every active render endpoint, so
                // it shows up as a row per device - duplicated noise the user can't act on.
                // Drop it the same way the Home page apps row does.
                if (session.IsSystemSounds) continue;

                endpointById.TryGetValue(session.CurrentEndpointId, out var endpoint);
                var row = new SessionRow(session, endpoint);
                _dispatcher.Enqueue(() =>
                {
                    if (!ct.IsCancellationRequested)
                    {
                        Items.Add(row);
                        OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(IsEmpty));
                    }
                });
            }
        }, ct).ConfigureAwait(false);

        _dispatcher.Enqueue(() =>
        {
            IsLoading = false;
        });
    }

    public void Dispose()
    {
        _sessions.SessionsChanged -= OnSessionsChanged;
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
    }
}

/// <summary>A rule as offered by the Sessions page's "add to rule" menu.</summary>
public sealed record RuleMenuEntry(Guid Id, string Name, bool AlreadyMatches);

public sealed record SessionRow(AudioSession Session, AudioEndpoint? CurrentEndpoint)
{
    public string Title => Session.IsSystemSounds ? "System Sounds" : Session.DisplayName;
    public string Subtitle => Session.IsSystemSounds
        ? "system"
        : string.IsNullOrEmpty(Session.ExecutablePath) ? Session.ProcessName : Session.ExecutablePath;
    public string CurrentEndpointName => CurrentEndpoint?.DisplayName ?? "(unknown)";
    public bool IsActive => Session.State == SessionState.Active;

    /// <summary>System sounds have no process to pattern-match, so they get no rule shortcut.</summary>
    public bool CanCreateRule => !Session.IsSystemSounds && !string.IsNullOrWhiteSpace(Session.ProcessName);
}
