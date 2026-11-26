using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Kesselwerk
{
    /// <summary>
    /// Spielt die Animationen eines importierten Monster-Modells ab, ohne dass ein
    /// Animator Controller angelegt werden muss (nutzt die Playables-API).
    /// </summary>
    public class MonsterAnimator : MonoBehaviour
    {
        PlayableGraph graph;
        AnimationPlayableOutput output;
        AnimationClipPlayable playable;
        AnimationClip currentClip;
        AnimationClip[] clips;
        bool loop;

        public static readonly string[] IdleKeys = { "Idle", "Flying" };
        public static readonly string[] AttackKeys = { "Bite_Front", "Attack", "Bite_InPlace" };
        public static readonly string[] HitKeys = { "HitRecieve", "HitReceive", "Hit" };
        public static readonly string[] DeathKeys = { "Death" };

        public void Init(Animator animator, AnimationClip[] allClips)
        {
            clips = allClips ?? new AnimationClip[0];
            graph = PlayableGraph.Create(name + "_Anim");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            output = AnimationPlayableOutput.Create(graph, "Anim", animator);
            graph.Play();
        }

        /// <summary>Sucht einen Clip, dessen Name auf einen der Schlüssel endet (z. B. "MonsterArmature|Idle").</summary>
        public AnimationClip Find(string[] keys)
        {
            foreach (var key in keys)
            {
                foreach (var c in clips)
                {
                    if (c == null || c.name.StartsWith("__preview__")) continue;
                    string n = c.name;
                    int bar = n.LastIndexOf('|');
                    string shortName = bar >= 0 ? n.Substring(bar + 1) : n;
                    if (shortName.EndsWith(key, StringComparison.OrdinalIgnoreCase))
                        return c;
                }
            }
            return null;
        }

        public bool Has(string[] keys) => Find(keys) != null;

        /// <summary>Spielt einen Clip ab und gibt seine Länge zurück (0, wenn keiner gefunden wurde).</summary>
        public float Play(string[] keys, bool loopClip)
        {
            var clip = Find(keys);
            if (clip == null || !graph.IsValid()) return 0f;

            if (playable.IsValid()) playable.Destroy();
            playable = AnimationClipPlayable.Create(graph, clip);
            output.SetSourcePlayable(playable);
            currentClip = clip;
            loop = loopClip;
            return clip.length;
        }

        public void PlayIdle() => Play(IdleKeys, true);

        void Update()
        {
            if (!playable.IsValid() || currentClip == null) return;
            double t = playable.GetTime();
            if (t < currentClip.length) return;

            if (loop)
            {
                playable.SetTime(t % currentClip.length);
            }
            else
            {
                // Letztes Bild halten
                playable.SetTime(currentClip.length);
                playable.SetSpeed(0);
            }
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
