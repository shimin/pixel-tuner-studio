using PixelTuner.Core.Layout;
using Xunit;

namespace PixelTuner.Core.Tests.Layout;

public class SvgPathTests
{
    private static void Near(double expected, double actual, double tolerance = 1e-6) => Assert.InRange(actual, expected - tolerance, expected + tolerance);

    private static (double X, double Y, double W, double H) Box(string data, Affine? transform = null) =>
        SvgPath.Bounds(data, transform ?? Affine.Identity)!.Value;

    [Fact]
    public void Straight_commands_in_both_cases_walk_the_outline()
    {
        Assert.Equal((10, 20, 30, 40), Box("M10,20 H40 V60 H10 Z"));
        Assert.Equal((10, 20, 30, 40), Box("m10,20 h30 v40 h-30 z"));
        Assert.Equal((0, 0, 5, 5), Box("M0 0 L5 0 5 5 0 5"));    // numbers after L go on drawing lines
        Assert.Equal((1, 1, 4, 4), Box("m1,1 4,0 0,4 -4,0"));      // after m they are relative lines
    }

    [Fact]
    public void A_subpath_closes_on_its_first_point_and_the_next_drawing_starts_there()
    {
        var subpaths = SvgPath.Flatten("M0,0 L4,0 L4,4 Z L0,9");
        Assert.Equal(2, subpaths.Count);
        Assert.Equal((0.0, 0.0), subpaths[0][^1]);
        Assert.Equal([(0.0, 0.0), (0.0, 9.0)], subpaths[1]);
    }

    [Fact]
    public void Several_subpaths_make_one_set_of_bounds()
    {
        Assert.Equal((0, 0, 30, 15), Box("M0,0 h10 v5 z M20,10 h10 v5 z"));
    }

    [Fact]
    public void A_cubic_curve_is_measured_along_the_curve_and_not_by_its_control_points()
    {
        // The curve from (0,0) to (10,0) with both controls at y = 8 reaches 6 high (3/4 of the control height) in the middle.
        var (x, y, w, h) = Box("M0,0 C0,8 10,8 10,0");
        Near(0, x); Near(0, y, 1e-9); Near(10, w);
        Near(6, h, 0.05);
    }

    [Fact]
    public void A_smooth_curve_continues_the_one_before_it()
    {
        var plain = SvgPath.Flatten("M0,0 C0,8 10,8 10,0 C10,-8 20,-8 20,0")[0];
        var smooth = SvgPath.Flatten("M0,0 C0,8 10,8 10,0 S20,-8 20,0")[0];
        Assert.Equal(plain.Count, smooth.Count);
        for (int i = 0; i < plain.Count; i++)
        {
            Near(plain[i].X, smooth[i].X, 1e-9);
            Near(plain[i].Y, smooth[i].Y, 1e-9);
        }
    }

    [Fact]
    public void A_quadratic_curve_and_its_smooth_form()
    {
        var (_, y, _, h) = Box("M0,0 Q5,10 10,0");
        Near(0, y, 1e-9);
        Near(5, h, 0.05);
        var plain = SvgPath.Flatten("M0,0 Q5,10 10,0 Q15,-10 20,0")[0];
        var smooth = SvgPath.Flatten("M0,0 Q5,10 10,0 T20,0")[0];
        Assert.Equal(plain.Count, smooth.Count);
        Near(plain[^2].Y, smooth[^2].Y, 1e-9);
    }

    [Fact]
    public void Arcs_make_circles_and_ellipses_with_the_right_bounds()
    {
        var (x, y, w, h) = Box("M2,5 A3,3 0 1 0 8,5 A3,3 0 1 0 2,5 Z");
        Near(2, x, 0.01); Near(2, y, 0.01); Near(6, w, 0.02); Near(6, h, 0.02);

        var (ex, ey, ew, eh) = Box("M1,5 A4,2 0 1 0 9,5 A4,2 0 1 0 1,5 Z");
        Near(1, ex, 0.01); Near(3, ey, 0.01); Near(8, ew, 0.02); Near(4, eh, 0.02);
    }

