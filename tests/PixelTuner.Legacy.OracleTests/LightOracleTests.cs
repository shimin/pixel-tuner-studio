using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Communication.Binarize;
using PixelTuner.Core.Export;
using PixelTuner.Core.Legacy;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace PixelTuner.Legacy.OracleTests;

/// <summary>
/// LightMaster .bin: Studio's port against PixelTuner 2's Communication/Binarize on random shows.
/// The glue that turned a project into LightProjectFile (FileViewModel.GetLightBinaryPresentation)
/// is repeated here as it was.
/// </summary>
public class LightOracleTests
{
    private static readonly DateTime Zero = DateTime.MinValue.AddYears(1);
    private static readonly string[] Effects = ["Linear Transition", "Sine Wave", "Square Wave", "Saw-tooth Wave"];

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 5)]
    [InlineData(3, 10)]
    [InlineData(4, 7)]
    [InlineData(5, 3)]
    [InlineData(6, 25)]
    [InlineData(7, 5)]
    [InlineData(8, 1)]
    public void Random_show_matches_PixelTuner_2(int seed, int rate)
    {
        var random = new Random(seed);
        var lines = Enumerable.Range(0, random.Next(1, 7)).Select(_ => RandomLine(random)).ToList();

        var document = PtpDocument.Load(new MemoryStream(Encoding.UTF8.GetBytes(ToPtp(lines))));
        byte[] actual = PtpLightExport.Export(PtpLightExport.LightTimelines(document), rate);

        byte[] expected = Legacy(lines, rate);
        Assert.Equal(expected, actual);
        BinReader.ReadLght(actual);
    }

    [Fact]
    public void Empty_project_matches_PixelTuner_2() =>
        Assert.Equal(Legacy([], 5), PtpLightExport.Export([], 5));

    private sealed record LineSpec(bool Monochrome, List<ItemSpec> Items);

    private sealed record ItemSpec(string Effect, int Start, int End, (byte R, byte G, byte B) First,
        (byte R, byte G, byte B) Last, int T1, int T2);

    private static LineSpec RandomLine(Random random)
    {
        var items = new List<ItemSpec>();
        int time = random.Next(3) == 0 ? 0 : random.Next(0, 3000);
        int count = random.Next(1, 8);
        for (int i = 0; i < count; i++)
        {
            int length = random.Next(1, 4000);
            string effect = random.Next(3) == 0 ? "Solid" : Effects[random.Next(Effects.Length)];
            int t1 = random.Next(4) == 0 ? 0 : random.Next(1, 2000);
            int t2 = random.Next(4) == 0 ? 0 : random.Next(1, 2000);
            var first = RandomColor(random);
            var last = RandomColor(random);
            items.Add(new ItemSpec(effect, time, time + length, first, last, t1, t2));
            time += length;
            if (random.Next(3) == 0) time += random.Next(1, 1500);
        }
        return new LineSpec(random.Next(2) == 0, items);
    }

    private static (byte, byte, byte) RandomColor(Random random) =>
        ((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));

    private static string Time(int ms) => TimeSpan.FromMilliseconds(ms).ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);

    private static string Hex((byte R, byte G, byte B) c) => $"#FF{c.R:X2}{c.G:X2}{c.B:X2}";

    private static string ToPtp(List<LineSpec> lines)
    {
        var xml = new StringBuilder();
        xml.AppendLine("""<?xml version="1.0"?>""");
        xml.AppendLine("""<FileViewModel xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">""");
        foreach (var line in lines)
        {
            xml.AppendLine($"""  <Line xsi:type="LightTimeLineViewModel" IsMonochrome="{(line.Monochrome ? "true" : "false")}">""");
            foreach (var item in line.Items)
            {
                if (item.Effect == "Solid")
                    xml.AppendLine($"""    <Item xsi:type="TimeLineItemSolid" Start="{Time(item.Start)}" End="{Time(item.End)}" FullName="pack://application:,,,/Effects/04_solid.png" Color="{Hex(item.First)}" />""");
                else
                    xml.AppendLine($"""    <Item xsi:type="TimeLineItemEffect" Start="{Time(item.Start)}" End="{Time(item.End)}" EffectID="{item.Effect}"><Data ColorFirst="{Hex(item.First)}" ColorLast="{Hex(item.Last)}" T1="{item.T1}" T2="{item.T2}" /></Item>""");
            }
            xml.AppendLine("  </Line>");
        }
        xml.AppendLine("</FileViewModel>");
        return xml.ToString();
    }

    /// <summary>FileViewModel.GetLightBinaryPresentation with its FillGabs and CreateBlankLightItem.</summary>
    private static byte[] Legacy(List<LineSpec> lines, int rate)
    {
        var project = new LightProjectFile();
        byte slaveAddress = 1;
        foreach (var line in lines)
        {
            var binaryLine = new LightTimelineFile { IsMonochrome = line.Monochrome, SubmoduleAddress = slaveAddress };
            var items = new List<ItemSpec>();
            if (line.Items[0].Start != 0) items.Add(Blank(0, line.Items[0].Start));
            for (int i = 0; i < line.Items.Count; i++)
            {
                items.Add(line.Items[i]);
                if (i + 1 < line.Items.Count && line.Items[i].End != line.Items[i + 1].Start)
                    items.Add(Blank(line.Items[i].End, line.Items[i + 1].Start));
            }
            foreach (var item in items)
            {
                var start = Zero.AddMilliseconds(item.Start);
                var end = Zero.AddMilliseconds(item.End);
                if (item.Effect == "Solid")
                    binaryLine.Images.Add(new LightColorFile("Solid Color", start, end) { Color = Color(item.First) });
                else
                    binaryLine.Images.Add(new LightEffectFile(item.Effect, start, end)
                    {
                        ColorFirst = Color(item.First), ColorLast = Color(item.Last), T1 = item.T1, T2 = item.T2,
                    });
            }
            project.TimeLines.Add(binaryLine);
            slaveAddress++;
        }
        return [.. project.GetBinary(rate)];
    }

    private static ItemSpec Blank(int start, int end) => new("Solid", start, end, (0, 0, 0), (0, 0, 0), 0, 0);

    private static DrawingColor Color((byte R, byte G, byte B) c) => DrawingColor.FromArgb(255, c.R, c.G, c.B);
}
