# Reticle Crosshair Overlay – Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Windows crosshair overlay app ("Reticle") with layer-based designer, library, global hotkeys, smart toggle, animations, share codes and Valorant-code import, usable directly with Valorant in Windowed Fullscreen.

**Architecture:** `Reticle.Core` (pure .NET, no WPF) holds the data model, JSON persistence, geometry (layer → pixel-snapped primitives), animation evaluation, hotkey matching and codecs; it is fully unit-tested. `Reticle.App` (WPF) holds the click-through overlay window that draws Core primitives, the low-level input hook service, the editor UI, tray and system integration. The editor and overlay share one in-process `AppState`; every change re-renders the overlay immediately.

**Tech Stack:** C# 14 / .NET 10 (LTS), WPF, CommunityToolkit.Mvvm, System.Text.Json, xUnit, Win32 P/Invoke (user32), WinForms `NotifyIcon` for the tray.

**Spec:** `Requirements.md` (repo root). Executors read both.

## Global Constraints

- Product name **Reticle**; never use "Crosshair X", "CenterPoint" or their assets/texts/colors anywhere (code, UI, presets, docs).
- Target `net10.0` (Core, tests) and `net10.0-windows` (App); Windows 10/11 x64 only.
- Overlay must never inject, read/write game memory, install drivers, send synthetic input, capture the screen, or block/modify input (OV-8, AN-4). Hooks always call `CallNextHookEx` and return immediately.
- No network access, no telemetry (NF-4).
- Static overlay idle CPU < 1 %: render only on state change or while an animation runs (NF-1).
- Data lives in `%APPDATA%\Reticle\` (`settings.json`, `crosshairs\<id>.json`), JSON with `"schema": 1` (NF-5).
- UI languages DE and EN via `.resx`; default follows OS culture, fallback EN (AP-4).
- Coordinates in Core are **device pixels**; origin `(0,0)` is the top-left corner of the screen's center pixel, so a 1 px dot is `Rect(0,0,1,1)`.
- Every commit message ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **DPI scaling ≠ 100 % and odd resolutions** (e.g. 125 %, 2560×1440 at 150 %, 1366×768): crosshair must stay on the exact center pixel, sharp, not blurred → Task 9 test `OverlayPlacement_*` + manual check.
2. **Key auto-repeat and held keys**: holding a key must not toggle repeatedly; "hide while held" must reappear when released even if another key was pressed meanwhile → Task 7 tests `Repeat_IsIgnored`, `HideWhileHeld_ReleasesAfterOtherKey`.
3. **Corrupt or partial data files** (manually edited JSON, unknown schema, missing fields): app must start, skip the bad crosshair, log it, never lose the others → Task 8 test `Load_SkipsCorruptFile`.
4. **Malformed import strings** (whitespace, trailing `;`, unknown keys, other games' codes, garbage): import returns an error message, never throws into the UI → Task 5 & 6 tests `Decode_Garbage_ReturnsError`, `Parse_IgnoresUnknownKeys`.
5. **Game started as admin** (Riot Client): hotkeys silently fail when Reticle isn't elevated → Task 13 shows a banner when a foreground process is elevated and Reticle isn't, with "Als Administrator neu starten".

---

## File Structure

```
Reticle.sln
src/Reticle.Core/
  Model/Rgba.cs, Vec2.cs, CrosshairDoc.cs, Layers.cs, Animation.cs, Settings.cs, KeyBinding.cs
  Serialization/Json.cs                 – JsonSerializerOptions, polymorphic layers
  Geometry/Primitives.cs                – render primitive records
  Geometry/LayerGeometry.cs             – Layer → LayerRender (dispatcher)
  Geometry/ClassicGeometry.cs, RadialGeometry.cs, ShapeGeometry.cs
  Animation/AnimationEvaluator.cs
  Hotkeys/HotkeyMatcher.cs
  Codecs/ShareCode.cs, ValorantCode.cs
  Storage/LibraryStore.cs, SettingsStore.cs, Presets.cs (+ Presets/*.json embedded)
src/Reticle.App/
  App.xaml(.cs), app.manifest (PerMonitorV2, asInvoker)
  AppState.cs                            – single source of truth, change events
  Overlay/OverlayWindow.cs, OverlayRenderer.cs, OverlayPlacement.cs, MonitorService.cs, Win32.cs
  Input/InputHookService.cs
  Views/MainWindow.xaml, LibraryView, DesignerView, PositionView, HotkeysView, SettingsView, ImportDialog
  ViewModels/*.cs
  System/TrayService.cs, SingleInstance.cs, AutostartService.cs, ElevationService.cs
  Resources/Strings.resx, Strings.de.resx, Icon.ico
tests/Reticle.Core.Tests/                – one test file per Core file
tests/Reticle.App.Tests/                 – OverlayPlacement, AppState (no UI automation)
README.md                                – install, Valorant setup guide
```

---

### Task 0: Toolchain, repo and solution scaffold

**Files:** Create `Reticle.sln`, `src/Reticle.Core/Reticle.Core.csproj`, `src/Reticle.App/Reticle.App.csproj`, `tests/Reticle.Core.Tests/*.csproj`, `tests/Reticle.App.Tests/*.csproj`, `.gitignore`, `Directory.Build.props` (`Nullable=enable`, `TreatWarningsAsErrors=true`, `LangVersion=latest`).

**Interfaces:** Produces the solution all later tasks build in.

- [ ] **Step 1:** Install the SDK: `winget install --id Microsoft.DotNet.SDK.10 --silent --accept-package-agreements --accept-source-agreements`. Verify: `dotnet --list-sdks` shows `10.x`.
- [ ] **Step 2:** `git init`, add `.gitignore` (`dotnet new gitignore`), then create projects with `dotnet new classlib`, `dotnet new wpf`, `dotnet new xunit` and wire references (App→Core, Core.Tests→Core, App.Tests→App). App: `<UseWindowsForms>true</UseWindowsForms>`, `<ApplicationManifest>app.manifest</ApplicationManifest>` with `<dpiAwareness>PerMonitorV2</dpiAwareness>`. Add package `CommunityToolkit.Mvvm` to App.
- [ ] **Step 3:** Add a smoke test `CoreAssembly_Loads` asserting `typeof(Rgba).Assembly` is not null (create empty `Rgba` record struct).
- [ ] **Step 4:** Run `dotnet build` and `dotnet test`. Expected: build succeeds with 0 warnings, 1 test passes.
- [ ] **Step 5:** Commit `chore: scaffold Reticle solution`.

---

### Task 1: Data model and JSON serialization

**Files:** `src/Reticle.Core/Model/*`, `Serialization/Json.cs`; Test `tests/Reticle.Core.Tests/SerializationTests.cs`.

**Interfaces – Produces (all `public sealed record` unless noted, mutable via `with`):**
- `readonly record struct Rgba(byte R, byte G, byte B, byte A)` with `static Rgba Parse(string hex)` (`#RRGGBB` or `#RRGGBBAA`), `string ToHex()` (always 8 digits), JSON as hex string.
- `readonly record struct Vec2(double X, double Y)`.
- `Outline(bool Enabled, double Width, Rgba Color, double Opacity)`; default `(false, 1, #000000FF, 1)`.
- `abstract record Layer` : `string Id, string Name, bool Visible, Rgba Color, double Opacity, double Rotation, double Scale, Vec2 Offset, double Blur, Outline Outline, LayerAnimation? Animation`.
- `ClassicLayer : Layer` – `int Length, int Thickness, int Gap, bool Top, bool Bottom, bool Left, bool Right, Dot Dot`; `Dot(bool Enabled, int Size, bool Round)`.
- `RadialLayer : Layer` – `int Arms (2..8), double StartAngle, int Length, int Thickness, int Gap`.
- `ShapeLayer : Layer` – `ShapeKind Kind {Circle, Ring, Rectangle, Triangle, Chevron, Arc, Line}, double Width, double Height, double StrokeWidth, double ArcStart, double ArcSweep`.
- `ImageLayer : Layer` – `string ImageBase64, string MimeType, double Width, double Height` (GIF allowed).
- `CrosshairDoc(int Schema, string Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int? ReferenceHeight, IReadOnlyList<Layer> Layers)`. `ReferenceHeight` = null means sizes are raw device pixels; `1080` means scale by `monitorHeight / 1080` (IM-3).
- `LayerAnimation(InputTrigger Trigger, AnimationMode Mode, IReadOnlyList<Keyframe> Keyframes)`; `InputTrigger(InputKind Kind, int Code)`; `enum InputKind {Keyboard, Mouse}`; mouse codes: 1=Left, 2=Right, 3=Middle, 4=X1, 5=X2; `enum AnimationMode {Once, WhileHeld}`; `Keyframe(double DurationMs, Easing Easing, double GapAdd, double ScaleMul, double RotationAdd, double OpacityMul, Rgba? Color)`; `enum Easing {Linear, EaseIn, EaseOut, EaseInOut}`.
- `KeyBinding(InputKind Kind, int Code, Modifiers Mods)`; `[Flags] enum Modifiers {None=0, Ctrl=1, Alt=2, Shift=4, Win=8}`.
- `enum HotkeyAction {ToggleOverlay, NextCrosshair, PreviousCrosshair, HideWhileHeld, Center, SizeUp, SizeDown, SelectCrosshair}`; `HotkeyBinding(HotkeyAction Action, KeyBinding? Binding, string? CrosshairId)`.
- `Settings(int Schema, string? ActiveCrosshairId, bool OverlayEnabled, string? MonitorDeviceName, Vec2 Offset, double GlobalScale, bool HideOnRightClick, bool LaunchOnStartup, string? Language, IReadOnlyList<HotkeyBinding> Hotkeys)` with `static Settings Default` (overlay on, primary monitor, offset 0, scale 1, hide-on-right-click off, hotkeys: ToggleOverlay=F8, NextCrosshair=F9, PreviousCrosshair=Shift+F9, Center=Ctrl+F8, others unbound).
- `static class Json { JsonSerializerOptions Options; string Serialize<T>(T); T Deserialize<T>(string) }` – camelCase, enums as strings, `Layer` polymorphic via `"type"` discriminator `classic|radial|shape|image`.

- [ ] **Step 1: Write failing tests** – `Doc_RoundTrips_AllLayerTypes` (doc with one of each layer → serialize → deserialize → `Assert.Equal` per layer, layer list compared element-wise); `Rgba_Parse_6And8Digits` (`#00FFFF` → `(0,255,255,255)`, `#FF000080` → A=128, `ToHex()` = `#FF000080`); `Layer_HasTypeDiscriminator` (serialized ClassicLayer contains `"type":"classic"`); `Deserialize_UnknownSchema_Throws` (`"schema":99` → `NotSupportedException`).
- [ ] **Step 2:** `dotnet test --filter SerializationTests` → FAIL (types missing).
- [ ] **Step 3:** Implement the model and `Json`. Records holding lists need custom `Equals` or tests compare via JSON string equality — use JSON string equality in tests to keep records simple.
- [ ] **Step 4:** Run tests → PASS.
- [ ] **Step 5:** Commit `feat(core): data model and json serialization`.

---

### Task 2: Classic geometry with pixel snapping and outline

**Files:** `Geometry/Primitives.cs`, `Geometry/LayerGeometry.cs`, `Geometry/ClassicGeometry.cs`; Test `GeometryClassicTests.cs`.

**Interfaces – Produces:**
- `abstract record Prim(Rgba Fill, Rgba? OutlineColor, double OutlineWidth)`; `RectPrim(double X, double Y, double W, double H, ...)`, `EllipsePrim(double Cx, double Cy, double Rx, double Ry, double StrokeWidth /*0 = filled*/, ...)`, `PolygonPrim(IReadOnlyList<Vec2> Points, ...)`, `ArcPrim(double Cx, double Cy, double R, double StartDeg, double SweepDeg, double StrokeWidth, ...)`, `ImagePrim(string LayerId, double X, double Y, double W, double H)`.
- `LayerRender(string LayerId, IReadOnlyList<Prim> Items, double Opacity, double Blur, double Rotation, double Scale, Vec2 Offset, Vec2 Pivot)`. Renderer contract: draw outlines of all items first (each outline = stroke of width `2*OutlineWidth` centered on the shape edge, drawn under the fill, so it shows `OutlineWidth` outside), then fills; apply `Scale` and `Rotation` around `Pivot`, then `Offset`.
- `static class LayerGeometry { LayerRender Build(Layer layer, double sizeFactor, AnimatedProps? anim = null) }` – `sizeFactor` = `globalScale × (ReferenceHeight is int h ? monitorHeight/h : 1)`, multiplied into lengths/thickness/gap **before** rounding so lines stay pixel-sharp. Snapping rule: `Round(value × sizeFactor)`, minimum 1 for thickness/length/size, gap may be ≤ 0.

**Classic algorithm (pins the pixel layout):** let `t`=thickness, `L`=length, `g`=gap (all after scaling & rounding), `c0 = -((t-1)/2)` (integer division). Center block spans `[c0, c0+t)` on both axes. Pivot = `(c0 + t/2.0, c0 + t/2.0)`.
- Top: `Rect(c0, c0-g-L, t, L)`; Bottom: `Rect(c0, c0+t+g, t, L)`; Left: `Rect(c0-g-L, c0, L, t)`; Right: `Rect(c0+t+g, c0, L, t)`.
- Dot size `s`: `d0 = -((s-1)/2)`; square → `Rect(d0,d0,s,s)`, round → `EllipsePrim(d0+s/2.0, d0+s/2.0, s/2.0, s/2.0, 0)`.
- Outline enabled → every prim gets `OutlineColor = Outline.Color with alpha × Outline.Opacity`, `OutlineWidth = Round(Outline.Width × sizeFactor)` (min 1).

- [ ] **Step 1: Failing tests** – `Classic_T1_L4_G2_FourRects` (t=1,L=4,g=2 → Top `(0,-6,1,4)`, Bottom `(0,3,1,4)`, Left `(-6,0,4,1)`, Right `(3,0,4,1)`); `Classic_EvenThickness_ExtendsRightDown` (t=2,L=4,g=2 → Top `(0,-6,2,4)`, Bottom `(0,4,2,4)`); `Classic_TShape_OmitsTop` (`Top=false` → 3 rects); `Classic_SizeFactor_RoundsAfterScaling` (t=1,L=4,g=2, factor 1.333 → L=5, g=3, t=1); `Dot_Size3_Square` → `(-1,-1,3,3)`; `Outline_AppliedToAllPrims` (width 1, opacity 0.5, color black → every prim `OutlineColor.A == 128`, `OutlineWidth == 1`); `NegativeGap_Allowed` (t=1, L=4, g=-1 → Top `(0,-3,1,4)`).
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement `Primitives`, `ClassicGeometry.Build(ClassicLayer, double sizeFactor, AnimatedProps?)` and `LayerGeometry.Build` dispatch (other layer types throw `NotImplementedException` until Task 3).
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(core): classic crosshair geometry`.

