using System;
using System.Collections.Generic;
using System.Linq;

namespace PeglinRelicRerolls.Core
{
    /// <summary>When a configured button counts as pressed. Generic over the key type so it needs no Unity reference.</summary>
    public static class ShortcutRules
    {
        /// <summary>
        /// True on the frame <paramref name="mainKey"/> goes down while every modifier is held. Other held keys don't
        /// matter, unlike BepInEx's KeyboardShortcut.IsDown: Unity reports every pad button twice (JoystickButton9 and
        /// Joystick1Button9), so that check never passes for a controller button. The default key (None) never fires.
        /// </summary>
        public static bool IsDown<TKey>(TKey mainKey, IEnumerable<TKey> modifiers, Func<TKey, bool> wentDown, Func<TKey, bool> held)
        {
            return !EqualityComparer<TKey>.Default.Equals(mainKey, default)
                && wentDown(mainKey)
                && (modifiers == null || modifiers.All(held));
        }
    }
}
