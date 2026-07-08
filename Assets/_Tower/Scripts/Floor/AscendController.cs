using System;
using System.Collections;
using UnityEngine;

namespace Tower.Floor
{
    // The "수직 체감 = Ascend 의식" transition (39 Shape of Dreams reference): reaching
    // the exit node physically raises the party (and, optionally, the camera). v0 is a
    // placeholder tween parameterised by height/duration/ease; the real ritual VFX is
    // deferred. Deterministic in shape (curve-driven), engine-side only.
    public sealed class AscendController : MonoBehaviour
    {
        [SerializeField] private float ascendHeight = 12f;
        [SerializeField] private float ascendDuration = 2.5f;
        [SerializeField] private AnimationCurve ascendEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public bool IsAscending { get; private set; }

        public float AscendHeight { get => ascendHeight; set => ascendHeight = value; }
        public float AscendDuration { get => ascendDuration; set => ascendDuration = Mathf.Max(0.01f, value); }
        public AnimationCurve AscendEase { get => ascendEase; set => ascendEase = value; }

        // Raise the party root (and optional camera) by ascendHeight over ascendDuration.
        public void Play(Transform partyRoot, Transform cameraRoot = null, Action onComplete = null)
        {
            if (partyRoot == null || IsAscending) return;
            StartCoroutine(Rise(partyRoot, cameraRoot, onComplete));
        }

        private IEnumerator Rise(Transform partyRoot, Transform cameraRoot, Action onComplete)
        {
            IsAscending = true;
            Vector3 partyStart = partyRoot.position;
            Vector3 camStart = cameraRoot != null ? cameraRoot.position : Vector3.zero;
            float duration = Mathf.Max(0.01f, ascendDuration);
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float k = ascendEase.Evaluate(Mathf.Clamp01(t / duration));
                float dy = ascendHeight * k;
                partyRoot.position = partyStart + Vector3.up * dy;
                if (cameraRoot != null) cameraRoot.position = camStart + Vector3.up * dy;
                yield return null;
            }

            partyRoot.position = partyStart + Vector3.up * ascendHeight;
            if (cameraRoot != null) cameraRoot.position = camStart + Vector3.up * ascendHeight;
            IsAscending = false;
            onComplete?.Invoke();
        }
    }
}