---

### Task 3: Radial, shape and image geometry

**Files:** `Geometry/RadialGeometry.cs`, `Geometry/ShapeGeometry.cs`, extend `LayerGeometry.cs`; Test `GeometryShapesTests.cs`.

**Interfaces:** Consumes Task 2 `Prim`/`LayerRender`. Produces `LayerGeometry.Build` for all four layer types.

- Radial: arm *i* is a `PolygonPrim` = rectangle `t × L` starting at distance `g` from center `(0.5,0.5)`, rotated by `StartAngle + i×360/Arms` (0° = up, clockwise). Points rounded to 0.01.
- Shapes are centered on `(0.5,0.5)`: Circle → filled ellipse `Width/2`; Ring → ellipse with `StrokeWidth`; Rectangle → `RectPrim` centered; Triangle → isosceles, apex up, bounding box `Width×Height`; Chevron → 6-point polygon "^" with arm thickness `StrokeWidth`; Arc → `ArcPrim(R=Width/2, ArcStart, ArcSweep, StrokeWidth)`; Line → horizontal `RectPrim Width × StrokeWidth`.
- Image → one `ImagePrim` centered on `(0.5,0.5)` with `Width × Height × sizeFactor`.

- [ ] **Step 1: Failing tests** – `Radial_3Arms_AnglesAt0_120_240` (first polygon's far-edge midpoint has `X≈0.5`, `Y<0`; second's angle from center ≈ 120°, tolerance 0.5°); `Radial_ArmsOutOfRange_Clamped` (Arms=12 → 8 polygons, Arms=1 → 2); `Ring_StrokeWidth` (Ring W=10, stroke 2 → `EllipsePrim(0.5,0.5,5,5,2)`); `Triangle_ApexUp` (min-Y point is the apex with X=0.5); `Image_ScaledBySizeFactor` (W=20,H=10, factor 2 → `ImagePrim(id, -19.5, -9.5, 40, 20)`).
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(core): radial, shape and image geometry`.

---

### Task 4: Animation evaluator

**Files:** `Animation/AnimationEvaluator.cs`; Test `AnimationEvaluatorTests.cs`.

**Interfaces – Produces:**
- `record AnimatedProps(double GapAdd, double ScaleMul, double RotationAdd, double OpacityMul, Rgba? Color)`; `AnimatedProps.Identity = (0,1,0,1,null)`. Consumed by `LayerGeometry.Build` (gap += GapAdd before rounding; scale/rotation/opacity/color applied to the `LayerRender`).
- `sealed class AnimationState` tracking per layer: `void OnTrigger(string layerId, bool down, double nowMs)`; `AnimatedProps Evaluate(Layer layer, double nowMs)`; `bool IsAnimating(double nowMs)` (renderer uses this to stop the frame loop – NF-1).
- Semantics: keyframes are stages from Identity → K1 → K2 … ; `Once`: a trigger-down starts the sequence from 0 (restarts if retriggered), ends back at Identity after the last stage. `WhileHeld`: plays stages to the last keyframe and holds while down; on release interpolates back to Identity over the first keyframe's duration. Easing: EaseIn `t²`, EaseOut `1-(1-t)²`, EaseInOut smoothstep.

- [ ] **Step 1: Failing tests** – `Once_Linear_UpAndBack` (one keyframe GapAdd=4, 100 ms, linear; `Once` = Identity→K1 over 100 ms, then K1→Identity over the same 100 ms; trigger at 0 → GapAdd 2 at 50 ms, 4 at 100 ms, 2 at 150 ms, Identity at 200 ms and `IsAnimating(200)` false); `WhileHeld_HoldsLastKeyframe` (held 1000 ms → GapAdd=4); `WhileHeld_Release_ReturnsToIdentity`; `Retrigger_Restarts`; `EaseOut_Value` (at t=0.5 → 0.75 of target).
- [ ] **Step 2–4:** FAIL → implement → PASS.
- [ ] **Step 5:** Commit `feat(core): animation evaluator`.

---

### Task 5: Share code and file import/export

**Files:** `Codecs/ShareCode.cs`; Test `ShareCodeTests.cs`.

**Interfaces – Produces:** `static class ShareCode { string Encode(CrosshairDoc doc); ImportResult Decode(string code) }`; `record ImportResult(CrosshairDoc? Doc, string? Error, string? Warning = null)` (shared with Task 6). Format: `RTCL1.` + base64url(Brotli(UTF-8 JSON of the doc with `Id`, timestamps stripped)). Decode assigns a new `Id` (`Guid.NewGuid().ToString("N")`), now-timestamps, trims whitespace. File export/import = plain `Json.Serialize(doc)` (no new code needed; App uses `Json`).

- [ ] **Step 1: Failing tests** – `RoundTrip_PreservesLayers` (layers JSON-equal after encode/decode); `Decode_NewId`; `Decode_TrimsWhitespace`; `Decode_Garbage_ReturnsError` (`"hello"`, `"RTCL1.!!!"`, `""`, a Valorant code → `Doc == null`, `Error` non-empty, no exception); `Encode_IsCompact` (classic default doc → code length < 300).
- [ ] **Step 2–4:** FAIL → implement → PASS.
- [ ] **Step 5:** Commit `feat(core): share codes`.

---

### Task 6: Valorant crosshair code import

**Files:** `Codecs/ValorantCode.cs`; Test `ValorantCodeTests.cs`.

**Interfaces – Produces:** `static class ValorantCode { ImportResult Parse(string code) }` → `CrosshairDoc` with `ReferenceHeight = 1080`, name `"Valorant Import"`, layers: `inner` (ClassicLayer), `outer` (ClassicLayer, only if outer lines shown), `dot` (ClassicLayer with no lines, only if center dot on).

Format: `;`-separated key/value pairs; leading `0` = version; sections start at markers `P` (primary), `A` (ADS), `S` (sniper) – use only keys after `P` until the next marker. **Before implementing, verify the full key list and every default value against a current public reference** (e.g. the Valorant crosshair-code documentation on valorant.fandom.com or a well-known open-source Valorant code parser) and record the source URL in a code comment. Known keys to map:
- `c` color index: 0 `#FFFFFF`, 1 `#00FF00`, 2 `#7FFF00`, 3 `#DFFF00`, 4 `#FFFF00`, 5 `#00FFFF`, 6 `#FF00FF`, 7 `#FF0000`, 8 = custom from `u` (`RRGGBBAA`).
- `h` outlines on/off, `t` outline thickness, `o` outline opacity → `Outline` on every layer.
- `d` center dot on/off, `z` dot thickness, `a` dot opacity.
- `0b` inner lines shown, `0t` thickness, `0l` length, `0v` vertical length (only if `0g` = 1, else = `0l`), `0o` offset (→ Gap), `0a` opacity, `0f` firing error (→ IM-2: `LayerAnimation` on Mouse Left, `Once`, one keyframe `GapAdd=3, 80 ms, EaseOut`).
- `1b`, `1t`, `1l`, `1v`, `1g`, `1o`, `1a`, `1f` same for outer lines.
- Unknown keys ignored. Separate vertical length is approximated by using `0l` for horizontal and a second classic layer is **not** created; when `0v` ≠ `0l`, use `0l` and set `ImportResult.Warning` to a message naming both values.

