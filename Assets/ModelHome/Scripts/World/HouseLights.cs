using System.Collections;
using System.Linq;
using UnityEngine;

namespace ModelHome
{
    /// <summary>The house's practical lights: occasional pendant flicker, and the power cut for the ending.</summary>
    public class HouseLights : MonoBehaviour
    {
        public Light[] lights;
        public Light pendant;
        public bool PoweredDown { get; private set; }
        float nextFlicker;

        void Start() => nextFlicker = Time.time + Random.Range(20f, 45f);

        void Update()
        {
            if (PoweredDown || pendant == null || !pendant.enabled || Time.time < nextFlicker) return;
            nextFlicker = Time.time + Random.Range(20f, 45f);
            StartCoroutine(Flicker(pendant, 0.4f));
        }

        public IEnumerator Flicker(Light l, float seconds)
        {
            float end = Time.time + seconds;
            float baseIntensity = l.intensity;
            while (Time.time < end)
            {
                l.intensity = baseIntensity * Random.Range(0.05f, 1f);
                yield return new WaitForSeconds(Random.Range(0.02f, 0.08f));
            }
            l.intensity = baseIntensity;
        }

        public void PowerCut()
        {
            PoweredDown = true;
            foreach (var l in lights.Where(l => l != null)) l.enabled = false;
            foreach (var sw in FindObjectsByType<LampSwitch>(FindObjectsSortMode.None)) sw.PoweredDown = true;
        }
    }
}
