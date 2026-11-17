using Reticle.Core.Geometry;
using Reticle.Core.Model;
using Reticle.Core.Serialization;
using Reticle.Core.Storage;

namespace Reticle.Core.Tests;

public sealed class StorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "reticle-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch (IOException) { }
    }

    private static CrosshairDoc Doc(string id, string name = "N", params Layer[] layers) => new(
        1, id, name, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(1), 1080,
        layers.Length > 0 ? layers : new Layer[] { new ClassicLayer { Id = "l" } });

    private string CrosshairDir => Path.Combine(_root, "crosshairs");

    [Fact]
    public void SaveLoad_RoundTrip()
    {
        var store = new LibraryStore(_root);
        var doc = Doc("abc", "Mine", new ClassicLayer { Id = "l", Gap = 0 }, new ShapeLayer { Id = "s" });
        store.Save(doc);
        var loaded = Assert.Single(store.LoadAll());
        Assert.Equal(Json.Serialize(doc), Json.Serialize(loaded));
        Assert.Equal(doc.UpdatedAt, loaded.UpdatedAt);
        Assert.Empty(Directory.GetFiles(CrosshairDir, "*.tmp"));
    }

    [Fact]
    public void Save_Overwrites()
    {
        var store = new LibraryStore(_root);
        store.Save(Doc("a", "One"));
        store.Save(Doc("a", "Two"));
        Assert.Equal("Two", Assert.Single(store.LoadAll()).Name);
    }

    [Fact]
    public void LoadAll_MissingDirectory_Empty()
    {
        Assert.Empty(new LibraryStore(_root).LoadAll());
    }

    [Fact]
    public void LoadAll_OrdersByNameIgnoreCaseThenId()
    {
        var store = new LibraryStore(_root);
        store.Save(Doc("3", "beta"));
        store.Save(Doc("2", "Alpha"));
        store.Save(Doc("1", "alpha"));
        Assert.Equal(new[] { "1", "2", "3" }, store.LoadAll().Select(d => d.Id));
    }

    [Fact]
    public void Load_SkipsCorruptFile()
    {
        var store = new LibraryStore(_root);
        store.Save(Doc("good"));
        var bad = Path.Combine(CrosshairDir, "bad.json");
        File.WriteAllText(bad, "{garbage");
        var errors = new List<string>();
        var docs = store.LoadAll(errors.Add);
        Assert.Equal("good", Assert.Single(docs).Id);
        Assert.Contains("bad.json", Assert.Single(errors));
        Assert.True(File.Exists(bad)); // library files are never renamed
    }

    [Theory]
    [InlineData("""{"schema":1,"id":"x","name":"x"}""")]
    [InlineData("""{"schema":1,"id":"x","name":"x","layers":null}""")]
    [InlineData("""{"schema":1,"id":"x","name":"x","layers":[null]}""")]
    [InlineData("""{"schema":2,"id":"x","name":"x","layers":[]}""")]
    [InlineData("")]
    public void Load_SkipsInvalidDocuments(string json)
    {
        var store = new LibraryStore(_root);
        Directory.CreateDirectory(CrosshairDir);
        File.WriteAllText(Path.Combine(CrosshairDir, "x.json"), json);
        var errors = new List<string>();
        Assert.Empty(store.LoadAll(errors.Add));
        Assert.Single(errors);
    }

    [Fact]
    public void Delete_RemovesFile_AndMissingIdIsNoOp()
    {
        var store = new LibraryStore(_root);
        store.Save(Doc("a"));
        store.Delete("a");
        Assert.Empty(store.LoadAll());
        store.Delete("a");
        store.Delete("never-existed");
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("../evil")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a:b")]
    [InlineData("a*b")]
    public void InvalidIds_Rejected(string id)
    {
        var store = new LibraryStore(_root);
        Assert.Throws<ArgumentException>(() => store.Save(Doc(id)));
        Assert.Throws<ArgumentException>(() => store.Delete(id));
    }

    [Fact]
    public void Settings_Missing_ReturnsDefault()
    {
        var s = new SettingsStore(_root).Load();
        Assert.Equal(Json.Serialize(Settings.Default), Json.Serialize(s));
    }

    [Fact]
    public void Settings_SaveLoad_RoundTrip()
    {
        var store = new SettingsStore(_root);
        var s = Settings.Default with { ActiveCrosshairId = "abc", GlobalScale = 1.5, Language = "de" };
        store.Save(s);
        Assert.Equal(Json.Serialize(s), Json.Serialize(store.Load()));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Theory]
    [InlineData("{garbage")]
    [InlineData("""{"schema":7}""")]
    [InlineData("""{"schema":1}""")]
    public void Settings_Corrupt_ReturnsDefaultAndBacksUp(string content)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "settings.json"), content);
        var s = new SettingsStore(_root).Load();
        Assert.Equal(Json.Serialize(Settings.Default), Json.Serialize(s));
        Assert.False(File.Exists(Path.Combine(_root, "settings.json")));
        var backup = Assert.Single(Directory.GetFiles(_root, "settings.corrupt-*.json"));
        Assert.Matches(@"settings\.corrupt-\d{14}\.json$", backup);
        Assert.Equal(content, File.ReadAllText(backup));
    }

    [Fact]
    public void Presets_AtLeast15_AllUniqueIds_AllBuildGeometry()
    {
        var all = Presets.All;
        Assert.True(all.Count >= 15);
        Assert.Equal(all.Count, all.Select(d => d.Id).Distinct().Count());
        Assert.Equal(all.Count, all.Select(d => d.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var doc in all)
        {
            Assert.Equal(1, doc.Schema);
            Assert.False(string.IsNullOrWhiteSpace(doc.Name));
            Assert.NotEmpty(doc.Layers);
            foreach (var layer in doc.Layers)
            {
                LayerGeometry.Build(layer, 1);
                LayerGeometry.Build(layer, 2.5);
            }
        }
        Assert.True(all.SelectMany(d => d.Layers).Select(l => l.Color).Distinct().Count() >= 6, "colors should vary");
    }

    [Fact]
    public void Presets_IncludeFiringAnimation()
    {
        var anim = Presets.All.SelectMany(d => d.Layers).Select(l => l.Animation).FirstOrDefault(a => a is not null);
        Assert.NotNull(anim);
        Assert.Equal(new InputTrigger(InputKind.Mouse, 1), anim!.Trigger);
        Assert.Equal(AnimationMode.Once, anim.Mode);
    }

    [Fact]
    public void Presets_TwoTierLookalike_HasExpectedValues()
    {
        var doc = Presets.All.Single(d => d.Id == "preset-two-tier");
        Assert.Equal(1080, doc.ReferenceHeight);
        var inner = Assert.IsType<ClassicLayer>(doc.Layers[0]);
        var outer = Assert.IsType<ClassicLayer>(doc.Layers[1]);
        Assert.Equal((6, 2, 3, 0.8), (inner.Length, inner.Thickness, inner.Gap, inner.Opacity));
        Assert.Equal((2, 2, 10, 0.35), (outer.Length, outer.Thickness, outer.Gap, outer.Opacity));
        Assert.All(doc.Layers, l =>
        {
            Assert.Equal(new Rgba(255, 255, 255, 255), l.Color);
            Assert.True(l.Outline.Enabled);
            Assert.Equal(1, l.Outline.Width);
            Assert.Equal(0.5, l.Outline.Opacity);
            Assert.Equal(new Rgba(0, 0, 0, 255), l.Outline.Color);
        });
    }

    [Fact]
    public void SeedIfEmpty_SeedsOnce_AndNotWhenLibraryHasDocs()
    {
        var store = new LibraryStore(_root);
        Presets.SeedIfEmpty(store);
        Assert.Equal(Presets.All.Count, store.LoadAll().Count);

        store.Delete(Presets.All[0].Id);
        Presets.SeedIfEmpty(store); // non-empty: nothing re-added
        Assert.Equal(Presets.All.Count - 1, store.LoadAll().Count);

        var other = new LibraryStore(Path.Combine(_root, "other"));
        other.Save(Doc("mine"));
        Presets.SeedIfEmpty(other);
        Assert.Equal("mine", Assert.Single(other.LoadAll()).Id);
    }
}
