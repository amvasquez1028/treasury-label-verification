using System.Text.Json;
using System.Text.Json.Serialization;
using LabelVerification.Core.Models;
using LabelVerification.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LabelVerification.Core.Cola;

public interface IColaPublicCache
{
    bool TryGetEntry(string ttbId, out ColaCacheEntry entry);

    IReadOnlyList<string> ListIds();
}

public sealed record ColaCacheEntry(
    ExpectedLabelFields Expected,
    string FancifulName,
    ExpectedLabelFields? OdpExpected = null);

public sealed record ColaExpectedFieldsResponse
{
    public string? FancifulName { get; init; }
    public required string BrandName { get; init; }
    public required string ClassTypeDesignation { get; init; }
    public required decimal AbvPercent { get; init; }
    public required string NetContents { get; init; }
    public required string BottlerProducerAddress { get; init; }
    public string? CountryOfOrigin { get; init; }
    public required string ProductCategory { get; init; }
    public required string TtbWarningText { get; init; }
    public required string BoldWarningPhrase { get; init; }

    public static ColaExpectedFieldsResponse FromEntry(ColaCacheEntry entry) =>
        new()
        {
            FancifulName = string.IsNullOrWhiteSpace(entry.FancifulName) ? null : entry.FancifulName,
            BrandName = entry.Expected.BrandName,
            ClassTypeDesignation = entry.Expected.ClassTypeDesignation,
            AbvPercent = entry.Expected.AbvPercent,
            NetContents = entry.Expected.NetContents,
            BottlerProducerAddress = entry.Expected.BottlerProducerAddress,
            CountryOfOrigin = entry.Expected.CountryOfOrigin,
            ProductCategory = entry.Expected.ProductCategory,
            TtbWarningText = entry.Expected.TtbWarningText,
            BoldWarningPhrase = entry.Expected.BoldWarningPhrase,
        };
}

internal sealed class ColaPublicCache : IColaPublicCache
{
    private readonly Dictionary<string, ColaCacheEntry> _byId;
    private readonly ILogger<ColaPublicCache> _logger;

    public ColaPublicCache(IOptions<ColaOptions> options, ILogger<ColaPublicCache> logger)
    {
        _logger = logger;
        _byId = new Dictionary<string, ColaCacheEntry>(StringComparer.Ordinal);
        var dir = ResolveColasDir(options.Value.ColasDir);
        if (string.IsNullOrEmpty(dir))
        {
            logger.LogWarning("COLA public cache directory not found — TTB ID lookup disabled");
            return;
        }

        try
        {
            LoadFromDirectory(dir);
            logger.LogInformation("COLA public cache loaded {Count} entries from {Dir}", _byId.Count, dir);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load COLA public cache from {Dir}", dir);
        }
    }

    public bool TryGetEntry(string ttbId, out ColaCacheEntry entry)
    {
        var normalized = NormalizeTtbId(ttbId);
        if (normalized.Length == 0)
        {
            entry = default!;
            return false;
        }

        if (_byId.TryGetValue(normalized, out entry!))
        {
            return true;
        }

        var repaired = TryRepairOcrTtbId(normalized);
        if (repaired is not null && _byId.TryGetValue(repaired, out entry!))
        {
            _logger.LogDebug("COLA cache matched repaired TTB ID {Repaired} from OCR input {Raw}", repaired, normalized);
            return true;
        }

        entry = default!;
        return false;
    }

    private string? TryRepairOcrTtbId(string digits)
    {
        if (digits.Length == 13)
        {
            for (var i = 0; i <= digits.Length; i++)
            {
                var candidate = digits.Insert(i, "0");
                if (_byId.ContainsKey(candidate))
                {
                    return candidate;
                }
            }
        }

        foreach (var cachedId in _byId.Keys)
        {
            if (cachedId.Length == digits.Length && CountDigitDifferences(cachedId, digits) == 1)
            {
                return cachedId;
            }
        }

        return null;
    }

