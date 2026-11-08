using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ModelHome
{
    /// <summary>Screen overlay: interaction prompt, short toasts, the clipboard, the fade and the clock.</summary>
    public class Hud : MonoBehaviour
    {
        public Text prompt;
        public Text toast;
        public CanvasGroup Fade;
        public Text Clock;
        public GameObject Clipboard;
        public Text ClipboardText;

        Coroutine toastRoutine;

        public void SetPrompt(string text)
        {
            if (prompt == null) return;
            prompt.text = text ?? "";
            prompt.enabled = !string.IsNullOrEmpty(text);
        }

        public void Toast(string text, float seconds = 2.5f)
        {
            if (toast == null) return;
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ShowToast(text, seconds));
        }

        IEnumerator ShowToast(string text, float seconds)
        {
            toast.text = text;
            toast.enabled = true;
            yield return new WaitForSeconds(seconds);
            toast.enabled = false;
        }

        public void ShowClipboard(bool show)
        {
            if (Clipboard) Clipboard.SetActive(show);
        }

        public IEnumerator FadeTo(float alpha, float seconds)
        {
            if (Fade == null) yield break;
            float start = Fade.alpha;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                Fade.alpha = Mathf.Lerp(start, alpha, t / seconds);
                yield return null;
            }
            Fade.alpha = alpha;
        }
    }
}
