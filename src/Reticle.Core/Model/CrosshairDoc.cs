namespace Reticle.Core.Model;

/// <param name="ReferenceHeight">null = sizes are raw device pixels; 1080 = scale by monitorHeight / 1080.</param>
public sealed record CrosshairDoc(
    int Schema,
    string Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int? ReferenceHeight,
    IReadOnlyList<Layer> Layers)
{
    public const int CurrentSchema = 1;
}
