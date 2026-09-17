using AwesomeAssertions;

using Earmark.Core.Models;

using Xunit;

namespace Earmark.Core.Tests;

public class WindowBoundsResolverTests
{
    private static readonly WindowBounds Primary = new(0, 0, 1920, 1080);
    private static readonly WindowBounds Secondary = new(1920, 0, 1280, 1024);

    private static WindowBounds Resolve(WindowBounds? saved, params WindowBounds[] workAreas) =>
        WindowBoundsResolver.Resolve(saved, workAreas);

    private static void ShouldSitInside(WindowBounds bounds, WindowBounds area)
    {
        bounds.X.Should().BeGreaterThanOrEqualTo(area.X);
        bounds.Y.Should().BeGreaterThanOrEqualTo(area.Y);
        bounds.Right.Should().BeLessThanOrEqualTo(area.Right);
        bounds.Bottom.Should().BeLessThanOrEqualTo(area.Bottom);
    }

    [Fact]
    public void Bounds_inside_the_work_area_are_kept()
    {
        var saved = new WindowBounds(200, 150, 360, 460);
        Resolve(saved, Primary).Should().Be(saved);
    }

    [Fact]
    public void Bounds_on_a_second_work_area_are_kept()
    {
        var saved = new WindowBounds(2100, 200, 360, 460);
        Resolve(saved, Primary, Secondary).Should().Be(saved);
    }

    [Fact]
    public void Bounds_off_every_work_area_are_pulled_back_on()
    {
        var saved = new WindowBounds(4000, 3000, 360, 460);
        var resolved = Resolve(saved, Primary);

        ShouldSitInside(resolved, Primary);
        resolved.Width.Should().Be(360);
        resolved.Height.Should().Be(460);
    }

    [Fact]
    public void Bounds_hanging_off_an_edge_are_nudged_into_view()
    {
        var saved = new WindowBounds(1800, 900, 360, 460);
        var resolved = Resolve(saved, Primary);

        ShouldSitInside(resolved, Primary);
        resolved.Width.Should().Be(360);
        resolved.Height.Should().Be(460);
        resolved.Should().Be(new WindowBounds(1560, 620, 360, 460));
    }

    [Fact]
    public void Bounds_larger_than_the_work_area_are_shrunk_to_fit()
    {
        var saved = new WindowBounds(-100, -100, 2400, 1400);
        var resolved = Resolve(saved, Primary);

        resolved.Should().Be(new WindowBounds(0, 0, 1920, 1080));
    }

    [Fact]
    public void No_saved_bounds_centres_the_default_size_on_the_first_work_area()
    {
        var resolved = Resolve(null, Primary, Secondary);

        resolved.Width.Should().Be(WindowBoundsResolver.DefaultWidth);
        resolved.Height.Should().Be(WindowBoundsResolver.DefaultHeight);
        resolved.X.Should().Be((1920 - WindowBoundsResolver.DefaultWidth) / 2);
        resolved.Y.Should().Be((1080 - WindowBoundsResolver.DefaultHeight) / 2);
    }

    [Fact]
    public void Without_work_areas_the_saved_bounds_survive_untouched()
    {
        var saved = new WindowBounds(4000, 3000, 360, 460);
        Resolve(saved).Should().Be(saved);
    }

    [Fact]
    public void Without_work_areas_and_without_saved_bounds_the_default_size_lands_at_origin()
    {
        Resolve(null).Should().Be(new WindowBounds(0, 0, WindowBoundsResolver.DefaultWidth, WindowBoundsResolver.DefaultHeight));
    }
}
