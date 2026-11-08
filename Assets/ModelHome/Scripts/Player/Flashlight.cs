using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace ModelHome
{
    /// <summary>The guard's torch: a shadowed spot light on the camera. F toggles; Sputter flickers it.</summary>
    [RequireComponent(typeof(Light))]
    public class Flashlight : MonoBehaviour
    {
        public AudioClip clickClip;
        public PlayerController player;
        [Tooltip("Layers that block the beam for IsLit checks")]
        public LayerMask occluders = Physics.DefaultRaycastLayers; // figures sit on Ignore Raycast

        Light spot;
        AudioSource source;
        bool sputtering;

        public bool IsOn => spot != null && spot.enabled;
        /// <summary>When set (the ending), F cannot switch the torch.</summary>
        public bool Locked { get; set; }

        void Awake()
        {
            Configure();
            source = GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        /// <summary>Applies the torch's light settings; also called by the scene builder so edit-mode previews match.</summary>
        public void Configure()
        {
            spot = GetComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 18f;
            spot.spotAngle = 55f;
            spot.innerSpotAngle = 30f;
            spot.intensity = 2.2f; // inverse-square: 6 blew faces out to white at 1 m
            spot.color = new Color(1f, 0.957f, 0.878f);
            spot.shadows = LightShadows.Soft;
            spot.shadowStrength = 0.9f;
            spot.renderMode = LightRenderMode.ForcePixel;
        }

        void Update()
        {
            if (sputtering || (player != null && !player.InputEnabled)) return;
            if (Input.GetKeyDown(KeyCode.F)) Toggle();
        }

        public void Toggle()
        {
            if (Locked) return;
            spot.enabled = !spot.enabled;
            if (clickClip) source.PlayOneShot(clickClip, 0.6f);
        }

        public void SetOn(bool on) => spot.enabled = on;

        /// <summary>Flickers the torch for `seconds`, then leaves it on or off.</summary>
        public IEnumerator Sputter(float seconds, bool endOn)
        {
            sputtering = true;
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                spot.enabled = !spot.enabled;
                yield return new WaitForSeconds(spot.enabled ? Random.Range(0.03f, 0.12f) : Random.Range(0.06f, 0.25f));
            }
            spot.enabled = endOn;
            sputtering = false;
        }

        /// <summary>True when the torch is on and its unobstructed beam reaches the point.</summary>
        public bool IsLit(Vector3 point)
        {
            if (!IsOn) return false;
            var origin = transform.position;
            if (!FlashlightMath.IsInCone(origin, transform.forward, spot.spotAngle * 0.5f, spot.range, point)) return false;
            var dir = point - origin;
            return !Physics.Raycast(origin, dir.normalized, dir.magnitude - 0.05f, occluders, QueryTriggerInteraction.Ignore);
        }
    }
}
