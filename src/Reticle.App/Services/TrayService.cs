using System.Drawing;
using System.Windows.Forms;
using Reticle.Core.Hotkeys;

namespace Reticle.App.Services;

/// <summary>Owns the notification-area control surface while the editor window is hidden.</summary>
public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly AppState _state;
    private readonly Action _showEditor;
    private readonly Action _exit;
    public bool IsExiting { get; private set; }
    public TrayService(AppState state, Action showEditor, Action exit)
    {
        _state = state; _showEditor = showEditor; _exit = exit;
        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) => BuildMenu(menu);
        _icon = new NotifyIcon { Text = "Reticle", Icon = SystemIcons.Application, ContextMenuStrip = menu, Visible = true };
        _icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) _showEditor(); };
    }
    private void BuildMenu(ContextMenuStrip menu)
    {
        menu.Items.Clear();
        menu.Items.Add(_state.Settings.OverlayEnabled ? "Overlay ausblenden" : "Overlay anzeigen", null, (_, _) => _state.Apply(new ToggleOverlay()));
        var crosshairs = new ToolStripMenuItem("Crosshair");
        foreach (var doc in _state.Library)
        {
            var item = new ToolStripMenuItem(doc.Name, null, (_, _) => _state.Apply(new SelectCrosshair(doc.Id)));
            crosshairs.DropDownItems.Add(item);
            item.Checked = doc.Id == _state.Settings.ActiveCrosshairId;
        }
        menu.Items.Add(crosshairs);
        menu.Items.Add("Editor öffnen", null, (_, _) => _showEditor());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => { IsExiting = true; _exit(); });
    }
    public void Dispose() { _icon.Visible = false; _icon.Dispose(); }
    public void ShowElevationWarning() => _icon.ShowBalloonTip(8000, "Reticle", "Spiel läuft als Administrator – Hotkeys funktionieren erst nach Neustart von Reticle als Administrator.", ToolTipIcon.Warning);
}
