using Earmark.App.Controls;
using Earmark.App.ViewModels;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Earmark.App.Views;

public sealed partial class SessionsPage : Page
{
    public SessionsPage(SessionsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public SessionsViewModel ViewModel { get; }

    /// <summary>Per-session rule menu: create a rule from this session, or fold it into an existing
    /// one. Built in code rather than declared in XAML because the rule list is dynamic.</summary>
    private void OnSessionMenuClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement button || button.Tag is not SessionRow row) return;

        var flyout = new MenuFlyout();

        // Informational, so it's fenced off from the rule actions below it.
        var why = new MenuFlyoutItem
        {
            Text = "Why is it here?",
            Icon = new FontIcon { Glyph = "" },
            Tag = new RouteExplainTarget(row, button),
        };
        why.Click += OnWhyIsItHereClicked;
        flyout.Items.Add(why);
        flyout.Items.Add(new MenuFlyoutSeparator());

        var create = new MenuFlyoutItem
        {
            Text = "Create rule…",
            Tag = row,
            IsEnabled = row.CanCreateRule,
        };
        create.Click += OnCreateRuleClicked;
        flyout.Items.Add(create);

        var entries = ViewModel.RulesForMenu(row);
        var addTo = new MenuFlyoutSubItem { Text = "Add to existing rule", IsEnabled = entries.Count > 0 };
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var item = new MenuFlyoutItem { Text = entry.Name, Tag = new RuleMenuTarget(row, entry.Id) };
            item.Click += OnAddToRuleClicked;
            addTo.Items.Add(item);

            // The rules that already match this session are listed first; fence them off from the rest.
            if (entry.AlreadyMatches && i + 1 < entries.Count && !entries[i + 1].AlreadyMatches)
            {
                addTo.Items.Add(new MenuFlyoutSeparator());
            }
        }
        flyout.Items.Add(addTo);

        flyout.ShowAt(button);
    }

    private void OnWhyIsItHereClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: RouteExplainTarget target }) return;

        // Defer so the menu finishes dismissing; opening both at once leaves the menu's
        // light-dismiss layer above the explanation.
        DispatcherQueue.TryEnqueue(() =>
            RouteExplanationFlyout.ShowAt(target.Anchor, ViewModel.ExplainRoute(target.Row)));
    }

    private async void OnCreateRuleClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement item || item.Tag is not SessionRow row) return;

        var id = await ViewModel.CreateRuleAsync(row);
        if (id is Guid ruleId) OpenRule(ruleId);
    }

    private async void OnAddToRuleClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement item || item.Tag is not RuleMenuTarget target) return;

        var id = await ViewModel.AddToRuleAsync(target.Row, target.RuleId);
        if (id is Guid ruleId) OpenRule(ruleId);
    }

    private static void OpenRule(Guid ruleId)
    {
        var services = App.Current.Services;
        services.GetRequiredService<RulesViewModel>().RequestExpand(ruleId);
        services.GetRequiredService<MainWindow>().NavigateByTag("Rules");
    }

    private sealed record RuleMenuTarget(SessionRow Row, Guid RuleId);

    /// <summary>The menu button travels with the click: the MenuFlyoutItem is gone by the time the
    /// handler runs, so the explanation flyout needs it to open against.</summary>
    private sealed record RouteExplainTarget(SessionRow Row, FrameworkElement Anchor);
}
