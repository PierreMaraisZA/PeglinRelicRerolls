using System.Collections;
using System.Linq;
using Audio;
using Battle;
using Battle.Attacks;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Audio;

namespace PeglinRelicRerolls
{
    /// <summary>
    /// Purely cosmetic: copies of the game's thrown-bomb prefab play their explosion animation and sound at
    /// random spots on screen when a chest reroll lands a boss relic. The bomb's own landing code is never run,
    /// so no damage, relic effects or "bomb exploded" events reach the game.
    /// </summary>
    internal static class BombCelebration
    {
        private const int BombCount = 14;
        private const float SpreadSeconds = 1.2f;
        // BombLob waits 0.3s after triggering "explode" before the bang (when the game plays the sound),
        // then another 0.35s before destroying itself.
        private const float BangSeconds = 0.3f;
        private const float ExplosionSeconds = 0.65f;
        // Fourteen overlapping bangs at full volume is a lot; keep each one a bit quieter.
        private const float BangVolume = 0.6f;

        private static GameObject _bombPrefab;
        private static AudioClip _bangSound;
        private static AudioMixerGroup _sfxMixer;
        private static AudioSource _audio;
        private static readonly System.Random Random = new System.Random();

        /// <summary>
        /// Remember the bomb prefab and its explosion sound whenever a battle loads, since chests have no
        /// BattleController. The sound comes from the battle's PegAudioManager, along with the mixer channel it
        /// plays through, so the player's SFX volume setting applies.
        /// </summary>
        public static void Remember(GameObject prefab, PegAudioManager audioManager)
        {
            if (prefab != null)
            {
                _bombPrefab = prefab;
            }
            if (audioManager != null)
            {
                Traverse audio = Traverse.Create(audioManager);
                AudioClip clip = audio.Field("_bombExplosionSfx").GetValue<AudioClip>();
                if (clip != null)
                {
                    _bangSound = clip;
                }
                AudioSource sfxSource = audio.Field("_sfxAudioSource").GetValue<AudioSource>();
                if (sfxSource != null && sfxSource.outputAudioMixerGroup != null)
                {
                    _sfxMixer = sfxSource.outputAudioMixerGroup;
                }
            }
        }

        public static void Play()
        {
            GameObject prefab = FindPrefab();
            Camera camera = Camera.main;
            if (prefab == null || camera == null)
            {
                Plugin.Log.LogWarning("Bomb celebration skipped: bomb prefab not loaded yet (fight a battle first).");
                return;
            }
            RerollHud.Ensure();
            RerollHud.RunCoroutine(Celebrate(prefab, camera));
        }

        private static GameObject FindPrefab()
        {
            if (_bombPrefab != null)
            {
                return _bombPrefab;
            }
            // Fallback: any regular (non-rigged) thrown-bomb prefab already loaded in memory.
            BombLob loaded = Resources.FindObjectsOfTypeAll<BombLob>()
                .FirstOrDefault(bomb => !bomb.isRigged && !bomb.gameObject.scene.IsValid());
            return loaded != null ? loaded.gameObject : null;
        }

        private static IEnumerator Celebrate(GameObject prefab, Camera camera)
        {
            int topLayer = SortingLayer.layers.Length > 0 ? SortingLayer.layers[SortingLayer.layers.Length - 1].id : 0;
            float[] delays = Enumerable.Range(0, BombCount)
                .Select(_ => (float)Random.NextDouble() * SpreadSeconds)
                .OrderBy(delay => delay)
                .ToArray();

            float elapsed = 0f;
            foreach (float delay in delays)
            {
                // Realtime: the relic screen may run with the game paused.
                if (delay > elapsed)
                {
                    yield return new WaitForSecondsRealtime(delay - elapsed);
                    elapsed = delay;
                }
                if (camera == null)
                {
                    yield break;
                }
                Vector3 position = camera.ViewportToWorldPoint(new Vector3(
                    0.05f + (float)Random.NextDouble() * 0.9f,
                    0.08f + (float)Random.NextDouble() * 0.84f,
                    0f));
                position.z = 0f;
                SpawnExplosion(prefab, position, topLayer);
            }
        }

        private static void SpawnExplosion(GameObject prefab, Vector3 position, int sortingLayer)
        {
            GameObject bomb = Object.Instantiate(prefab, position, Quaternion.Euler(0f, 0f, (float)Random.NextDouble() * 360f));

            // Strip everything that could interact with the game: no landing callback, no physics, no collisions.
            BombLob lob = bomb.GetComponent<BombLob>();
            if (lob != null)
            {
                lob.OnBombTossComplete = null;
                lob.enabled = false;
            }
            Rigidbody2D body = bomb.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.isKinematic = true;
                body.velocity = Vector2.zero;
                body.simulated = false;
            }
            foreach (Collider2D collider in bomb.GetComponentsInChildren<Collider2D>())
            {
                collider.enabled = false;
            }

            // Draw above the relic screen.
            foreach (Renderer renderer in bomb.GetComponentsInChildren<Renderer>())
            {
                renderer.sortingLayerID = sortingLayer;
                renderer.sortingOrder = 1000;
            }

            Animator animator = bomb.GetComponent<Animator>();
            if (animator != null)
            {
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.SetTrigger("explode");
            }
            RerollHud.RunCoroutine(BangAfter(BangSeconds));
            RerollHud.RunCoroutine(DestroyAfter(bomb, ExplosionSeconds));
        }

        /// <summary>Plays the explosion sound at the same moment in the animation as a real thrown bomb.</summary>
        private static IEnumerator BangAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            AudioClip sound = FindBangSound();
            AudioSource source = AudioPlayer();
            if (sound != null && source != null)
            {
                source.PlayOneShot(sound, BangVolume);
            }
        }

        private static AudioClip FindBangSound()
        {
            if (_bangSound != null)
            {
                return _bangSound;
            }
            // Fallback before any battle this session: the peg-bomb explosion sound, which is loaded on any pegboard.
            PegSfx pegSfx = Resources.FindObjectsOfTypeAll<PegSfx>().FirstOrDefault(sfx => sfx.BombExplosionSfx != null);
            return pegSfx != null ? pegSfx.BombExplosionSfx : null;
        }

        /// <summary>A 2D audio source that survives scene changes and keeps playing if the game is paused.</summary>
        private static AudioSource AudioPlayer()
        {
            if (_audio == null)
            {
                GameObject host = new GameObject("PeglinRelicRerollsAudio");
                Object.DontDestroyOnLoad(host);
                _audio = host.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 0f;
                _audio.ignoreListenerPause = true;
            }
            if (_sfxMixer != null && _audio.outputAudioMixerGroup != _sfxMixer)
            {
                _audio.outputAudioMixerGroup = _sfxMixer;
            }
            return _audio;
        }

        private static IEnumerator DestroyAfter(GameObject target, float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (target != null)
            {
                Object.Destroy(target);
            }
        }
    }

    [HarmonyPatch(typeof(BattleController), "Start")]
    internal static class RememberBombPrefabPatch
    {
        private static void Postfix(BattleController __instance, GameObject ____tossableBombPrefab)
        {
            // PegAudioManager lives on the same object as BattleController.
            BombCelebration.Remember(____tossableBombPrefab, __instance.GetComponent<PegAudioManager>());
        }
    }
}