- [ ] **Step 1: Failing tests** – `Parse_Requirements_Example` (`0;P;c;5;h;0;0l;4;0o;2;0a;1;0f;0;1b;0` → one `inner` layer, Color `#00FFFF`, Length 4, Gap 2, Opacity 1, Outline disabled, no outer layer, no Animation, `ReferenceHeight` 1080); `Parse_CustomColor` (`0;P;c;8;u;FF5500FF` → Color `#FF5500FF`); `Parse_UsesOnlyPrimary` (`0;P;c;1;A;c;7` → green); `Parse_CenterDot` (`0;P;d;1;z;3;a;1;0b;0;1b;0` → only `dot` layer, size 3); `Parse_FiringError_AddsAnimation` (`0f;1`); `Parse_IgnoresUnknownKeys` (`0;P;c;1;zz;9;` → OK); `Parse_Garbage_ReturnsError` (`""`, `"hello"`, `RTCL1.abc`).
- [ ] **Step 2–4:** FAIL → research defaults & implement → PASS.
- [ ] **Step 5:** Commit `feat(core): valorant crosshair code import`.

---

### Task 7: Hotkey matcher

**Files:** `Hotkeys/HotkeyMatcher.cs`; Test `HotkeyMatcherTests.cs`.

**Interfaces – Produces:**
- `record InputEvent(InputKind Kind, int Code, bool Down)` (keyboard Code = Win32 VK; modifier state is derived by the matcher from VK_*CONTROL/MENU/SHIFT/LWIN/RWIN events it sees).
- `abstract record OverlayCommand`: `ToggleOverlay`, `Cycle(int Delta)`, `SelectCrosshair(string Id)`, `SetHidden(bool Hidden)`, `CenterOffset`, `ScaleStep(int Delta)`, `AnimationTrigger(InputKind Kind, int Code, bool Down)`.
- `sealed class HotkeyMatcher(Func<Settings> settings) { IReadOnlyList<OverlayCommand> Process(InputEvent e) }`.
- Rules: a binding fires on the **down** edge only when its exact modifier set is held; repeated downs while already pressed are ignored; `HideWhileHeld` → `SetHidden(true)` on down, `SetHidden(false)` on its own up regardless of other keys; `HideOnRightClick` → same with Mouse Right; every event (down/up, keyboard and mouse) also yields `AnimationTrigger` so layer animations can react; a modifier key alone never fires a non-modifier binding.

