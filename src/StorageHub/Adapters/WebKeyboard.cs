using GameCore.HotUpdate;
using GameCore.HotUpdate.ReduxUI;
using GameCore.Scripts;

namespace StorageHub.Adapters;

/// <summary>
/// Sends the keyboard to the web UI while one of the panel's text fields has focus, the way the game's own crafting
/// search does, and gives it back as it was found. The game's hotkeys are held off for as long as the field has it.
/// The game keeps the system's input method (for typing Chinese) off while playing,
/// and turns it on only while one of its own text boxes has focus (Reducer_Web_ToolTable.RA_SearchFocus), so a page
/// field turns it on too, and off again when it lets go.
/// </summary>
internal sealed class WebKeyboard
{
    private bool? _wasEnabled;

    /// <summary>True while a page field holds the keyboard, so its keys, Esc included, are the page's to handle.</summary>
    public bool IsClaimed => _wasEnabled != null;

    public void Claim()
    {
        InputGate.TypingStarted();

        var layer = GameSingleton.Existing<ReduxUISystem>()?.GetWebUILayer();

        if (layer == null) return;

        _wasEnabled ??= layer.IsKeyboardEnabled;
        layer.SetKeyboardEnabled(true);
        ImeGuard.SetEnabled(true);
    }

    public void Release()
    {
        InputGate.TypingEnded();

        if (_wasEnabled is not { } wasEnabled) return;

        _wasEnabled = null;
        GameSingleton.Existing<ReduxUISystem>()?.GetWebUILayer()?.SetKeyboardEnabled(wasEnabled);
        ImeGuard.SetEnabled(false);
    }
}