    [Fact]
    public void A_half_circle_arc_bulges_to_the_side_the_flags_say()
    {
        var up = Box("M0,0 A5,5 0 0 1 10,0");     // sweep: clockwise on the screen: over the top
        Near(-5, up.Y, 0.05);
        var down = Box("M0,0 A5,5 0 0 0 10,0");
        Near(0, down.Y, 1e-6);
        Near(5, down.H, 0.05);
    }

    [Fact]
    public void An_arc_with_a_radius_too_small_is_scaled_up_to_reach_and_one_without_a_radius_is_a_line()
    {
        var (_, _, w, _) = Box("M0,0 A1,1 0 0 1 10,0");
        Near(10, w, 1e-6);
        var line = SvgPath.Flatten("M0,0 A0,5 0 0 1 10,0")[0];
        Assert.Equal([(0.0, 0.0), (10.0, 0.0)], line);
    }

    [Fact]
    public void Flags_of_an_arc_may_stand_without_spaces()
    {
        Assert.Equal(Box("M2,5 A3,3 0 1 0 8,5"), Box("M2,5 A3,3 0 108,5"));
    }

    [Fact]
    public void Numbers_may_run_together_the_way_inkscape_writes_them()
    {
        Assert.Equal((0, 0, 1.5, 0.5), Box("M0,0 L1.5.5"));            // 1.5 and .5
        Assert.Equal((0, -3, 5, 3), Box("M0,0L5-3"));                   // a minus starts a number
        var (_, _, w, _) = Box("M0,0 l7.2e-4,0");
        Near(7.2e-4, w, 1e-12);
    }

    [Fact]
    public void Bounds_follow_the_transform()
    {
        var (x, y, w, h) = Box("M0,0 H10 V5 H0 Z", new Affine(2, 0, 0, 3, 100, 200));
        Assert.Equal((100.0, 200.0, 20.0, 15.0), (x, y, w, h));
        var turned = Box("M0,0 H10 V5 H0 Z", CanvasDrawing.Parse("rotate(90)"));
        Near(-5, turned.X); Near(0, turned.Y); Near(5, turned.W); Near(10, turned.H);
    }

    [Fact]
    public void Text_that_is_not_path_data_gives_what_could_be_read_and_nothing_for_nothing()
    {
        Assert.Null(SvgPath.Bounds("", Affine.Identity));
        Assert.Null(SvgPath.Bounds("garbage", Affine.Identity));
        Assert.Equal((0, 0, 4, 0), Box("M0,0 L4,0 L oops"));
        Assert.Empty(SvgPath.Flatten("5 5"));        // numbers with no command
        Assert.Empty(SvgPath.Flatten("X0,0"));
    }

    [Fact]
    public void Every_outline_of_the_built_in_schemas_has_bounds_on_its_page()
    {
        foreach (var (name, _) in CanvasSchemas.All)
        {
            var drawing = CanvasDrawing.TryParse(CanvasSchemas.Read(name))!;
            foreach (var shape in drawing.Shapes)
            {
                var (x, y, w, h) = shape.Bounds;
                Assert.True(w > 0 && h > 0, $"{name} {shape.Id} has no size");
                Assert.True(x >= drawing.X - 1 && y >= drawing.Y - 1 && x + w <= drawing.X + drawing.Width + 1 && y + h <= drawing.Y + drawing.Height + 1,
                    $"{name} {shape.Id} lies off the page: {x},{y} {w}x{h}");
            }
        }
    }

    [Fact]
    public void The_head_of_the_human_schema_is_where_the_picture_has_it()
    {
        var head = CanvasDrawing.TryParse(CanvasSchemas.Read("human"))!.Find("path3336")!;
        var (x, y, w, h) = head.Bounds;
        // The path starts at y 273.09 under a layer that moves everything up by 272.83: the head is at the top of the page.
        Near(0.26, y, 0.5);
        Assert.InRange(w, 90, 130);
        Assert.InRange(h, 90, 140);
        Assert.InRange(x, 100, 150);
    }
}