- [ ] **Step 1: Failing tests** – `F8_Down_TogglesOverlay`; `Repeat_IsIgnored` (two F8 downs without up → one command); `ShiftF9_Previous_NotNext`; `HideWhileHeld_ReleasesAfterOtherKey` (bind H; H down, W down, H up → `SetHidden(true)`, then `SetHidden(false)`); `RightClick_HidesWhenEnabled` and `…_NothingWhenDisabled`; `PerCrosshairKey_SelectsCrosshair` (`SelectCrosshair` binding on key 1 with CrosshairId "abc"); `EveryEvent_EmitsAnimationTrigger`.
- [ ] **Step 2–4:** FAIL → implement → PASS.
- [ ] **Step 5:** Commit `feat(core): hotkey matcher`.

---

### Task 8: Storage and starter presets

**Files:** `Storage/LibraryStore.cs`, `Storage/SettingsStore.cs`, `Storage/Presets.cs`, `Storage/Presets/*.json` (embedded resources); Test `StorageTests.cs`.

**Interfaces – Produces:**
- `sealed class LibraryStore(string rootDir) { IReadOnlyList<CrosshairDoc> LoadAll(Action<string>? onError = null); void Save(CrosshairDoc); void Delete(string id) }` – one file per doc `crosshairs/<id>.json`, atomic write (write `.tmp` then `File.Move(overwrite:true)`).
- `sealed class SettingsStore(string rootDir) { Settings Load(); void Save(Settings) }` – missing/corrupt file → `Settings.Default` (corrupt file renamed to `settings.corrupt-<timestamp>.json`).
- `static class Presets { IReadOnlyList<CrosshairDoc> All }` – ≥ 15 original designs (e.g. classic small cross, classic + dot, dot only, T-shape, thick cross with outline, circle-dot, ring, 3-arm radial, 6-arm radial, chevron, arc pair, triangle, cross with firing animation, sniper-style long thin cross, "Valorant default look-alike" built from neutral values). Names in English, colors varied. First start (empty library) seeds them into the store.
- Default root: `Path.Combine(Environment.GetFolderPath(SpecialFolder.ApplicationData), "Reticle")`.

