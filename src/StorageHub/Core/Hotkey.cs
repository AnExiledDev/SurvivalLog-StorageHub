using System;

namespace StorageHub.Core;

/// <summary>
/// When the toggle key opens or closes the panel. The HUD button opens it too, so the key is unbound by default, and a
/// bound key does nothing while the player types in a text box.
/// </summary>
internal static class Hotkey
{
    /// <summary>The Unity key name the ToggleKey setting starts with: none, so no key is taken from the game or other mods.</summary>
    public const string DefaultKey = "None";

    /// <summary>
    /// Whether this frame's press fires the key. The press and the typing check are only asked for when they can change
    /// the answer: an unbound key asks nothing, and typing is only checked on a press.
    /// </summary>
    public static bool Fires(bool isBound, Func<bool> isPressed, Func<bool> isTyping) => isBound && isPressed() && !isTyping();
}
