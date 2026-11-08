using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ModelHome
{
    /// <summary>
    /// One display figure. Micro-life happens while watched (eyes follow the torch with a lag, breathing,
    /// a blink when you glance away); proportions (neck, smile, head tilt) and placement are set by the
    /// LapDirector only while the screen is black, or by the ending only while the figure is unseen.
    /// </summary>
    [DisallowMultipleComponent]
    public class DisplayFigure : MonoBehaviour
    {
        public FigureId Id;
        public string Variant;
        public bool EyeTracking;
        public bool Breathing;
        public bool BlinkOnGlanceAway;
        public float HeadTiltDeg;
        [Range(0, 1)] public float NeckLong01;
        [Range(0, 1)] public float SmileWide01;

        public const float NeckLift = 0.08f;      // metres at NeckLong = 1 (Blender contract)
        const float GazeLimit = 30f;              // degrees from rest
        const float GazeLag = 0.35f;              // seconds (exponential smoothing time)
        const float TrackRange = 4f;              // metres: tracks the player this close even without the torch
        const float BreathHz = 0.22f;

        public Transform Head { get; private set; }
        Transform eyeL, eyeR;
        SkinnedMeshRenderer headSmr, lidL, lidR;
        readonly List<SkinnedMeshRenderer> bodies = new List<SkinnedMeshRenderer>();
        Renderer seenRenderer;

        Vector3 headRestLocalPos;
        Quaternion headRestLocalRot;
        Vector3 neckAxisLocal;
        Vector3 faceInHead;                        // head-local face direction (eyes at rest)
        Quaternion eyeLRestRel, eyeRRestRel;        // eye rotation relative to the head
        Vector3 gazeLInHead, gazeRInHead;           // current (smoothed) gaze, head-local
        bool initialised, hasHead, hasEyes, hasLids;
        float breathPhase;
        GlanceAwayDetector glance;
        Flashlight torch;
        bool blinking;

        /// <summary>Face direction now (follows head tilt), world space.</summary>
        public Vector3 FaceForward => hasHead ? Head.rotation * faceInHead : Facing.Of(transform);
        /// <summary>Face direction with no head tilt, world space.</summary>
        public Vector3 RestFaceForward => hasHead ? transform.rotation * (headRestLocalRot * faceInHead) : Facing.Of(transform);

        void Awake() => Init();

        public void Init()
        {
            if (initialised) return;
            initialised = true;
            var all = GetComponentsInChildren<Transform>(true);
            Transform Find(string n) => all.FirstOrDefault(t => t.name == n);
            Head = Find("Head");
            eyeL = Find("Eye_L");
            eyeR = Find("Eye_R");
            headSmr = Head ? Head.GetComponent<SkinnedMeshRenderer>() : null;
            lidL = Find("Lid_L")?.GetComponent<SkinnedMeshRenderer>();
            lidR = Find("Lid_R")?.GetComponent<SkinnedMeshRenderer>();
            foreach (var n in new[] { "Body", "Clothes" })
            {
                var smr = Find(n)?.GetComponent<SkinnedMeshRenderer>();
                if (smr) bodies.Add(smr);
            }

            var missing = new List<string>();
            if (!Head) missing.Add("Head");
            if (!eyeL) missing.Add("Eye_L");
            if (!eyeR) missing.Add("Eye_R");
            if (!lidL) missing.Add("Lid_L");
            if (!lidR) missing.Add("Lid_R");
            if (bodies.Count == 0) missing.Add("Body");
            if (missing.Count > 0)
                Debug.LogError($"[ModelHome] {name}: missing {string.Join(", ", missing)} - those features are disabled");

            hasHead = Head != null;
            hasEyes = hasHead && eyeL && eyeR;
            hasLids = lidL && lidR;
            seenRenderer = headSmr != null ? headSmr : (Renderer)bodies.FirstOrDefault();
            if (hasHead)
            {
                headRestLocalPos = Head.localPosition;
                headRestLocalRot = Head.localRotation;
                neckAxisLocal = transform.InverseTransformDirection(Vector3.up);
                var faceWorld = hasEyes ? -eyeL.up : Facing.Of(transform); // iris looks along the eye's local -Y
                faceInHead = Quaternion.Inverse(Head.rotation) * faceWorld;
            }
            if (hasEyes)
            {
                eyeLRestRel = Quaternion.Inverse(Head.rotation) * eyeL.rotation;
                eyeRRestRel = Quaternion.Inverse(Head.rotation) * eyeR.rotation;
                gazeLInHead = gazeRInHead = faceInHead;
            }
            breathPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            glance = new GlanceAwayDetector();
        }

        void Start() => torch = FindAnyObjectByType<Flashlight>();

        /// <summary>Applies the static pose: head tilt, neck length, smile.</summary>
        public void ApplyPose()
        {
            if (!initialised) Init();
            if (hasHead)
            {
                Head.localPosition = headRestLocalPos + neckAxisLocal * (NeckLift * NeckLong01);
                Head.localRotation = headRestLocalRot * Quaternion.AngleAxis(HeadTiltDeg, faceInHead);
            }
            foreach (var b in bodies) SetShape(b, "NeckLong", 100f * NeckLong01);
            SetShape(headSmr, "SmileWide", 100f * SmileWide01);
        }

        void LateUpdate()
        {
            if (!Application.isPlaying) return;
            ApplyPose();
            float t = Time.time;
            foreach (var b in bodies)
                SetShape(b, "Breathe", Breathing ? 50f + 50f * Mathf.Sin(2f * Mathf.PI * BreathHz * t + breathPhase) : 0f);

            var cam = Camera.main;
            if (cam == null) return;
            if (hasEyes) UpdateGaze(cam);
            if (BlinkOnGlanceAway && hasLids && !blinking && hasHead)
            {
                bool onScreen = IsSeenBy(cam);
                float angle = Vector3.Angle(cam.transform.forward, Head.position - cam.transform.position);
                if (glance.Update(onScreen, angle, t)) StartCoroutine(Blink());
            }
        }

        void UpdateGaze(Camera cam)
        {
            var target = cam.transform.position;
            bool active = EyeTracking && (Vector3.Distance(target, Head.position) < TrackRange ||
                                          (torch != null && torch.IsLit(Head.position)));
            if (active)
            {
                float k = 1f - Mathf.Exp(-Time.deltaTime / GazeLag);
                var restWorld = Head.rotation * faceInHead;
                gazeLInHead = StepGaze(eyeL, restWorld, target, gazeLInHead, k);
                gazeRInHead = StepGaze(eyeR, restWorld, target, gazeRInHead, k);
            }
            AimEye(eyeL, eyeLRestRel, gazeLInHead);
            AimEye(eyeR, eyeRRestRel, gazeRInHead);
        }

        Vector3 StepGaze(Transform eye, Vector3 restWorld, Vector3 target, Vector3 currentInHead, float k)
        {
            var desired = GazeMath.ClampToCone(restWorld, target - eye.position, GazeLimit);
            var desiredInHead = Quaternion.Inverse(Head.rotation) * desired;
            return Vector3.Slerp(currentInHead, desiredInHead, k).normalized;
        }

        void AimEye(Transform eye, Quaternion restRel, Vector3 gazeInHead)
        {
            var restWorld = Head.rotation * faceInHead;
            var gazeWorld = Head.rotation * gazeInHead;
            eye.rotation = Quaternion.FromToRotation(restWorld, gazeWorld) * Head.rotation * restRel;
        }

        /// <summary>Closes the lids over closeSeconds, calls onClosed at full closure, then reopens.</summary>
        public IEnumerator Blink(float closeSeconds = 0.12f, Action onClosed = null)
        {
            if (!hasLids) { onClosed?.Invoke(); yield break; }
            blinking = true;
            for (float t = 0; t < closeSeconds; t += Time.deltaTime) { SetLids(100f * t / closeSeconds); yield return null; }
            SetLids(100f);
            onClosed?.Invoke();
            yield return new WaitForSeconds(0.06f);
            for (float t = 0; t < closeSeconds * 1.5f; t += Time.deltaTime) { SetLids(100f * (1f - t / (closeSeconds * 1.5f))); yield return null; }
            SetLids(0f);
            blinking = false;
        }

        void SetLids(float w)
        {
            SetShape(lidL, "Blink", w);
            SetShape(lidR, "Blink", w);
        }

        public void PlaceAt(Transform anchor) => transform.SetPositionAndRotation(anchor.position, anchor.rotation);

        /// <summary>Stands the figure at feet and turns it about world Y so its face looks toward lookTarget.</summary>
        public void PlaceFacing(Vector3 feet, Vector3 lookTarget)
        {
            if (!initialised) Init();
            transform.position = feet;
            var want = lookTarget - feet;
            want.y = 0f;
            var face = RestFaceForward;
            face.y = 0f;
            if (want.sqrMagnitude < 1e-6f || face.sqrMagnitude < 1e-6f) return;
            // yaw only: FromToRotation picks an arbitrary axis for a 180 deg turn and can flip the figure over
            float yaw = Vector3.SignedAngle(face, want, Vector3.up);
            transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * transform.rotation;
        }

        public bool IsSeenBy(Camera cam) => isActiveAndEnabled && Visibility.IsSeen(seenRenderer, cam);

        static void SetShape(SkinnedMeshRenderer smr, string shape, float weight)
        {
            if (smr == null || smr.sharedMesh == null) return;
            int i = smr.sharedMesh.GetBlendShapeIndex(shape);
            if (i >= 0) smr.SetBlendShapeWeight(i, weight);
        }
    }
}
