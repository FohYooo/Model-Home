using UnityEngine;

namespace ModelHome
{
    /// <summary>Lap 2: a soft inhale from the Daughter figure the first time the guard comes within 2 m.</summary>
    public class ProximityCue : MonoBehaviour
    {
        public LapDirector director;
        public DisplayFigure daughter;
        public Transform player;
        public AudioClip inhale;
        public float radius = 2f;
        LapStaging armedFor;
        LapStaging firedFor;

        void Update()
        {
            if (director == null || daughter == null || player == null || inhale == null) return;
            var lap = director.Current;
            if (!lap.inhaleCueNearDaughter || firedFor == lap || !daughter.gameObject.activeInHierarchy || director.Transitioning) return;
            armedFor = lap;
            var head = daughter.Head ? daughter.Head.position : daughter.transform.position + Vector3.up;
            if (Vector3.Distance(player.position + Vector3.up * 1.6f, head) <= radius)
            {
                AudioSource.PlayClipAtPoint(inhale, head, 0.9f);
                firedFor = armedFor;
            }
        }
    }
}
