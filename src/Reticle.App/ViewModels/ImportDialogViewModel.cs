using CommunityToolkit.Mvvm.ComponentModel;
using Reticle.App.Resources;
using Reticle.Core.Codecs;

namespace Reticle.App.ViewModels;

/// <summary>
/// One text box that accepts a Reticle share code (<c>RTCL1.</c>, SH-1) or a Valorant crosshair code (<c>0;…</c>, IM-1).
/// The input is checked as it is typed; import is possible once it decodes.
/// </summary>
public sealed partial class ImportDialogViewModel : ObservableObject
{
    public const string SharePrefix = "RTCL1.";
    public const string ValorantPrefix = "0;";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Result), nameof(Error), nameof(Warning), nameof(CanImport), nameof(FormatLabel))]
    private string _text = "";

    /// <summary>Outcome for the current text; null while the box is empty.</summary>
    public ImportResult? Result { get; private set; }

    partial void OnTextChanged(string value) => Result = string.IsNullOrWhiteSpace(value) ? null : Detect(value);

    public string? Error => Result?.Error;
    public string? Warning => Result?.Warning;
    public bool CanImport => Result?.Doc is not null;

    public string? FormatLabel => string.IsNullOrWhiteSpace(Text) ? null : Text.Trim() switch
    {
        var t when t.StartsWith(SharePrefix, StringComparison.Ordinal) => Strings.FormatShareCode,
        var t when t.StartsWith(ValorantPrefix, StringComparison.Ordinal) => Strings.FormatValorant,
        _ => null,
    };

    /// <summary>Routes by prefix: <c>RTCL1.</c> → <see cref="ShareCode.Decode"/>, <c>0;</c> → <see cref="ValorantCode.Parse"/>, else an error.</summary>
    public static ImportResult Detect(string text)
    {
        var t = (text ?? "").Trim();
        if (t.StartsWith(SharePrefix, StringComparison.Ordinal)) return ShareCode.Decode(t);
        if (t.StartsWith(ValorantPrefix, StringComparison.Ordinal)) return ValorantCode.Parse(t);
        return new ImportResult(null, Strings.ErrorUnknownCodeFormat);
    }
}
