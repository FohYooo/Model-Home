using UnityEngine;

namespace ModelHome
{
    /// <summary>Shared plumbing for world objects driven by an Interactable.</summary>
    [RequireComponent(typeof(Interactable))]
    public abstract class WorldSwitch : MonoBehaviour
    {
        public AudioClip clip;
        public Hud hud;
        Interactable cachedIt;
        AudioSource source;

        /// <summary>Lazy so edit-mode calls (the scene builder applying Lap 1) work before Awake.</summary>
        protected Interactable it
        {
            get
            {
                if (cachedIt == null) cachedIt = GetComponent<Interactable>();
                return cachedIt;
            }
        }

        protected virtual void Awake()
        {
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