- [ ] **Step 1: Failing tests** (temp dir) – `SaveLoad_RoundTrip`; `Load_SkipsCorruptFile` (one valid + one `{garbage` file → 1 doc returned, `onError` called once); `Delete_RemovesFile`; `Settings_Missing_ReturnsDefault`; `Settings_Corrupt_ReturnsDefaultAndBacksUp`; `Presets_AtLeast15_AllUniqueIds_AllBuildGeometry` (every preset passes `LayerGeometry.Build` without throwing).
- [ ] **Step 2–4:** FAIL → implement → PASS.
- [ ] **Step 5:** Commit `feat(core): storage and presets`.

---

### Task 9: Overlay window (click-through, DPI-exact, multi-monitor)

**Files:** `src/Reticle.App/Overlay/*`, `AppState.cs`; Test `tests/Reticle.App.Tests/OverlayPlacementTests.cs`.

**Interfaces:**
- Consumes `LayerGeometry.Build`, `AnimationState`, `Settings`, `CrosshairDoc`.
- Produces `sealed class AppState : ObservableObject` (`Settings Settings`, `IReadOnlyList<CrosshairDoc> Library`, `CrosshairDoc? Active`, `bool Hidden`, `AnimationState Animations`, `event Action? Changed`, methods `Update(Func<Settings,Settings>)`, `SaveDoc(CrosshairDoc)`, `DeleteDoc(string)`, `Apply(OverlayCommand)`); persists through Task 8 stores (settings debounced 500 ms).
- `record MonitorInfo(string DeviceName, RectI BoundsPx, double DpiScale, bool IsPrimary)`; `MonitorService.GetAll()` via `EnumDisplayMonitors` + `GetMonitorInfo` + `GetDpiForMonitor`.
- `static class OverlayPlacement { PointI WindowTopLeft(MonitorInfo m, Vec2 offset, int windowSize) }` → `(m.Left + m.Width/2 + round(offset.X) - windowSize/2, m.Top + m.Height/2 + round(offset.Y) - windowSize/2)`; center pixel of the window is at `(windowSize/2, windowSize/2)`. `WindowSize = 768`.
- `OverlayWindow`: WPF `Window` with `WindowStyle=None`, `AllowsTransparency=true`, `Background=Transparent`, `Topmost=true`, `ShowInTaskbar=false`, `ShowActivated=false`; on `SourceInitialized` add `WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`; position/size only via `SetWindowPos` in device pixels; a 1 s `DispatcherTimer` re-asserts `HWND_TOPMOST` with `SWP_NOACTIVATE|SWP_NOMOVE|SWP_NOSIZE` (OV-10); re-place on `WM_DISPLAYCHANGE`/`WM_DPICHANGED`.
- `OverlayRenderer : FrameworkElement` – `OnRender` applies `ScaleTransform(1/DpiScale)` so 1 unit = 1 device px and translates origin to the window center pixel; for layers whose rotation is a multiple of 90° and scale 1 use `RenderOptions.EdgeMode=Aliased` (sharp), otherwise anti-aliased; blur via `BlurEffect` on a per-layer child visual only if `Blur > 0`; images decoded once per layer id (GIF frames via `GifBitmapDecoder`, frame delays from metadata `/grctlext/Delay`). Uses `CompositionTarget.Rendering` only while `AnimationState.IsAnimating` or an animated GIF is visible; otherwise redraws only on `AppState.Changed`.

