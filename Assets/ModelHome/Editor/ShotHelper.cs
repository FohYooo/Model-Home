using UnityEngine;

namespace ModelHome.EditorTools
{
    /// <summary>Verification helper: points the player camera at a figure or a point for screenshots.</summary>
    public static class ShotHelper
    {
        public static string FrameFigure(string variant, float distance, float side = 0f, float fov = 50f, float headOffset = 0f)
        {
            var go = GameObject.Find($"Figure_{variant}");
            if (go == null) return $"no Figure_{variant}";
            var fig = go.GetComponent<DisplayFigure>();
            fig.Init();
            var target = fig.Head.position + Vector3.up * headOffset;
            var face = fig.FaceForward;
            face.y = 0;
            if (face.sqrMagnitude < 1e-4f) face = Vector3.forward; // lying figure: approach from +Z
            face.Normalize();
            var from = target + face * distance + Vector3.Cross(Vector3.up, face) * side;
            return FramePoint(new Vector3(from.x, 0f, from.z), target, fov);
        }

        public static string FramePoint(Vector3 feet, Vector3 target, float fov = 50f)
        {
            var pc = Object.FindAnyObjectByType<PlayerController>();
            var cc = pc.GetComponent<CharacterController>();
            bool was = cc.enabled;
            cc.enabled = false;
            pc.transform.position = feet;
            var flat = target - feet;
            flat.y = 0;
            pc.transform.rotation = Quaternion.LookRotation(flat.normalized);
            cc.enabled = was;
            var eye = feet + Vector3.up * 1.62f;
            float pitch = -Mathf.Atan2(target.y - eye.y, flat.magnitude) * Mathf.Rad2Deg;
            pc.cameraPivot.localRotation = Quaternion.Euler(pitch, 0, 0);
            pc.cameraPivot.GetComponent<Camera>().fieldOfView = fov;
            if (Application.isPlaying) pc.InputEnabled = false;
            return $"camera at {eye} looking at {target}";
        }
    }
}
