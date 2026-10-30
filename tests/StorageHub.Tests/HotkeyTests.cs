using StorageHub.Core;
using Xunit;

namespace StorageHub.Tests;

/// <summary>The toggle key: unbound by default since the HUD button opens the panel, and silent while the player types.</summary>
public class HotkeyTests
{
    private int _pressAsks;
    private int _typingAsks;

    private Func<bool> Pressed(bool isPressed) => () =>
    {
        _pressAsks++;

        return isPressed;
    };

    private Func<bool> Typing(bool isTyping) => () =>
    {
        _typingAsks++;

        return isTyping;
    };

    [Fact]
    public void The_toggle_key_is_unbound_by_default()
    {
        Assert.Equal("None", Hotkey.DefaultKey);
    }

    [Fact]
    public void An_unbound_key_never_fires_and_asks_for_nothing()
    {
        Assert.False(Hotkey.Fires(isBound: false, Pressed(true), Typing(false)));
        Assert.Equal(0, _pressAsks);
        Assert.Equal(0, _typingAsks);
    }

    [Fact]
    public void A_bound_key_fires_on_its_press()
    {
        Assert.True(Hotkey.Fires(isBound: true, Pressed(true), Typing(false)));
    }

    [Fact]
    public void A_bound_key_does_nothing_while_the_player_types()
    {
        Assert.False(Hotkey.Fires(isBound: true, Pressed(true), Typing(true)));
    }

    [Fact]
    public void Typing_is_only_checked_when_the_key_goes_down()
    {
        Assert.False(Hotkey.Fires(isBound: true, Pressed(false), Typing(false)));
        Assert.Equal(0, _typingAsks);
    }
}
