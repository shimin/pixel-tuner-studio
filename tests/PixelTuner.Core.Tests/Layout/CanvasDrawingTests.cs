using PixelTuner.Core.Layout;
using Xunit;

namespace PixelTuner.Core.Tests.Layout;

public class CanvasDrawingTests
{
    private static CanvasDrawing Read(string svg) => CanvasDrawing.TryParse(svg)!;

    private static void Near(double expected, double actual) => Assert.InRange(actual, expected - 1e-6, expected + 1e-6);

    [Fact]
    public void The_human_schema_has_its_outlines_under_the_layer_with_the_ids_PixelTuner_2_showed()
    {
        var drawing = Read(CanvasSchemas.Read("human"));
        Assert.Equal(12, drawing.Shapes.Count);
        Assert.Equal("path3336", drawing.Shapes[0].Id);
        Assert.All(drawing.Shapes, s => Assert.False(string.IsNullOrWhiteSpace(s.PathData)));
        Assert.Equal(drawing.Shapes.Count, drawing.Shapes.Select(s => s.Id).Distinct().Count());
        // The page is the view box; the layer moves everything up by its translate.
        Assert.Equal((0, 0), (drawing.X, drawing.Y));
        Near(744.09449, drawing.Width);
        Near(779.52756, drawing.Height);
        Near(-272.83465, drawing.Shapes[0].Transform.F);
    }

    [Fact]
    public void The_simple_schema_has_no_view_box_and_takes_the_page_from_the_width_and_height()
    {
        var drawing = Read(CanvasSchemas.Read("human_1"));
        Assert.Equal(11, drawing.Shapes.Count);
        Near(343.93427, drawing.Width);
        Near(488.89819, drawing.Height);
    }

    [Fact]
    public void Every_built_in_schema_can_be_read()
    {
        foreach (var (name, title) in CanvasSchemas.All)
        {
            Assert.NotEmpty(title);
            Assert.NotEmpty(Read(CanvasSchemas.Read(name)).Shapes);
        }
        Assert.Throws<ArgumentException>(() => CanvasSchemas.Read("nothing"));
    }

    [Fact]
    public void Something_that_is_not_an_svg_is_refused()
    {
        Assert.Null(CanvasDrawing.TryParse("not xml at all"));
        Assert.Null(CanvasDrawing.TryParse("<html><body/></html>"));
        Assert.Empty(Read("<svg xmlns='http://www.w3.org/2000/svg'/>").Shapes);
    }

    [Fact]
    public void Basic_shapes_become_path_data()
    {
        var drawing = Read("""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 50">
              <rect id="r" x="1" y="2" width="10" height="20" />
              <rect id="rr" x="0" y="0" width="10" height="10" rx="2" />
              <circle id="c" cx="5" cy="5" r="3" />
              <ellipse id="e" cx="5" cy="5" rx="4" ry="2" />
              <line id="l" x1="0" y1="0" x2="9" y2="9" />
              <polyline id="pl" points="0,0 5,5 10,0" />
              <polygon id="pg" points="0 0 5 5 10 0" />
              <path id="p" d=" M0,0 L1,1 " />
            </svg>
            """);
        var data = drawing.Shapes.ToDictionary(s => s.Id, s => s.PathData);
        Assert.Equal("M1,2H11V22H1Z", data["r"]);
        Assert.StartsWith("M2,0H8A2,2 0 0 1 10,2V8", data["rr"]);
        Assert.Equal("M2,5A3,3 0 1 0 8,5A3,3 0 1 0 2,5Z", data["c"]);
        Assert.Equal("M1,5A4,2 0 1 0 9,5A4,2 0 1 0 1,5Z", data["e"]);
        Assert.Equal("M0,0L9,9", data["l"]);
        Assert.Equal("M0,0L5,5L10,0", data["pl"]);
        Assert.Equal("M0,0L5,5L10,0Z", data["pg"]);
        Assert.Equal("M0,0 L1,1", data["p"]);
    }

    [Fact]
    public void Shapes_without_size_or_data_are_left_out_and_a_missing_id_gets_a_name_from_the_shape_number()
    {
        var drawing = Read("""
            <svg xmlns="http://www.w3.org/2000/svg">
              <rect width="0" height="5" />
              <path d="" id="empty" />
              <circle r="0" />
              <rect width="2" height="2" />
              <rect id="named" width="2" height="2" />
              <rect width="3" height="3" />
            </svg>
            """);
        Assert.Equal(["shape1", "named", "shape3"], drawing.Shapes.Select(s => s.Id));
    }

