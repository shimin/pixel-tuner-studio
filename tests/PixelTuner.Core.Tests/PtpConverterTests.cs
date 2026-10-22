using System.Text;
using PixelTuner.Core.Legacy;
using PixelTuner.Core.Model;

namespace PixelTuner.Core.Tests;

/// <summary>Every field PixelTuner 2 stored that the model keeps lands where it should.</summary>
public class PtpConverterTests
{
    private const string Project = """
        <?xml version="1.0"?>
        <FileViewModel xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" UnitSize="7.5" AudioPath="./music/track.mp3">
          <Canvas>
            <Elements>
              <CanvasElementViewModel TilesMargin="2" RowsCount="2" ColumnsCount="3" Path="rect1">
                <LedTiles>
                  <LedTile LedNumber="2" Index="1" Selected="true" />
                  <LedTile LedNumber="1" Index="5" Selected="true" />
                  <LedTile LedNumber="0" Index="3" Selected="false" />
                </LedTiles>
              </CanvasElementViewModel>
            </Elements>
            <Content>&lt;svg id="drawing" /&gt;</Content>
          </Canvas>
          <Line xsi:type="PixelTimeLineViewModel" LineTag="Staff" LineColorTagXml="#80102030" IsFlippedVertical="true">
            <Item Start="00:00:01.250" End="00:01:02.500" FullName="C:\shows\a.bmp" Stretching="3" Brightness="40" LaserOn="true" />
          </Line>
          <Line xsi:type="VideoTimeLineViewModel" LineTag="Suit" MetaDataPath="rect1">
            <Item xsi:type="TimeLineItemVideo" Start="00:00:00.000" End="00:00:02.000" FullName="./clip.gif" XOffset="-4" YOffset="7" />
          </Line>
          <Line xsi:type="VideoTimeLineViewModel" MetaDataPath="nowhere" />
          <Line xsi:type="LightTimeLineViewModel" IsMonochrome="true">
            <Item xsi:type="TimeLineItemSolid" Start="00:00:00.000" End="00:00:00.500" FullName="pack://application:,,,/Effects/04_solid.png" Color="#FF20A040" />
            <Item xsi:type="TimeLineItemEffect" Start="00:00:00.500" End="00:00:01.000" EffectID="Saw-tooth Wave"><Data ColorFirst="#F00" ColorLast="#FF0000FF" T1="90" T2="15" /></Item>
            <Item xsi:type="TimeLineItemEffect" Start="00:00:01.000" End="00:00:02.000" EffectID="Strobe"><Data ColorFirst="#FFFFFFFF" ColorLast="#FF000000" /></Item>
          </Line>
          <Line xsi:type="EpicPoiTimeLineViewModel">
            <Item xsi:type="TimeLineItemGradient" Start="00:00:00.000" End="00:00:01.000" FullName="./missing.png" />
          </Line>
        </FileViewModel>
        """;

