using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace FalloutRemake.Runtime;

public sealed record SceneryReplacement([property: JsonRequired] Guid Id, [property: JsonRequired] Guid CauseUseId,
    [property: JsonRequired] string ReplacesKey, [property: JsonRequired] ActorLocation Location,
    [property: JsonRequired] int Pid, [property: JsonRequired] int Fid, [property: JsonRequired] string PrototypeHash);

/// <summary>Validated runtime overlay, separate from immutable imported MAP objects.</summary>
public sealed class ScenerySession
{
    private Dictionary<string, SceneryReplacement> _replacements = new(StringComparer.Ordinal);
    public long Revision { get; private set; }
    public bool Replaces(string key) => _replacements.ContainsKey(key);
    public SceneryReplacement[] Capture() => _replacements.Values.OrderBy(r => r.Id).ToArray();
    internal static Dictionary<string, SceneryReplacement> Prepare(SceneryReplacement[] replacements) => replacements.ToDictionary(r => r.ReplacesKey, StringComparer.Ordinal);
    internal void ApplyPrepared(Dictionary<string, SceneryReplacement> replacements) { _replacements = replacements; Revision++; }
    internal void Reset() { _replacements.Clear(); Revision++; }
}