    private static int CountDigitDifferences(string left, string right)
    {
        if (left.Length != right.Length)
        {
            return int.MaxValue;
        }

        var diff = 0;
        for (var i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
            {
                diff++;
            }
        }

        return diff;
    }

    public IReadOnlyList<string> ListIds()
    {
        var ids = _byId.Keys.OrderBy(id => id, StringComparer.Ordinal).ToList();
        return ids;
    }

    private void LoadFromDirectory(string dir)
    {
        foreach (var metaPath in Directory.EnumerateFiles(dir, "*.meta.json"))
        {
            try
            {
                var raw = File.ReadAllText(metaPath);
                var meta = JsonSerializer.Deserialize<ColaMetaFile>(raw, JsonOptions);
                if (meta is null)
                {
                    continue;
                }

                var id = NormalizeTtbId(meta.TtbId);
                if (id.Length == 0)
                {
                    var fileName = Path.GetFileName(metaPath);
                    id = fileName.EndsWith(".meta.json", StringComparison.OrdinalIgnoreCase)
                        ? NormalizeTtbId(fileName[..^".meta.json".Length])
                        : NormalizeTtbId(Path.GetFileNameWithoutExtension(fileName));
                }

                if (id.Length == 0 || meta.ExpectedLabelFields is null)
                {
                    continue;
                }

                _byId[id] = new ColaCacheEntry(
                    MergeFancifulIntoExpected(meta.ExpectedLabelFields, meta.FancifulName),
                    meta.FancifulName ?? "",
                    meta.OdpExpectedLabelFields is null
                        ? null
                        : MergeFancifulIntoExpected(meta.OdpExpectedLabelFields, meta.FancifulName));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping COLA meta file {MetaPath}", metaPath);
            }
        }
    }

    private static ExpectedLabelFields MergeFancifulIntoExpected(ExpectedLabelFields expected, string? fancifulName)
    {
        if (string.IsNullOrWhiteSpace(expected.FancifulName) && !string.IsNullOrWhiteSpace(fancifulName))
        {
            return expected with { FancifulName = fancifulName.Trim() };
        }

        return expected;
    }

    private static string? ResolveColasDir(string configured)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            candidates.Add(configured);
        }

        candidates.Add("testdata/colas");
        candidates.Add(Path.Combine("..", "testdata", "colas"));
        candidates.Add(Path.Combine("..", "..", "testdata", "colas"));
        candidates.Add(Path.Combine("..", "..", "..", "testdata", "colas"));

        var baseDir = AppContext.BaseDirectory;
        for (var depth = 0; depth <= 6; depth += 1)
        {
            var parts = new List<string> { baseDir };
            for (var i = 0; i < depth; i += 1)
            {
                parts.Add("..");
            }

            parts.Add("testdata");
            parts.Add("colas");
            candidates.Add(Path.Combine(parts.ToArray()));
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var full = Path.GetFullPath(candidate);
            if (Directory.Exists(full))
            {
                return full;
            }
        }

        return null;
    }

    private static string NormalizeTtbId(string ttbId)
    {
        if (string.IsNullOrWhiteSpace(ttbId))
        {
            return string.Empty;
        }

        var trimmed = ttbId.Trim();
        var digits = trimmed.Where(char.IsDigit).ToArray();
        return digits.Length > 0 ? new string(digits) : trimmed;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private sealed class ColaMetaFile
    {
        [JsonPropertyName("ttbId")]
        public string? TtbId { get; init; }

        [JsonPropertyName("fancifulName")]
        public string? FancifulName { get; init; }

        [JsonPropertyName("expectedLabelFields")]
        public ExpectedLabelFields? ExpectedLabelFields { get; init; }

        [JsonPropertyName("odpExpectedLabelFields")]
        public ExpectedLabelFields? OdpExpectedLabelFields { get; init; }
    }
}
