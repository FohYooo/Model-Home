using System.Collections;
using UnityEngine;

namespace ModelHome
{
    /// <summary>
    /// Lap 3 turned in: the power dies, the torch sputters, and the Daughter is always 0.6 m behind the guard
    /// (re-placed every frame she is unseen). Once she has been on screen for 0.8 s she blinks; at full lid
    /// closure the screen cuts to black, 2 s of silence, title card. Fallback: if she is not seen within 20 s,
    /// her breathing plays behind the guard and the cut happens 3 s later anyway.
    /// </summary>
    public class EndingSequence : MonoBehaviour
    {
        public LapDirector director;
        public DisplayFigure daughter;
        public Flashlight torch;
        public HouseLights houseLights;
        public Hud hud;
        public PlayerController player;
        public AudioClip drone;
        public AudioClip breath;

        [Tooltip("Seconds the Daughter must be on screen before she blinks and the screen cuts (spec: 0.8)")]
        public float seenToBlink = 0.8f;
        public const float FallbackAfter = 20f;
        public bool Finished { get; private set; }
        bool running;

        void Start()
        {
            if (director) director.EndingRequested += Play;
        }

        public void Play()
        {
            if (running) return;
            running = true;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            houseLights.PowerCut();
            var droneSource = gameObject.AddComponent<AudioSource>();
            droneSource.clip = drone;
            droneSource.spatialBlend = 0f;
            droneSource.volume = 0f;
            droneSource.Play();
            StartCoroutine(Ramp(droneSource, 0.6f, 8f));
            StartCoroutine(torch.Sputter(2f, true));

            daughter.gameObject.SetActive(true);
            daughter.EyeTracking = true;
            daughter.BlinkOnGlanceAway = false;
            daughter.Breathing = false;
            daughter.ApplyPose();

            var cam = Camera.main;
            float start = Time.time;
            float seenFor = 0f;
            float fallbackCut = float.PositiveInfinity;
            while (true)
            {
                if (daughter.IsSeenBy(cam))
                {
                    seenFor += Time.deltaTime;
                    if (seenFor >= seenToBlink) break;
                }
                else
                {
                    seenFor = 0f;
                    PlaceBehind(cam);
                }
                if (float.IsPositiveInfinity(fallbackCut) && Time.time - start > FallbackAfter)
                {
                    var src = daughter.Head ? daughter.Head.position : daughter.transform.position + Vector3.up * 1.2f;
                    AudioSource.PlayClipAtPoint(breath, src, 1f);
                    fallbackCut = Time.time + 3f;
                }
                if (Time.time >= fallbackCut) break;
                yield return null;
            }

            bool cut = false;
            yield return daughter.Blink(0.12f, () => { Cut(droneSource); cut = true; });
            if (!cut) Cut(droneSource);
            yield return new WaitForSeconds(2f);
            yield return Title();
            Finished = true;
        }

        void PlaceBehind(Camera cam)
        {
            var feet = player.transform.position;
            var candidates = EndingPlacement.Candidates(feet, cam.transform.forward);
            var chosen = candidates[candidates.Count - 1];
            bool clear = false;
            foreach (var c in candidates)
            {
                if (Physics.CheckCapsule(c + Vector3.up * 0.3f, c + Vector3.up * 1.1f, 0.25f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                chosen = c;
                clear = true;
                break;
            }
            if (!clear && !warned)
            {
                Debug.LogWarning("[ModelHome] no clear spot behind the guard; using the last candidate");
                warned = true;
            }
            daughter.PlaceFacing(chosen, cam.transform.position);
        }

        bool warned;

        void Cut(AudioSource droneSource)
        {
            if (hud && hud.Fade) hud.Fade.alpha = 1f;
            if (player) player.InputEnabled = false;
            droneSource.Stop();
            AudioListener.volume = 0f;
        }

        IEnumerator Title()
        {
            AudioListener.volume = 1f;
            foreach (var a in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (a.clip != null && a.clip.name == "hum_roomtone") a.volume = 0.08f;
                else a.Stop();
            if (hud == null || hud.Clock == null) yield break;
            var t = hud.Clock;
            t.text = "MODEL HOME";
            t.fontSize = 64;
            t.enabled = true;
            var c = t.color;
            for (float x = 0; x < 2f; x += Time.deltaTime)
            {
                t.color = new Color(c.r, c.g, c.b, x / 2f);
                yield return null;
            }
            t.color = new Color(c.r, c.g, c.b, 1f);
        }

        static IEnumerator Ramp(AudioSource a, float to, float seconds)
        {
            float from = a.volume;
            for (float x = 0; x < seconds && a != null && a.isPlaying; x += Time.deltaTime)
            {
                a.volume = Mathf.Lerp(from, to, x / seconds);
                yield return null;
            }
        }

        void Update()
        {
            if (!Finished || !Input.GetKeyDown(KeyCode.Escape)) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
