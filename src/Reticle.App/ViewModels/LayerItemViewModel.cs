using CommunityToolkit.Mvvm.ComponentModel;
using Reticle.Core.Model;

namespace Reticle.App.ViewModels;

/// <summary>One row of the designer's layer list.</summary>
public sealed partial class LayerItemViewModel : ObservableObject
{
    private readonly DesignerViewModel _owner;
    private Layer _layer;

    public LayerItemViewModel(DesignerViewModel owner, Layer layer)
    {
        _owner = owner;
        _layer = layer;
    }

    public string Id => _layer.Id;
    public string Name => _layer.Name;
    public string TypeLabel => Labels.LayerType(_layer);

    /// <summary>Segoe Fluent Icons glyph for the layer type.</summary>
    public string Glyph => _layer switch
    {
        ClassicLayer => "",
        RadialLayer => "",
        ShapeLayer => "",
        ImageLayer => "",
        _ => "",
    };

    public bool Visible
    {
        get => _layer.Visible;
        set => _owner.SetLayerVisible(_layer.Id, value);
    }

    internal void Update(Layer layer)
    {
        if (ReferenceEquals(layer, _layer)) return;
        _layer = layer;
        OnPropertyChanged(string.Empty);
    }
}
