using System.Text.Json.Serialization;
using PixelTuner.Core.Model;

namespace PixelTuner.Package;

/// <summary>Source-generated JSON: no reflection, works with iOS AOT and trimming.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Manifest))]
[JsonSerializable(typeof(ShowProject))]
internal sealed partial class PackageJsonContext : JsonSerializerContext;
