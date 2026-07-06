using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tower.UI
{
    public sealed class SceneSequenceManager : MonoBehaviour
    {
        private static SceneSequenceManager instance;
        public static SceneSequenceManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("SceneSequenceManager");
                    instance = go.AddComponent<SceneSequenceManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        private Canvas faderCanvas;
        private Image faderImage;
        private bool isTransitioning;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            CreateFaderCanvas();
        }

        private void CreateFaderCanvas()
        {
            var go = new GameObject("Fader Canvas");
            go.transform.SetParent(transform, false);
            faderCanvas = go.AddComponent<Canvas>();
            faderCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            faderCanvas.sortingOrder = 9999;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            go.AddComponent<CanvasResolutionScaler>();

            var imgGo = new GameObject("Fader Image");
            imgGo.transform.SetParent(go.transform, false);
            faderImage = imgGo.AddComponent<Image>();
            faderImage.color = new Color(0, 0, 0, 0);

            var rect = faderImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public void LoadSceneWithSequence(string sceneName)
        {
            if (isTransitioning) return;
            StartCoroutine(SequenceCoroutine(sceneName));
        }

        private IEnumerator SequenceCoroutine(string sceneName)
        {
            isTransitioning = true;
            faderCanvas.gameObject.SetActive(true);

            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.unscaledDeltaTime;
                faderImage.color = new Color(0, 0, 0, Mathf.Min(t / 0.4f, 1f));
                yield return null;
            }
            faderImage.color = Color.black;

            var op = SceneManager.LoadSceneAsync(sceneName);
            while (op != null && !op.isDone)
            {
                yield return null;
            }

            yield return new WaitForEndOfFrame();

            t = 0f;
            while (t < 0.4f)
            {
                t += Time.unscaledDeltaTime;
                faderImage.color = new Color(0, 0, 0, Mathf.Max(1f - (t / 0.4f), 0f));
                yield return null;
            }
            faderImage.color = new Color(0, 0, 0, 0);

            faderCanvas.gameObject.SetActive(false);
            isTransitioning = false;
        }
    }
}
