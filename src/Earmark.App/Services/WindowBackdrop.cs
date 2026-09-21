using Earmark.App.Settings;

using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;

using WinRT;

namespace Earmark.App.Services;

/// <summary>
/// Builds the system backdrop controller for a window. Every window needs the same Mica / Acrylic
/// choice, and AGENTS.md requires a chrome change to reach all of them, so the logic lives here
/// instead of being copied per window. Callers keep their own <see cref="SystemBackdropConfiguration"/>
/// because they differ: the main window tracks activation, the overlays pin input active.
/// </summary>
internal static class WindowBackdrop
{
    /// <summary>
    /// Disposes <paramref name="existing"/> and returns the controller for <paramref name="mode"/>,
    /// or null when the mode is Solid or the material is unsupported - the caller then shows its own
    /// opaque fill.
    /// </summary>
    public static ISystemBackdropControllerWithTargets? Apply(
        Window window,
        BackdropMode mode,
        SystemBackdropConfiguration config,
        ISystemBackdropControllerWithTargets? existing)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(config);

        existing?.Dispose();

        ISystemBackdropControllerWithTargets? controller = mode switch
        {
            BackdropMode.Acrylic when DesktopAcrylicController.IsSupported() => new DesktopAcrylicController(),
            BackdropMode.Mica when MicaController.IsSupported() => new MicaController { Kind = MicaKind.Base },
            _ => null,
        };

        if (controller is null)
        {
            return null;
        }

        controller.SetSystemBackdropConfiguration(config);
        controller.AddSystemBackdropTarget(window.As<ICompositionSupportsSystemBackdrop>());
        return controller;
    }
}
