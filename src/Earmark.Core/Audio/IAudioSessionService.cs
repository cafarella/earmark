using Earmark.Core.Models;

namespace Earmark.Core.Audio;

public sealed record AudioSessionEvent(AudioSession Session);

public sealed record AudioSessionRemovedEvent(uint ProcessId);

public interface IAudioSessionService
{
    IReadOnlyList<AudioSession> GetSessions();
    event EventHandler<AudioSessionEvent>? SessionAdded;
    event EventHandler<AudioSessionRemovedEvent>? SessionRemoved;
    event EventHandler? SessionsChanged;

    /// <summary>Sets the session's volume only if it differs by more than 0.5%. Returns true when a write happened.</summary>
    bool SetVolume(AudioSession session, float level);

    /// <summary>Sets the session's mute only if it differs. Returns true when a write happened.</summary>
    bool SetMuted(AudioSession session, bool muted);

    /// <summary>
    /// Fires when any watched session's volume or mute changes (Volume Mixer, the app itself, or our
    /// own writes). Carries no session identity: the COM callback doesn't provide one. Raised on a
    /// COM callback thread.
    /// </summary>
    event EventHandler? ExternalSessionVolumeChanged;
}
