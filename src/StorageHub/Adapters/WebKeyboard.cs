using GameCore.HotUpdate;
using GameCore.HotUpdate.ReduxUI;

namespace StorageHub.Adapters;

/// <summary>
/// Sends the keyboard to the web UI while the panel's search box has focus, the way the game's own crafting search
/// does, and gives it back as it was found.
/// </summary>
internal sealed class WebKeyboard
{
    private bool? _wasEnabled;

    public void Claim()
    {
        var layer = BaseSingleton<ReduxUISystem>.Instance?.GetWebUILayer();

        if (layer == null) return;

        _wasEnabled ??= layer.IsKeyboardEnabled;
        layer.SetKeyboardEnabled(true);
    }

    public void Release()
    {
        if (_wasEnabled is not { } wasEnabled) return;

        _wasEnabled = null;
        BaseSingleton<ReduxUISystem>.Instance?.GetWebUILayer()?.SetKeyboardEnabled(wasEnabled);
    }
}
