using System.Collections.Generic;
using PeglinRelicRerolls.Core;
using Xunit;

namespace PeglinRelicRerolls.Tests
{
    public class ShortcutRulesTests
    {
        // Stand-in for Unity's KeyCode, where None is 0.
        private enum Key
        {
            None,
            T,
            LeftShift,
            JoystickButton4,
            JoystickButton9,
            Joystick1Button9,
        }

        private static bool IsDown(Key main, Key[] modifiers, Key[] wentDown, Key[] held)
        {
            var down = new HashSet<Key>(wentDown);
            var holding = new HashSet<Key>(held);
            return ShortcutRules.IsDown(main, modifiers, down.Contains, holding.Contains);
        }

        [Fact]
        public void IsDown_PadButton_FiresDespiteUnityReportingItTwice()
        {
            Key[] pressed = { Key.JoystickButton9, Key.Joystick1Button9 };
            Assert.True(IsDown(Key.JoystickButton9, new Key[0], pressed, pressed));
        }

        [Fact]
        public void IsDown_OnlyOnTheFrameTheKeyGoesDown()
        {
            Assert.False(IsDown(Key.JoystickButton9, new Key[0], new Key[0], new[] { Key.JoystickButton9 }));
        }

        [Fact]
        public void IsDown_OtherKeyDown_DoesNotFire()
        {
            Assert.False(IsDown(Key.JoystickButton9, new Key[0], new[] { Key.T }, new[] { Key.T }));
        }

        [Fact]
        public void IsDown_NoneNeverFires()
        {
            Key[] pressed = { Key.None };
            Assert.False(IsDown(Key.None, new Key[0], pressed, pressed));
        }

        [Fact]
        public void IsDown_RequiresEveryModifierHeld()
        {
            Key[] modifiers = { Key.JoystickButton4, Key.LeftShift };
            Key[] down = { Key.JoystickButton9 };
            Assert.False(IsDown(Key.JoystickButton9, modifiers, down, new[] { Key.JoystickButton9, Key.JoystickButton4 }));
            Assert.True(IsDown(Key.JoystickButton9, modifiers, down, new[] { Key.JoystickButton9, Key.JoystickButton4, Key.LeftShift }));
        }

        [Fact]
        public void IsDown_KeyboardKey_WorksToo()
        {
            Assert.True(IsDown(Key.T, new Key[0], new[] { Key.T }, new[] { Key.T, Key.LeftShift }));
        }

        [Fact]
        public void IsDown_NoModifierList_StillFires()
        {
            Assert.True(ShortcutRules.IsDown(Key.T, null, key => key == Key.T, key => key == Key.T));
        }

        [Fact]
        public void IsDown_NoneAsModifier_NeverFires()
        {
            Assert.False(IsDown(Key.T, new[] { Key.None }, new[] { Key.T }, new[] { Key.T }));
        }
    }
}
