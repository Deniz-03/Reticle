using Reticle.Core.Model;

namespace Reticle.Core.Codecs;

/// <summary>Outcome of importing a crosshair from text. Exactly one of Doc / Error is set.</summary>
public sealed record ImportResult(CrosshairDoc? Doc, string? Error, string? Warning = null);