    [Fact]
    public void A_second_shape_with_the_same_id_is_ignored_as_PixelTuner_2_kept_one_element_per_id()
    {
        var drawing = Read("""<svg xmlns="http://www.w3.org/2000/svg"><rect id="a" width="1" height="1"/><rect id="a" width="2" height="2"/></svg>""");
        var shape = Assert.Single(drawing.Shapes);
        Assert.Equal("M0,0H1V1H0Z", shape.PathData);
    }

    [Fact]
    public void Transforms_of_the_groups_and_of_the_shape_add_up_outermost_first()
    {
        var drawing = Read("""
            <svg xmlns="http://www.w3.org/2000/svg">
              <g transform="translate(10,20)">
                <g transform="scale(2)">
                  <rect id="a" width="1" height="1" transform="translate(1 1)"/>
                </g>
              </g>
            </svg>
            """);
        var t = drawing.Shapes[0].Transform;
        // The point (0, 0) of the rectangle: translate 1,1 first, then scale 2, then translate 10,20.
        var (x, y) = t.Apply(0, 0);
        Near(12, x);
        Near(22, y);
    }

    [Theory]
    [InlineData("translate(5)", 0, 0, 5, 0)]
    [InlineData("translate(5, 7)", 1, 1, 6, 8)]
    [InlineData("scale(3)", 1, 2, 3, 6)]
    [InlineData("scale(2 3)", 1, 2, 2, 6)]
    [InlineData("matrix(1 0 0 1 4 5)", 0, 0, 4, 5)]
    [InlineData("rotate(90)", 1, 0, 0, 1)]
    [InlineData("rotate(90 1 1)", 2, 1, 1, 2)]
    [InlineData("skewX(45)", 0, 1, 1, 1)]
    [InlineData("skewY(45)", 1, 0, 1, 1)]
    [InlineData("translate(10,0) scale(2)", 1, 0, 12, 0)]
    [InlineData("scale(2) translate(10,0)", 1, 0, 22, 0)]
    [InlineData("  ", 3, 4, 3, 4)]
    [InlineData("nonsense(1)", 3, 4, 3, 4)]
    public void A_transform_text_gives_its_matrix(string text, double x, double y, double expectedX, double expectedY)
    {
        var (ax, ay) = CanvasDrawing.Parse(text).Apply(x, y);
        Near(expectedX, ax);
        Near(expectedY, ay);
    }

    [Fact]
    public void Hidden_shapes_and_the_parts_of_the_file_that_draw_nothing_are_skipped()
    {
        var drawing = Read("""
            <svg xmlns="http://www.w3.org/2000/svg">
              <defs><rect id="inDefs" width="1" height="1"/></defs>
              <clipPath id="clip"><rect id="inClip" width="1" height="1"/></clipPath>
              <g display="none"><rect id="hiddenGroup" width="1" height="1"/></g>
              <rect id="hidden" width="1" height="1" style="display:none"/>
              <rect id="invisible" width="1" height="1" visibility="hidden"/>
              <rect id="shown" width="1" height="1"/>
            </svg>
            """);
        Assert.Equal(["shown"], drawing.Shapes.Select(s => s.Id));
    }

    [Fact]
    public void The_stroke_comes_from_the_style_or_the_attribute_and_none_means_no_stroke()
    {
        var drawing = Read("""
            <svg xmlns="http://www.w3.org/2000/svg">
              <rect id="a" width="1" height="1" style="fill:#000;stroke:#ff0000;stroke-width:2"/>
              <rect id="b" width="1" height="1" stroke="blue"/>
              <rect id="c" width="1" height="1" style="stroke:none"/>
              <rect id="d" width="1" height="1"/>
            </svg>
            """);
        Assert.Equal(["#ff0000", "blue", null, null], drawing.Shapes.Select(s => s.Stroke));
    }

    [Theory]
    [InlineData("210mm", 793.7007874)]
    [InlineData("1in", 96)]
    [InlineData("72pt", 96)]
    [InlineData("10", 10)]
    [InlineData("10px", 10)]
    [InlineData("2.54cm", 96)]
    [InlineData("1pc", 16)]
    public void Lengths_come_in_drawing_units(string text, double expected)
    {
        Near(expected, CanvasDrawing.Length(text)!.Value);
    }

    [Fact]
    public void A_text_that_is_not_a_length_is_none()
    {
        Assert.Null(CanvasDrawing.Length("wide"));
        Assert.Null(CanvasDrawing.Length("5em"));
        Assert.Null(CanvasDrawing.Length(null));
    }

    [Fact]
    public void Find_returns_the_shape_of_an_id()
    {
        var drawing = Read(CanvasSchemas.Read("human"));
        Assert.Equal("rect3342", drawing.Find("rect3342")!.Id);
        Assert.Null(drawing.Find("nothing"));
    }
}
