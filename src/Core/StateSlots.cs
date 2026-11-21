using System.Text;

namespace PeglinRelicRerolls.Core
{
    /// <summary>
    /// File names for the reroll state. The default slot keeps the file name earlier versions used, so existing
    /// players keep their state; other slots get their own file.
    /// </summary>
    public static class StateSlots
    {
        public const string DefaultFileName = "PeglinRelicRerolls.state";

        private const int MaxSlotLength = 64;

        /// <summary>
        /// Which file to read on continue. Only the default slot falls back to the pre-rename file, and only when
        /// its current file is missing; every other slot reads its own file and never the legacy one.
        /// </summary>
        public static bool ReadLegacyFile(bool isDefaultSlot, bool primaryExists)
        {
            return isDefaultSlot && !primaryExists;
        }

        /// <summary>The state file name for a slot. Null, empty or blank means the default slot.</summary>
        public static string FileName(string slot)
        {
            string safe = Sanitise(slot);
            return safe.Length == 0 ? DefaultFileName : "PeglinRelicRerolls." + safe + ".state";
        }

        /// <summary>
        /// Reduces a slot name to <c>[a-z0-9_-]</c> so it can't escape the config folder or hit a reserved name.
        /// A name that needed any change (other characters, capitals, too long) gets a short hash of the original
        /// appended, so two different names never share a file ("a b" and "a_b", or "Run" and "run" on a
        /// case-insensitive disk). A name that is already lowercase letters, digits, '_' and '-' is kept as it is.
        /// </summary>
        public static string Sanitise(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot))
            {
                return string.Empty;
            }
            StringBuilder builder = new StringBuilder();
            bool changed = false;
            foreach (char c in slot)
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-')
                {
                    builder.Append(c);
                }
                else if (c >= 'A' && c <= 'Z')
                {
                    builder.Append((char)(c + ('a' - 'A')));
                    changed = true;
                }
                else
                {
                    builder.Append('_');
                    changed = true;
                }
            }
            if (builder.Length > MaxSlotLength)
            {
                builder.Length = MaxSlotLength;
                changed = true;
            }
            if (changed)
            {
                builder.Append('-').Append(ShortHash(slot));
            }
            return builder.ToString();
        }

        // FNV-1a over the UTF-16 characters: stable across runtimes, unlike string.GetHashCode.
        private static string ShortHash(string text)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in text)
                {
                    hash = (hash ^ c) * 16777619;
                }
                return hash.ToString("x8");
            }
        }
    }
}
