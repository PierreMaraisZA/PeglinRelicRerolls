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

        private static string FilePath => Path.Combine(Paths.ConfigPath, "PeglinRelicRerolls.state");

        public static int Count => Counter.Count;

        public static int Used => Counter.Used;

        public static void ResetForNewRun(CruciballManager cruciball)
        {
            int level = cruciball != null ? cruciball.currentCruciballLevel : 0;
            Counter.Reset(StartingCountFor(level));
            ModLog.Info($"New run (cruciball {level}): rerolls set to {Count}");
        }

        public static void Add(int amount) => Counter.Add(amount);

        public static bool TrySpend() => Counter.TrySpend();

        public static void Refund() => Counter.Refund();

        public static int StartingCountFor(int cruciballLevel)
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

        /// <summary>Called when the game saves the run.</summary>
        public static void Commit()
        {
            try
            {
                File.WriteAllText(FilePath, Counter.Snapshot(StaticGameData.currentSeed).Serialize());
                string legacyPath = Path.Combine(Paths.ConfigPath, Plugin.LegacyStateFile);
                if (File.Exists(legacyPath))
                {
                    File.Delete(legacyPath);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not write {FilePath}: {e.Message}");
            }
        }

        /// <summary>Called when the game loads a saved run (after it has restored the run seed).</summary>
        public static void Restore()
        {
            string saved = null;
            try
            {
                // Fall back to the pre-rename file so a run in progress keeps its rerolls after the rename.
                string legacyPath = Path.Combine(Paths.ConfigPath, Plugin.LegacyStateFile);
                string path = File.Exists(FilePath) ? FilePath : legacyPath;
                if (File.Exists(path))
                {
                    saved = File.ReadAllText(path);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not read {FilePath}: {e.Message}");
            }

            CruciballManager cruciball = Resources.FindObjectsOfTypeAll<CruciballManager>().FirstOrDefault();
            int startingCount = StartingCountFor(cruciball != null ? cruciball.currentCruciballLevel : 0);
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
    }
}
