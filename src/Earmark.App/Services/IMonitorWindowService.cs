using Earmark.App.Settings;
using Earmark.App.ViewModels;
using Earmark.App.Views;
using Earmark.Core.Models;

using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

using Windows.Foundation;

namespace Earmark.App.Services;

public interface IMonitorWindowService
{
    bool IsOpen { get; }
    void Show();
    void Hide();
    void Toggle();
}

/// <summary>
/// Owns the pop-out audio monitor window: one at a time, opened on demand, and reopened where the
/// user last left it.
/// </summary>
internal sealed class MonitorWindowService : IMonitorWindowService
{
    private static readonly TimeSpan BoundsSaveDelay = TimeSpan.FromMilliseconds(500);

    private readonly HomeViewModel _viewModel;
    private readonly ISettingsService _settings;
    private readonly ILogger<MonitorWindowService> _logger;

    private MonitorWindow? _window;
    private DispatcherTimer? _boundsTimer;
    private TypedEventHandler<AppWindow, AppWindowChangedEventArgs>? _changedHandler;
    private TypedEventHandler<object, WindowEventArgs>? _closedHandler;

    public MonitorWindowService(
        HomeViewModel viewModel,
        ISettingsService settings,
        ILogger<MonitorWindowService> logger)
    {
        _viewModel = viewModel;
        _settings = settings;
        _logger = logger;
    }

    public bool IsOpen => _window is not null;

    public void Show()
    {
        if (_window is { } existing)
        {
            existing.Activate();
            return;
        }

        var window = new MonitorWindow(_viewModel, _settings);

        var bounds = WindowBoundsResolver.Resolve(_settings.Current.MonitorBounds, WorkAreas());
        if (_settings.Current.MonitorBounds is null)
        {
            // DefaultWidth/Height are physical pixels at 100%; AppWindow.Resize is physical too, so
            // an unscaled default opens too small on a 150%/200% display.
            var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(window)) / 96.0;
            bounds = new WindowBounds(
                bounds.X, bounds.Y,
                (int)Math.Round(bounds.Width * scale),
                (int)Math.Round(bounds.Height * scale));
        }
        window.MoveTo(bounds);

        _boundsTimer = new DispatcherTimer { Interval = BoundsSaveDelay };
        _boundsTimer.Tick += OnBoundsTimerTick;

        _changedHandler = OnAppWindowChanged;
        _closedHandler = OnWindowClosed;
        window.AppWindow.Changed += _changedHandler;
        window.Closed += _closedHandler;

        _window = window;
        // The cards are singletons shared with the Devices page, so a stale expansion would
        // otherwise survive a close/reopen.
        foreach (var card in _viewModel.VisibleCards)
        {
            card.IsMonitorExpanded = false;
        }

        window.Activate();
        _viewModel.ResumePeakPollingForMonitor();
        _logger.LogInformation("Audio monitor opened");
    }

    public void Hide() => _window?.Close();

    public void Toggle()
    {
        if (IsOpen) Hide();
        else Show();
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidPositionChange && !args.DidSizeChange) return;

        // Changed fires once per pixel of a drag or resize, so restart the timer on every one and
        // write only after the window has been still for BoundsSaveDelay.
        _boundsTimer?.Stop();
        _boundsTimer?.Start();
    }

    private void OnBoundsTimerTick(object? sender, object e)
    {
        _boundsTimer?.Stop();
        PersistBounds();
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (_window is not { } window) return;

        if (_boundsTimer is { } timer)
        {
            timer.Stop();
            timer.Tick -= OnBoundsTimerTick;
            _boundsTimer = null;
        }

        // Unconditional: a move or resize that ended inside the debounce window would otherwise be
        // lost, which is exactly the drag the user finished with.
        PersistBounds();

        if (_changedHandler is not null) window.AppWindow.Changed -= _changedHandler;
        if (_closedHandler is not null) window.Closed -= _closedHandler;
        _changedHandler = null;
        _closedHandler = null;
        _window = null;
        _viewModel.PausePeakPollingForMonitor();

        _logger.LogInformation("Audio monitor closed");
    }

    private void PersistBounds()
    {
        if (_window is not { } window) return;

        try
        {
            _settings.Current.MonitorBounds = window.CurrentBounds;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audio monitor: could not read window bounds");
            return;
        }

        _ = SaveSettingsAsync();
    }

    private async Task SaveSettingsAsync()
    {
        try { await _settings.SaveAsync().ConfigureAwait(false); }
        catch (Exception ex) { _logger.LogWarning(ex, "Audio monitor: saving window bounds failed"); }
    }

    /// <summary>Current work areas, primary display first so an unremembered monitor centres on the
    /// screen the user is most likely looking at. Empty when the displays can't be read, which the
    /// bounds resolver treats as "keep what was saved".</summary>
    private List<WindowBounds> WorkAreas()
    {
        var areas = new List<WindowBounds>();

        try
        {
            var displays = DisplayArea.FindAll();
            var primaryId = DisplayArea.Primary?.DisplayId.Value;

            // Indexed, not foreach/LINQ: the projection of this WinRT vector view throws
            // InvalidCastException when asked for IEnumerable<DisplayArea>, so iterating it at all
            // takes the window down. The indexer is fine, and it also gives us primary-first
            // ordering without a sort.
            for (var i = 0; i < displays.Count; i++)
            {
                var display = displays[i];
                var bounds = new WindowBounds(
                    display.WorkArea.X, display.WorkArea.Y, display.WorkArea.Width, display.WorkArea.Height);

                if (primaryId is { } id && display.DisplayId.Value == id) areas.Insert(0, bounds);
                else areas.Add(bounds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audio monitor: could not read display work areas");
            areas.Clear();
        }

        return areas;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);
}