- [ ] **Step 1: Failing tests** – `OverlayPlacement_EvenResolution` (1920×1080 at (0,0), offset 0, size 768 → `(576,156)`); `OverlayPlacement_OddResolution` (1366×767 → `(299,-1)`); `OverlayPlacement_SecondMonitor_Offset` (bounds `(1920,0,2560,1440)`, offset `(3,-2)` → `(2819,334)`); `AppState_Cycle_WrapsAround`; `AppState_SelectUnknownId_NoChange`.
- [ ] **Step 2–4:** FAIL → implement → PASS.
- [ ] **Step 5: Manual verification** – run the app (`dotnet run --project src/Reticle.App`) with a temporary debug startup that shows the overlay with a preset; take a screenshot via PowerShell (`[System.Windows.Forms.Screen]` + `Graphics.CopyFromScreen`), assert in a scratch script that the pixel at monitor center is the crosshair color; click on the desktop through the crosshair and confirm the click reaches the window below. Repeat at 125 % scaling if available; otherwise note it as unverified.
- [ ] **Step 6:** Commit `feat(app): click-through overlay window`.

---

### Task 10: Global input hooks wired to the overlay

**Files:** `Input/InputHookService.cs`, modify `AppState.cs`, `App.xaml.cs`.

**Interfaces:** Consumes `HotkeyMatcher`, `AppState.Apply`. Produces `sealed class InputHookService : IDisposable { event Action<InputEvent>? Input; void Start(); }`.
- Dedicated background thread with its own message loop (`GetMessage`) installs `WH_KEYBOARD_LL` and `WH_MOUSE_LL`; the callback only enqueues an `InputEvent` to the WPF dispatcher (`BeginInvoke`) and returns `CallNextHookEx` immediately (never returns 1 → never blocks input). Mouse move events are not forwarded.
- `App` routes events → `HotkeyMatcher.Process` → `AppState.Apply` (and `AnimationTrigger` → `AnimationState.OnTrigger` for every layer whose trigger matches).