    [Fact]
    public void Converts_every_stored_field()
    {
        var document = PtpDocument.Load(new MemoryStream(Encoding.UTF8.GetBytes(Project)));
        var asked = new List<string>();
        var converter = new PtpConverter(document, path =>
        {
            asked.Add(path);
            return path.Contains("missing", StringComparison.Ordinal) ? null : "id:" + path;
        });
        var project = converter.ToProject("Show", (bytes, name) => $"content:{name}:{Encoding.UTF8.GetString(bytes)}");

        Assert.Equal("Show", project.Name);
        Assert.Equal("./music/track.mp3", project.AudioSourcePath);
        Assert.Equal("id:./music/track.mp3", project.AudioResourceId);
        Assert.Equal("""content:canvas.svg:<svg id="drawing" />""", project.CanvasResourceId);
        Assert.Equal(["./music/track.mp3", @"C:\shows\a.bmp", "./clip.gif", "./missing.png"], asked);
        Assert.Equal(["./missing.png"], converter.MissingMedia);

        var layout = Assert.Single(project.LedLayouts);
        Assert.Equal(("rect1", 3, 2), (layout.Name, layout.Columns, layout.Rows));
        Assert.Equal([5, 1], layout.Leds);   // selected tiles by LED number

        var pixel = project.Timelines[0];
        Assert.Equal((TimelineKind.Pixel, "Staff", "#80102030", true), (pixel.Kind, pixel.Name, pixel.Color, pixel.FlipVertical));
        var image = Assert.Single(pixel.Items);
        Assert.Equal(ItemKind.Image, image.Kind);
        Assert.Equal(new TimeSpan(0, 0, 0, 1, 250), image.Start);
        Assert.Equal(new TimeSpan(0, 0, 1, 2, 500), image.End);
        Assert.Equal((@"C:\shows\a.bmp", "id:C:\\shows\\a.bmp"), (image.SourcePath, image.ResourceId));
        Assert.Equal((3, (byte)40, true), (image.Stretching, image.Brightness, image.LaserOn));

        var video = project.Timelines[1];
        Assert.Equal((TimelineKind.Video, "Suit", layout.Id, "#FF0000FF"), (video.Kind, video.Name, video.LedLayoutId, video.Color));
        var clip = Assert.Single(video.Items);
        Assert.Equal((ItemKind.Video, -4, 7, 1, (byte)100), (clip.Kind, clip.OffsetX, clip.OffsetY, clip.Stretching, clip.Brightness));
        Assert.Null(project.Timelines[2].LedLayoutId);

        var light = project.Timelines[3];
        Assert.Equal((TimelineKind.Light, true), (light.Kind, light.Monochrome));
        Assert.Equal((ItemKind.Solid, "#FF20A040", (string?)null, (string?)null), (light.Items[0].Kind, light.Items[0].Color, light.Items[0].ResourceId, light.Items[0].SourcePath));
        var saw = light.Items[1];
        Assert.Equal((ItemKind.Effect, LightEffectKind.SawToothWave, "#F00", "#FF0000FF", 90, 15), (saw.Kind, saw.Effect, saw.Color, saw.ColorLast, saw.T1, saw.T2));
        Assert.Equal("Strobe", light.Items[2].Effect);   // unknown effects stay as they are; the export refuses them

        var epic = Assert.Single(project.Timelines[4].Items);
        Assert.Equal((TimelineKind.EpicPoi, ItemKind.Gradient), (project.Timelines[4].Kind, epic.Kind));
        Assert.Equal(("./missing.png", (string?)null), (epic.SourcePath, epic.ResourceId));
    }

    private static LedLayout LayoutOf(string tiles, string margin = "")
    {
        string xml = $"""
            <?xml version="1.0"?>
            <FileViewModel>
              <Canvas>
                <Elements>
                  <CanvasElementViewModel {margin} RowsCount="2" ColumnsCount="3" Path="rect1">
                    <LedTiles>{tiles}</LedTiles>
                  </CanvasElementViewModel>
                </Elements>
                <Content />
              </Canvas>
            </FileViewModel>
            """;
        var converter = new PtpConverter(PtpDocument.Load(new MemoryStream(Encoding.UTF8.GetBytes(xml))), _ => null);
        return Assert.Single(converter.LedLayouts);
    }

    [Fact]
    public void The_tile_margin_comes_across_and_is_2_when_the_file_does_not_say()
    {
        Assert.Equal(7, LayoutOf("", "TilesMargin=\"7\"").TilesMargin);
        Assert.Equal(2, LayoutOf("").TilesMargin);
    }

    [Fact]
    public void Numbers_without_gaps_or_repeats_have_nothing_to_report()
    {
        var layout = LayoutOf("""<LedTile LedNumber="2" Index="1" Selected="true" /><LedTile LedNumber="1" Index="5" Selected="true" />""");
        Assert.Null(layout.SourceIssues);
        Assert.Equal([5, 1], layout.Leds);
    }

    [Fact]
    public void Gaps_and_repeats_of_the_stored_numbers_are_kept_for_the_editor_to_report_while_the_chain_stays_what_the_export_used()
    {
        // Numbers 1, 3, 3, 6: 2, 4 and 5 are missing, 3 is used twice.
        var layout = LayoutOf("""
            <LedTile LedNumber="1" Index="0" Selected="true" />
            <LedTile LedNumber="3" Index="2" Selected="true" />
            <LedTile LedNumber="3" Index="3" Selected="true" />
            <LedTile LedNumber="6" Index="5" Selected="true" />
            <LedTile LedNumber="9" Index="4" Selected="false" />
            """);
        Assert.Equal([0, 2, 3, 5], layout.Leds);
        var issues = layout.SourceIssues!;
        Assert.Equal([2, 4, 5], issues.MissingNumbers);
        Assert.Equal([3], issues.DuplicateNumbers);
        Assert.Equal([2, 3], issues.DuplicateCells);
        Assert.Equal([2, 3, 5], issues.CellsAfterGap);   // the cells of 3 follow the gap at 2, the cell of 6 the gap at 5
        Assert.Equal(6, issues.MaxNumber);
    }
}
