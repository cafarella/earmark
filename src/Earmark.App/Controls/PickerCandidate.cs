namespace Earmark.App.Controls;

/// <summary>
/// One entry in a <see cref="PickerComboBox"/> list. <paramref name="Value"/> is what gets written
/// to the rule; <paramref name="Note"/> is display-only trailing text ("offline", "use as typed"),
/// so a label never leaks into the saved pattern.
/// </summary>
public sealed record PickerCandidate(string Value, string? Note = null)
{
    public bool HasNote => !string.IsNullOrEmpty(Note);
}