- [ ] **Step 1:** Implement; add an App test `AppState_Apply_SetHidden_RaisesChanged`.
- [ ] **Step 2:** `dotnet test` → PASS.
- [ ] **Step 3: Manual verification** – with Notepad focused: F8 toggles overlay; holding right mouse with smart toggle on hides it; a preset with firing animation spreads on left click; CPU of Reticle in Task Manager stays < 1 % while idle.
- [ ] **Step 4:** Commit `feat(app): global hotkeys, smart toggle, animation triggers`.

---

### Task 11: Main window – Library and Designer

**Files:** `Views/MainWindow.xaml`, `LibraryView.xaml`, `DesignerView.xaml`, `ViewModels/LibraryViewModel.cs`, `DesignerViewModel.cs`, `Resources/Strings*.resx`.

**Interfaces:** Consumes `AppState`, `LayerGeometry`, `ShareCode`, `ValorantCode`, `Json`. Produces navigation shell (left nav: Bibliothek, Designer, Position & Monitor, Hotkeys, Einstellungen) used by Task 12.
- Own visual identity: dark neutral theme with a single accent color `#4FD1C5` (teal), rounded 6 px corners, Segoe UI Variable. Do not imitate the reference product's layout.
- **Library:** grid of cards with live thumbnails (reuse `OverlayRenderer` in a 96 px viewbox on a mid-grey background), active marker, actions: Aktivieren, Bearbeiten, Duplizieren, Umbenennen, Löschen (with confirm), "Neu", "Importieren…" (opens `ImportDialog`: one text box, auto-detects `RTCL1.` share code vs Valorant code, shows `Error`/`Warning`), "Code kopieren", "Als Datei exportieren/importieren" (`.reticle.json`), "Als PNG exportieren" (render 256 px `RenderTargetBitmap`, transparent).
- **Designer:** left = layer list (add Classic/Radial/Shape/Image, delete, duplicate, drag-reorder, visibility eye, rename); center = live preview (zoom 1×–8× nearest-neighbour, background dark/light/checker/sample image bundled as an original, generic screenshot-free gradient); right = property panel for the selected layer type incl. outline and animation editor (trigger picker by "press a key", mode, keyframe list). Edits apply to the overlay live (DE-1) via `AppState.SaveDoc` debounced 150 ms. Undo/Redo (Ctrl+Z/Ctrl+Y) with a 100-step snapshot stack of the doc (DE-11).
- Image import: PNG/JPG/WebP/GIF, max 2 MB, larger → error message (DE-6).

- [ ] **Step 1:** Implement views and view models; add App tests `DesignerViewModel_UndoRedo_RestoresDoc` and `ImportDialog_DetectsFormat`.
- [ ] **Step 2:** `dotnet test` → PASS.
- [ ] **Step 3: Manual verification** – screenshot of Library and Designer; change thickness in Designer and confirm overlay updates without restart; import the Requirements example Valorant code and confirm acceptance criterion 9.
- [ ] **Step 4:** Commit `feat(app): library and designer`.

---

### Task 12: Position & Monitor, Hotkeys and Settings pages

