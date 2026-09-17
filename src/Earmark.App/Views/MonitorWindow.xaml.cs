using Earmark.App.Services;
using Earmark.App.Settings;
using Earmark.App.ViewModels;

using Earmark.Core.Models;

using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

using Windows.Graphics;

namespace Earmark.App.Views;

/// <summary>
/// Always-on-top level monitor: a read-only view over the device cards the Devices page already
/// builds. Controls live on that page and in Quick Controls; this window only watches.
/// </summary>
public sealed partial class MonitorWindow : Window
{
    private readonly ISettingsService _settings;
    private ISystemBackdropControllerWithTargets? _backdropController;
    private readonly SystemBackdropConfiguration _backdropConfig = new() { IsInputActive = true };
    private BackdropMode? _appliedBackdrop;

    public MonitorWindow(HomeViewModel viewModel, ISettingsService settings)
    {
        ViewModel = viewModel;
        _settings = settings;
        InitializeComponent();

        SystemBackdrop = null;
        AppWindow.Title = "Audio monitor";
        Root.DataContext = ViewModel;

        ConfigureWindow();
        ApplyTheme();
        ApplyBackdrop();

        Root.ActualThemeChanged += (_, _) => UpdateBackdropTheme();
        _settings.SettingsChanged += OnSettingsChanged;
        Closed += (_, _) =>
        {
            _settings.SettingsChanged -= OnSettingsChanged;
            _backdropController?.Dispose();
            _backdropController = null;
        };
    }

    public HomeViewModel ViewModel { get; }

    /// <summary>Current position and size, for the owning service to persist.</summary>
    public WindowBounds CurrentBounds => new(
        AppWindow.Position.X, AppWindow.Position.Y, AppWindow.Size.Width, AppWindow.Size.Height);

    public void MoveTo(WindowBounds bounds)
    {
        AppWindow.Resize(new SizeInt32(bounds.Width, bounds.Height));
        AppWindow.Move(new PointInt32(bounds.X, bounds.Y));
    }

    private void ConfigureWindow()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = true;
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (DispatcherQueue.HasThreadAccess) ApplySettings();
        else DispatcherQueue.TryEnqueue(ApplySettings);
    }

    private void ApplySettings()
    {
        ApplyTheme();
        ApplyBackdrop();
    }

    private void ApplyTheme()
    {
        var element = _settings.Current.Theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        if (Root.RequestedTheme != element)
        {
            Root.RequestedTheme = element;
        }
        UpdateBackdropTheme();
    }

    private void ApplyBackdrop()
    {
        var mode = _settings.Current.Backdrop;
        if (_appliedBackdrop == mode) return;
        _appliedBackdrop = mode;

        _backdropController = WindowBackdrop.Apply(this, mode, _backdropConfig, _backdropController);
        SolidBackdrop.Visibility = _backdropController is null ? Visibility.Visible : Visibility.Collapsed;
        UpdateBackdropTheme();
    }

    private void UpdateBackdropTheme()
    {
        var effective = Root.RequestedTheme == ElementTheme.Default ? Root.ActualTheme : Root.RequestedTheme;
        _backdropConfig.Theme = effective == ElementTheme.Light ? SystemBackdropTheme.Light : SystemBackdropTheme.Dark;
    }
}
