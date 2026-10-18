using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Intersection.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 유족 요청 도입 장면. 업무 화면을 덮고 요청 본문(현재 언어)을 문장 순서대로 나타낸다. 앞 문장은 남겨 전체 요청을 읽을 수 있다.
    /// 본문의 줄 중 detailFromLine 이후는 부가 정보(정리 대상 등)로 작게 보인다. 자동으로 넘어가지 않고
    /// 본문이 다 나타난 뒤에만 확인 버튼이 생긴다. 열람 기록은 버튼을 누른 쪽(AppShell)이 남긴다.
    /// </summary>
    public class RequestSceneView : MonoBehaviour
    {
        const float FirstDelay = 0.5f;
        const float SentenceStep = 1.4f;
        const float SentenceFade = 0.8f;
        const float ButtonDelay = 0.6f;

        readonly List<CanvasGroup> lines = new List<CanvasGroup>();
        CanvasGroup headingGroup;
        CanvasGroup buttonGroup;
        Button confirm;
        float shownAt;
        bool selected;

        public bool ButtonReady => buttonGroup != null && buttonGroup.interactable;

        /// <summary>
        /// 본문을 줄로 나눈 뒤 주요 줄은 문장 단위로, 부가 줄은 한 묶음으로 보여준다(표시용 분절만 하고 원문은 그대로).
        /// </summary>
        public static RequestSceneView Create(Canvas canvas, UITheme theme, string heading, string body, int detailFromLine,
            string confirmLabel, Action onConfirm)
        {
            var root = FullScreenParts.Root(canvas, theme, "RequestScene");
            var view = root.AddComponent<RequestSceneView>();
            view.shownAt = Time.unscaledTime;

            view.headingGroup = FullScreenParts.Group(root.transform, "Heading", new Vector2(0.15f, 0.80f), new Vector2(0.85f, 0.87f));
            FullScreenParts.Label(view.headingGroup.gameObject, heading, theme.mediumFont, 16f, theme.accent, TextAlignmentOptions.Center);

            var column = FullScreenParts.Rect(root.transform, "Body", new Vector2(0.14f, 0.26f), new Vector2(0.86f, 0.76f));
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 18f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var all = (body ?? string.Empty).Replace("\r\n", "\n").Split('\n');
            int split = detailFromLine > 0 ? Mathf.Min(detailFromLine, all.Length) : all.Length;
            foreach (var sentence in all.Take(split).SelectMany(Sentences))
                view.AddLine(column, theme, sentence, theme.mediumFont, 30f, theme.text, 0f);
            var detail = string.Join("\n", all.Skip(split).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()));
            if (detail.Length > 0)
                view.AddLine(column, theme, detail, theme.regularFont, 18f, theme.subText, 22f);

            view.buttonGroup = FullScreenParts.Group(root.transform, "Confirm", new Vector2(0.3f, 0.10f), new Vector2(0.7f, 0.19f));
            var row = FullScreenParts.Column(view.buttonGroup.transform, "Row", Vector2.zero, Vector2.one, 0f);
            row.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            view.confirm = FullScreenParts.Button(row, theme, "ConfirmButton", confirmLabel, true, 300f, 54f, () =>
            {
                if (view.ButtonReady)
                    onConfirm?.Invoke();
            });
            view.Tick();
            return view;
        }

        /// <summary>문장 끝(. ! ? 。) 뒤의 공백에서 나눈다. 문장 부호가 없으면 줄 전체가 한 문장이다.</summary>
        static IEnumerable<string> Sentences(string line) =>
            Regex.Split(line.Trim(), @"(?<=[.!?。])\s+").Where(s => !string.IsNullOrWhiteSpace(s));

        void AddLine(RectTransform column, UITheme theme, string value, TMP_FontAsset font, float size, Color color, float topGap)
        {
            var go = new GameObject("Line", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(column, false);
            var label = FullScreenParts.Label(go, value, font, size, color, TextAlignmentOptions.Center);
            label.lineSpacing = 8f;
            if (topGap > 0f)
                label.margin = new Vector4(0f, topGap, 0f, 0f);
            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            lines.Add(group);
        }

        float ButtonAt => FirstDelay + Mathf.Max(0, lines.Count - 1) * SentenceStep + SentenceFade + ButtonDelay;

        public void Tick()
        {
            float t = Time.unscaledTime - shownAt;
            headingGroup.alpha = FullScreenParts.FadeIn(t, 0f, 0.6f);
            for (int i = 0; i < lines.Count; i++)
                lines[i].alpha = FullScreenParts.FadeIn(t, FirstDelay + i * SentenceStep, SentenceFade);
            FullScreenParts.Show(buttonGroup, FullScreenParts.FadeIn(t, ButtonAt, 0.4f));
            if (!selected && buttonGroup.interactable)
            {
                selected = true;
                FullScreenParts.Select(confirm);
            }
        }

        /// <summary>검수용: 연출을 끝낸 상태로 맞춘다.</summary>
        public void SkipFade() => shownAt = Time.unscaledTime - (ButtonAt + 1f);

        /// <summary>검수용: 표시 순서대로 나눈 문장·부가 정보.</summary>
        public IEnumerable<string> Lines() => lines.Select(g => g.GetComponent<TMP_Text>().text);
    }
}