**Files:** `Views/PositionView.xaml`, `HotkeysView.xaml`, `SettingsView.xaml` + view models.

- **Position & Monitor:** monitor dropdown (`MonitorService.GetAll`, shows name + resolution + primary marker), offset X/Y numeric with ±1 buttons, "Zentrieren" (0,0), global size slider 0.25–4 (step 0.05) with numeric box.
- **Hotkeys:** list of `HotkeyAction`s with a "press to bind" capture button (keyboard incl. modifiers, mouse buttons 3–5; Esc cancels, Backspace clears = "Keine Aktion"); section "Crosshair-Tasten" to add per-crosshair bindings (HK-3); conflict warning when two bindings are identical (HK-6); checkbox "Bei Rechtsklick ausblenden" (HK-4).
- **Settings:** language (System/Deutsch/English, restart hint), "Mit Windows starten", "Overlay beim Start anzeigen", "Als Administrator neu starten" button, data folder "Öffnen" button, version info.

- [ ] **Step 1:** Implement; App test `HotkeysViewModel_DetectsConflict`.
- [ ] **Step 2:** `dotnet test` → PASS.
- [ ] **Step 3: Manual verification** – bind a per-crosshair key and switch crosshairs in Notepad (criterion 5); move to second monitor if present (criterion 7, else note unverified).
- [ ] **Step 4:** Commit `feat(app): position, hotkeys and settings pages`.

---

### Task 13: Tray, single instance, autostart, elevation

**Files:** `System/TrayService.cs`, `SingleInstance.cs`, `AutostartService.cs`, `ElevationService.cs`, modify `App.xaml.cs`.

- Tray (`System.Windows.Forms.NotifyIcon`, own original icon): left-click opens main window; menu: Overlay an/aus, Crosshair ▸ (library list), Editor öffnen, Beenden. Closing the main window hides it to tray (first time shows a balloon hint).
- Single instance: named `Mutex` `Local\Reticle.SingleInstance` + named pipe `Reticle.Activate`; second start signals the first to show its window and exits (AP-6).
- Autostart: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value `Reticle` = `"<exe>" --minimized` (AP-2).
- Elevation: `ElevationService.IsElevated()`; `IsForegroundElevated()` via `GetForegroundWindow` → `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)` + `OpenProcessToken`/`GetTokenInformation(TokenElevation)` (access denied ⇒ treat as elevated); polled every 3 s; if foreground elevated and self not → banner in main window + one tray notification "Spiel läuft als Administrator – Hotkeys funktionieren erst nach Neustart von Reticle als Administrator" with restart action (`runas` verb). (Review Focus 5)

- [ ] **Step 1:** Implement; App test `Autostart_Command_ContainsMinimizedFlag`.
- [ ] **Step 2:** `dotnet test` → PASS. **Step 3:** Manual: start twice → one instance; restart Windows-style test by running the Run-key command manually.
- [ ] **Step 4:** Commit `feat(app): tray, single instance, autostart, elevation`.

---

### Task 14: Publish and Valorant guide

**Files:** `README.md`, `build/publish.ps1`, optional `build/installer.iss`.

- `publish.ps1`: `dotnet publish src/Reticle.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist/` → `dist/Reticle.exe` (portable, AP-5). `installer.iss` (Inno Setup) is written but only built if `iscc` is available.
- README (German): Installation; first steps; **Valorant-Setup**: Anzeigemodus „Vollbild im Fenster“; In-Game-Fadenkreuz ausblenden (Valorant: Fadenkreuz → Innere/Äußere Linien aus, Mittelpunkt aus) oder bewusst kombinieren; Valorant-Code importieren; bei Riot Client als Admin → Reticle als Admin; Hinweis zur Nutzung auf eigenes Risiko (reines Overlay ohne Spielzugriff, aber keine offizielle Freigabe durch Riot); Troubleshooting (Overlay unsichtbar in Exclusive Fullscreen, Hotkeys gehen nicht, verschwommen bei Skalierung).

- [ ] **Step 1:** Run `build/publish.ps1` → `dist/Reticle.exe` exists; start it, overlay visible, close via tray.
- [ ] **Step 2:** Run full `dotnet test` → all PASS; walk through acceptance criteria 1–8 from `Requirements.md` §6 and record pass/unverified in `docs/acceptance.md`.
- [ ] **Step 3:** Commit `chore: publish script, README and acceptance report`.

---

## Self-review notes

- Spec coverage: OV-1..10 → T9/T10; OV-11 out of scope (Kann). DE-1..6, 9..12 → T1–T3, T11; DE-7/8 (Kann) out. AN-1..4 → T4, T7, T10, T11. LB-1..4 → T8, T11; LB-5 (Kann) out. HK-1..6 → T7, T10, T12, T13. SH-1..3 → T5, T11. IM-1..3 → T6. AP-1..6 → T8, T11–T14. NF-1 → T9/T10, NF-2 → hook→dispatcher path T10, NF-3..6 → global constraints/T1/T8.
- Acceptance criterion 10 (real Valorant) can only be verified by the user; the build will report it as unverified.
