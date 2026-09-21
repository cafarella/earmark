using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Windows.Storage.Pickers;

using WinRT.Interop;

namespace Earmark.App.Services;

public interface IExecutablePicker
{
    /// <summary>Returns the full path of the chosen .exe, or null if the user cancelled.</summary>
    Task<string?> PickAsync();
}

internal sealed class ExecutablePicker : IExecutablePicker
{
    private readonly ILogger<ExecutablePicker> _logger;

    public ExecutablePicker(ILogger<ExecutablePicker> logger)
    {
        _logger = logger;
    }

    public async Task<string?> PickAsync()
    {
        try
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
            };

            // Unpackaged WinUI 3 has no implicit window context, so the picker throws instead of
            // opening unless it is initialised with the main window's HWND. FileTypeFilter must
            // also be non-empty for the same reason.
            var window = App.Current.Services.GetRequiredService<MainWindow>();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));

            picker.FileTypeFilter.Add(".exe");
            picker.FileTypeFilter.Add("*");

            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ExecutablePicker: file picker failed");
            return null;
        }
    }
}
