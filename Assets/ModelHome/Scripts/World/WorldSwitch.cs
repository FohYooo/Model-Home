using UnityEngine;

namespace ModelHome
{
    /// <summary>Shared plumbing for world objects driven by an Interactable.</summary>
    [RequireComponent(typeof(Interactable))]
    public abstract class WorldSwitch : MonoBehaviour
    {
        public AudioClip clip;
        public Hud hud;
        protected Interactable it;
        AudioSource source;

        protected virtual void Awake()
        {
            it = GetComponent<Interactable>();
            it.Interacted += _ => OnUse();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 1f;
            source.maxDistance = 12f;
        }

        protected abstract void OnUse();

        protected void Play(float volume = 0.8f)
        {
            if (clip) source.PlayOneShot(clip, volume);
        }

        protected void Say(string text)
        {
            if (hud) hud.Toast(text, 1.6f);
        }
    }
}
