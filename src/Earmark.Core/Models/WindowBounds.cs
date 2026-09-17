namespace Earmark.Core.Models;

/// <summary>A window rectangle in virtual-screen coordinates, in physical pixels.</summary>
public readonly record struct WindowBounds(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;
}

/// <summary>
/// Decides where a remembered window should reopen. Saved bounds can name a screen that is no longer
/// attached (laptop undocked, monitor unplugged), so bounds that fall outside every current work area
/// are pulled back onto the nearest one instead of reopening where nobody can see them.
/// </summary>
public static class WindowBoundsResolver
{
    public const int DefaultWidth = 360;
    public const int DefaultHeight = 460;

    /// <summary>
    /// Resolves the bounds to open at from what was saved and the work areas available now. With no
    /// work areas to reason about, the saved bounds (or the default size) are handed back untouched.
    /// </summary>
    public static WindowBounds Resolve(WindowBounds? saved, IReadOnlyList<WindowBounds> workAreas)
    {
        ArgumentNullException.ThrowIfNull(workAreas);

        if (workAreas.Count == 0)
        {
            return saved ?? new WindowBounds(0, 0, DefaultWidth, DefaultHeight);
        }

        if (saved is not { } bounds)
        {
            return Centre(DefaultWidth, DefaultHeight, workAreas[0]);
        }

        return Clamp(bounds, HostFor(bounds, workAreas));
    }

    private static WindowBounds Centre(int width, int height, WindowBounds area) =>
        new(area.X + ((area.Width - width) / 2), area.Y + ((area.Height - height) / 2), width, height);

    /// <summary>The work area the bounds overlap most, or - when they overlap none - the one whose
    /// centre is nearest.</summary>
    private static WindowBounds HostFor(WindowBounds bounds, IReadOnlyList<WindowBounds> workAreas)
    {
        var best = workAreas[0];
        long bestOverlap = -1;
        long bestDistance = long.MaxValue;

        foreach (var area in workAreas)
        {
            var overlap = OverlapArea(bounds, area);
            if (overlap > bestOverlap)
            {
                best = area;
                bestOverlap = overlap;
                bestDistance = CentreDistanceSquared(bounds, area);
            }
            else if (overlap == bestOverlap && bestOverlap == 0)
            {
                var distance = CentreDistanceSquared(bounds, area);
                if (distance < bestDistance)
                {
                    best = area;
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    private static WindowBounds Clamp(WindowBounds bounds, WindowBounds area)
    {
        var width = Math.Min(bounds.Width, area.Width);
        var height = Math.Min(bounds.Height, area.Height);
        var x = Math.Clamp(bounds.X, area.X, area.Right - width);
        var y = Math.Clamp(bounds.Y, area.Y, area.Bottom - height);

        return new WindowBounds(x, y, width, height);
    }

    private static long OverlapArea(WindowBounds bounds, WindowBounds area)
    {
        long width = Math.Min(bounds.Right, area.Right) - Math.Max(bounds.X, area.X);
        long height = Math.Min(bounds.Bottom, area.Bottom) - Math.Max(bounds.Y, area.Y);

        return width <= 0 || height <= 0 ? 0 : width * height;
    }

    private static long CentreDistanceSquared(WindowBounds bounds, WindowBounds area)
    {
        long dx = (bounds.X + (bounds.Width / 2)) - (area.X + (area.Width / 2));
        long dy = (bounds.Y + (bounds.Height / 2)) - (area.Y + (area.Height / 2));

        return (dx * dx) + (dy * dy);
    }
}
