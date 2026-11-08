using UnityEngine;

namespace ModelHome
{
    /// <summary>Slow first-person walk (no sprint), mouse look, head bob and footsteps.
    /// Esc releases the cursor and suspends look; clicking the Game view locks it again.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public Transform cameraPivot;
        public float walkSpeed = 1.6f;
        public float mouseSensitivity = 2.0f;
        public float pitchLimit = 80f;
        public float bobAmplitude = 0.025f;
        public float bobFrequency = 1.8f;
        public float stepDistance = 0.55f;
        public AudioClip[] woodSteps;
        public AudioClip[] carpetSteps;
        public AudioSource stepSource;

        public bool InputEnabled { get; set; } = true;
        public bool CursorLocked { get; private set; }

        CharacterController cc;
        float pitch;
        float yVelocity;
        float bobPhase;
        float stepAccumulator;
        Vector3 pivotRest;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (cameraPivot) pivotRest = cameraPivot.localPosition;
        }

        void Start() => SetCursorLocked(true);

        public void SetCursorLocked(bool locked)
        {
            CursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) SetCursorLocked(false);
            else if (!CursorLocked && Input.GetMouseButtonDown(0)) SetCursorLocked(true);

            Vector3 move = Vector3.zero;
            if (InputEnabled)
            {
                if (CursorLocked)
                {
                    transform.Rotate(0f, Input.GetAxis("Mouse X") * mouseSensitivity, 0f);
                    pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * mouseSensitivity, -pitchLimit, pitchLimit);
                    if (cameraPivot) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                }
                var input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
                move = transform.TransformDirection(Vector3.ClampMagnitude(input, 1f)) * walkSpeed;
            }

            yVelocity = cc.isGrounded ? -1f : yVelocity - 9.81f * Time.deltaTime;
            cc.Move((move + Vector3.up * yVelocity) * Time.deltaTime);

            float speed = new Vector3(cc.velocity.x, 0f, cc.velocity.z).magnitude;
            if (cameraPivot)
            {
                bobPhase += Time.deltaTime * bobFrequency * Mathf.PI * 2f * Mathf.Clamp01(speed / walkSpeed);
                float bob = Mathf.Sin(bobPhase) * bobAmplitude * Mathf.Clamp01(speed / walkSpeed);
                cameraPivot.localPosition = pivotRest + Vector3.up * bob;
            }
            stepAccumulator += speed * Time.deltaTime;
            if (stepAccumulator >= stepDistance)
            {
                stepAccumulator = 0f;
                PlayStep();
            }
        }

        void PlayStep()
        {
            if (stepSource == null) return;
            bool carpet = Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out var hit, 0.6f,
                ~0, QueryTriggerInteraction.Ignore) && hit.collider.name.Contains("Rug");
            var set = carpet ? carpetSteps : woodSteps;
            if (set == null || set.Length == 0) return;
            stepSource.pitch = Random.Range(0.94f, 1.04f);
            stepSource.PlayOneShot(set[Random.Range(0, set.Length)], carpet ? 0.5f : 0.7f);
        }
    }
}
