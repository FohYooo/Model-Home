using UnityEngine;

namespace ModelHome
{
    /// <summary>Looks for an Interactable under the crosshair (2 m), shows its prompt, uses it on E.</summary>
    public class PlayerInteractor : MonoBehaviour
    {
        public Hud hud;
        public PlayerController player;
        public float reach = 2.0f;

        Interactable current;

        void Update()
        {
            current = null;
            if (player == null || player.InputEnabled)
            {
                if (Physics.Raycast(transform.position, transform.forward, out var hit, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                {
                    var it = hit.collider.GetComponentInParent<Interactable>();
                    if (it != null && it.Enabled) current = it;
                }
            }
            if (hud) hud.SetPrompt(current ? "E — " + current.Prompt : null);
            if (current && Input.GetKeyDown(KeyCode.E)) current.Interact();
        }
    }
}
