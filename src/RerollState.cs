using System;
using System.IO;
using System.Linq;
using BepInEx;
using Cruciball;
using PeglinRelicRerolls.Core;
using UnityEngine;

namespace PeglinRelicRerolls
{
    /// <summary>
    /// Game-side wrapper around <see cref="RerollCounter"/> for the current run.
    ///
    /// The live count changes freely during play, but it is only written to disk when the game
    /// itself saves the run (see SavePatches), and restored from disk when a run is continued.
    /// That keeps rerolls in lockstep with gold and relics: quitting mid-shop and continuing
    /// rolls back both the gold spent and the reroll bought, so purchases can't be duplicated.
    ///
    /// Stored in its own file (not the game save) so a modded value never ends up in Peglin's save data.
    /// </summary>
    internal static class RerollState
    {
        private static readonly RerollCounter Counter = new RerollCounter();

        /// <summary>
        /// The slot's file. The default slot is the file earlier versions used. Looked up on every save and load,
        /// because another mod decides the slot per run.
        /// </summary>
        private static string FilePath(string slot) => Path.Combine(Paths.ConfigPath, StateSlots.FileName(slot));

        public static int Count => Counter.Count;

        public static int Used => Counter.Used;

        public static void ResetForNewRun(CruciballManager cruciball)
        {
            int level = cruciball != null ? cruciball.currentCruciballLevel : 0;
            Counter.Reset(StartingCountFor(level));
            ModLog.Info($"New run (cruciball {level}): rerolls set to {Count}");
        }

        public static void Add(int amount) => Counter.Add(amount);

        /// <summary>For the public API: adds or, if negative, removes rerolls, never going below 0.</summary>
        public static void Adjust(int amount) => Counter.Adjust(amount);

        public static bool TrySpend() => Counter.TrySpend();

        public static void Refund() => Counter.Refund();

        /// <summary>Starting rerolls for a new run: another mod's answer if it gives one, otherwise the usual rule.</summary>
        public static int StartingCountFor(int cruciballLevel)
        {
            return RelicRerollsApi.StartingCount() ?? ConfiguredStartingCount(cruciballLevel);
        }

        /// <summary>
        /// The usual starting count (settings and Cruciball penalty). Continuing a run uses this, never the API's
        /// starting-count providers, which are only for new runs.
        /// </summary>
        private static int ConfiguredStartingCount(int cruciballLevel)
        {
#if DEBUG
            if (Plugin.DevModeActive && Plugin.DevStartingRerolls.Value >= 0)
            {
                return Plugin.DevStartingRerolls.Value;
            }
#endif
            return RerollRules.StartingCount(Plugin.StartingRerolls.Value, Plugin.CruciballPenaltyPerLevel.Value,
                RerollRules.ParsePenaltyLevels(Plugin.CruciballPenaltyLevels.Value), cruciballLevel);
        }

        /// <summary>Called when the game saves the run (and by the API, for modes with their own save flow).</summary>
        public static void Commit()
        {
            if (!RelicRerollsApi.TryCurrentSlot(out string slot))
            {
                return;
            }
            string path = FilePath(slot);
            try
            {
                File.WriteAllText(path, Counter.Snapshot(StaticGameData.currentSeed).Serialize());
                // The pre-rename file only ever belonged to the default slot.
                string legacyPath = Path.Combine(Paths.ConfigPath, Plugin.LegacyStateFile);
                if (IsDefaultSlot(slot) && File.Exists(legacyPath))
                {
                    File.Delete(legacyPath);
                }
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning($"Could not write {path}: {e.Message}");
            }
        }

        /// <summary>Called when the game loads a saved run (after it has restored the run seed), and by the API.</summary>
        public static void Restore()
        {
            if (!RelicRerollsApi.TryCurrentSlot(out string slot))
            {
                return;
            }
            string path = FilePath(slot);
            string saved = null;
            try
            {
                // Fall back to the pre-rename file so a run in progress keeps its rerolls after the rename.
                string legacyPath = Path.Combine(Paths.ConfigPath, Plugin.LegacyStateFile);
                string readPath = StateSlots.ReadLegacyFile(IsDefaultSlot(slot), File.Exists(path)) ? legacyPath : path;
                if (File.Exists(readPath))
                {
                    saved = File.ReadAllText(readPath);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not read {path}: {e.Message}");
            }

            CruciballManager cruciball = Resources.FindObjectsOfTypeAll<CruciballManager>().FirstOrDefault();
            // Continuing a run never asks the API's starting-count providers, only the usual rule.
            int startingCount = ConfiguredStartingCount(cruciball != null ? cruciball.currentCruciballLevel : 0);
            string seed = StaticGameData.currentSeed;
            RerollSnapshot snapshot = RerollSnapshot.ResolveOnContinue(saved, seed, startingCount);
            Counter.Restore(snapshot);

            if (RerollSnapshot.TryParse(saved, out RerollSnapshot parsed) && parsed.Seed == seed)
            {
                ModLog.Info($"Continued run: rerolls restored to {Count}");
            }
            else
            {
                Plugin.Log.LogWarning($"No saved rerolls for this run; using starting count {Count}");
            }
        }

        private static bool IsDefaultSlot(string slot) => StateSlots.FileName(slot) == StateSlots.DefaultFileName;
    }
}
