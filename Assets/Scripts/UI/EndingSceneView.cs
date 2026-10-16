using System.Collections.Generic;
using TMPro;
using Intersection.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    public sealed class EndingSceneContent
    {
        public string title;
        public string resultHeading;
        public string requestLabel;
        public string request;
        public string counts;
        public string deletedResult;
        public string deletedUnavailable;
        public string deletedFinal;
        public string preservedResult;
        public string later;
        public string auditHeading;
        public string auditBody;
        public string preservedFinal;
        public string complete;
    }

    /// <summary>
    /// 업무 프로그램을 완전히 덮고 처리 결과의 후일을 보여주는 엔딩 연출.
    /// 삭제 엔딩은 답할 기록이 사라진 결과를, 보존 엔딩은 뒤늦은 감사와 해고를 서로 다른 박자로 보여준다.
    /// </summary>
    public class EndingSceneView : MonoBehaviour
    {
        readonly List<CanvasGroup> beats = new List<CanvasGroup>();
        bool preserved;
        bool animate;
        float shownAt;

        public static EndingSceneView Create(Canvas canvas, UITheme theme, bool preserved, bool playSequence,
            EndingSceneContent content)
        {
            var root = new GameObject("EndingSequence", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            root.transform.SetParent(canvas.transform, false);
            root.transform.SetAsLastSibling();
            Stretch((RectTransform)root.transform);

            var background = root.GetComponent<Image>();
            background.color = Color.Lerp(theme.stage, Color.black, 0.62f);
            background.raycastTarget = true;

            var rootGroup = root.GetComponent<CanvasGroup>();
            rootGroup.alpha = 1f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = true;

            var view = root.AddComponent<EndingSceneView>();
            view.preserved = preserved;
            view.animate = playSequence;
            view.shownAt = Time.unscaledTime;

            view.BuildResultBeat(root.transform, theme, preserved, content);
            if (preserved)
            {
                view.BuildTimeBeat(root.transform, theme, content);
                view.BuildAuditBeat(root.transform, theme, content);
            }
            else
            {
                view.BuildInquiryBeat(root.transform, theme, content);
            }
            view.BuildFinalBeat(root.transform, theme, preserved, content);

            for (int i = 0; i < view.beats.Count; i++)
                view.beats[i].alpha = playSequence ? 0f : (i == view.beats.Count - 1 ? 1f : 0f);
            view.Tick();
            return view;
        }

        public void Tick()
        {
            if (!animate || beats.Count == 0)
                return;

            float elapsed = Time.unscaledTime - shownAt;
            if (preserved)
            {
                SetBeat(0, Window(elapsed, 0f, 2.6f));
                SetBeat(1, Window(elapsed, 2.6f, 4.2f));
                SetBeat(2, Window(elapsed, 4.2f, 8.1f));
                SetBeat(3, FadeIn(elapsed, 8.1f));
            }
            else
            {
                SetBeat(0, Window(elapsed, 0f, 2.6f));
                SetBeat(1, Window(elapsed, 2.6f, 6.6f));
                SetBeat(2, FadeIn(elapsed, 6.6f));
            }
        }

        void BuildResultBeat(Transform root, UITheme theme, bool isPreserved, EndingSceneContent content)
        {
            var beat = AddBeat(root, "ProcessingResult");
            AddText(beat.transform, "Heading", content.resultHeading, theme.mediumFont, 15f, theme.accent,
                TextAlignmentOptions.Center, new Vector2(0.18f, 0.68f), new Vector2(0.82f, 0.75f));
            AddText(beat.transform, "Result", isPreserved ? content.preservedResult : content.deletedResult,
                theme.mediumFont, 28f, theme.text, TextAlignmentOptions.Center,
                new Vector2(0.14f, 0.45f), new Vector2(0.86f, 0.63f));
            AddText(beat.transform, "Counts", content.counts, theme.regularFont, 18f, theme.subText,
                TextAlignmentOptions.Center, new Vector2(0.2f, 0.36f), new Vector2(0.8f, 0.43f));
        }

        void BuildInquiryBeat(Transform root, UITheme theme, EndingSceneContent content)
        {
            var beat = AddBeat(root, "UnansweredInquiry");
            AddText(beat.transform, "Label", content.requestLabel, theme.mediumFont, 14f, theme.accent,
                TextAlignmentOptions.Center, new Vector2(0.16f, 0.67f), new Vector2(0.84f, 0.73f));
            AddText(beat.transform, "Request", content.request, theme.regularFont, 25f, theme.text,
                TextAlignmentOptions.Center, new Vector2(0.14f, 0.46f), new Vector2(0.86f, 0.64f));
            AddLine(beat.transform, theme, new Vector2(0.35f, 0.405f), new Vector2(0.65f, 0.407f));
            AddText(beat.transform, "Unavailable", content.deletedUnavailable, theme.regularFont, 18f, theme.subText,
                TextAlignmentOptions.Center, new Vector2(0.18f, 0.29f), new Vector2(0.82f, 0.37f));
        }

        void BuildTimeBeat(Transform root, UITheme theme, EndingSceneContent content)
        {
            var beat = AddBeat(root, "TimePasses");
            AddText(beat.transform, "Later", content.later, theme.mediumFont, 24f, theme.text,
                TextAlignmentOptions.Center, new Vector2(0.2f, 0.44f), new Vector2(0.8f, 0.56f));
        }

        void BuildAuditBeat(Transform root, UITheme theme, EndingSceneContent content)
        {
            var beat = AddBeat(root, "AuditNotice");
            var panel = AddRect(beat.transform, "Notice", new Vector2(0.23f, 0.22f), new Vector2(0.77f, 0.78f));
            var image = panel.gameObject.AddComponent<Image>();
            image.sprite = theme.roundedSprite;
            image.type = theme.roundedSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = Color.Lerp(theme.panel, Color.black, 0.12f);
            image.raycastTarget = false;

            AddText(panel, "NoticeType", content.later, theme.regularFont, 13f, theme.subText,
                TextAlignmentOptions.TopLeft, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.88f));
            AddText(panel, "Heading", content.auditHeading, theme.boldFont, 28f, theme.text,
                TextAlignmentOptions.TopLeft, new Vector2(0.08f, 0.57f), new Vector2(0.92f, 0.76f));
            AddLine(panel, theme, new Vector2(0.08f, 0.52f), new Vector2(0.92f, 0.523f));
            AddText(panel, "Body", content.auditBody, theme.regularFont, 19f, theme.text,
                TextAlignmentOptions.TopLeft, new Vector2(0.08f, 0.13f), new Vector2(0.92f, 0.46f));
        }

        void BuildFinalBeat(Transform root, UITheme theme, bool isPreserved, EndingSceneContent content)
        {
            var beat = AddBeat(root, "FinalFrame");
            AddLine(beat.transform, theme, new Vector2(0.18f, 0.78f), new Vector2(0.82f, 0.782f));
            AddText(beat.transform, "Title", content.title, theme.boldFont, 42f, theme.text,
                TextAlignmentOptions.Center, new Vector2(0.15f, 0.65f), new Vector2(0.85f, 0.75f));
            AddText(beat.transform, "Consequence", isPreserved ? content.preservedFinal : content.deletedFinal,
                theme.mediumFont, 25f, theme.text, TextAlignmentOptions.Center,
                new Vector2(0.14f, 0.38f), new Vector2(0.86f, 0.59f));
            AddText(beat.transform, "Counts", content.counts, theme.regularFont, 16f, theme.subText,
                TextAlignmentOptions.Center, new Vector2(0.2f, 0.28f), new Vector2(0.8f, 0.34f));
            AddText(beat.transform, "Complete", content.complete, theme.mediumFont, 14f, theme.accent,
                TextAlignmentOptions.Center, new Vector2(0.2f, 0.12f), new Vector2(0.8f, 0.18f));
        }

        CanvasGroup AddBeat(Transform root, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(root, false);
            Stretch((RectTransform)go.transform);
            var group = go.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            beats.Add(group);
            return group;
        }

        void SetBeat(int index, float alpha)
        {
            if (index >= 0 && index < beats.Count)
                beats[index].alpha = alpha;
        }

        static float Window(float time, float start, float end)
        {
            const float fade = 0.55f;
            if (time < start || time > end)
                return 0f;
            float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time - start) / fade));
            float fadeOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((end - time) / fade));
            return Mathf.Min(fadeIn, fadeOut);
        }

        static float FadeIn(float time, float start) =>
            Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time - start) / 0.75f));

        static void AddLine(Transform parent, UITheme theme, Vector2 anchorMin, Vector2 anchorMax)
        {
            var line = AddRect(parent, "Rule", anchorMin, anchorMax);
            var image = line.gameObject.AddComponent<Image>();
            image.color = theme.border;
            image.raycastTarget = false;
        }

        static TMP_Text AddText(Transform parent, string name, string value, TMP_FontAsset font, float size,
            Color color, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = AddRect(parent, name, anchorMin, anchorMax);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = value ?? string.Empty;
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            label.margin = Vector4.zero;
            return label;
        }

        static RectTransform AddRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
