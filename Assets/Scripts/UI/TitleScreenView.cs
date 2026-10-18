using System;
using System.Collections.Generic;
using Intersection.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 업무 프로그램을 완전히 덮는 시작 화면(타이틀·요청 장면) 공용 부품. 엔딩 연출(EndingSceneView)과 같은 어두운 배경·글꼴·색을 쓴다.
    /// 씬에 미리 두지 않고 실행 중 캔버스 맨 위에 만들었다가 닫을 때 지운다.
    /// </summary>
    static class FullScreenParts
    {
        public static Color Background(UITheme theme) => Color.Lerp(theme.stage, Color.black, 0.62f);

        /// <summary>캔버스 전체를 덮고 아래 업무 화면의 마우스 입력을 모두 받는 루트.</summary>
        public static GameObject Root(Canvas canvas, UITheme theme, string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            root.transform.SetParent(canvas.transform, false);
            root.transform.SetAsLastSibling();
            Stretch((RectTransform)root.transform);
            var background = root.GetComponent<Image>();
            background.color = Background(theme);
            background.raycastTarget = true;
            return root;
        }

        public static CanvasGroup Group(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = Rect(parent, name, anchorMin, anchorMax);
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        public static TMP_Text Text(Transform parent, string name, string value, TMP_FontAsset font, float size,
            Color color, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = Rect(parent, name, anchorMin, anchorMax);
            return Label(rect.gameObject, value, font, size, color, alignment);
        }

        public static TMP_Text Label(GameObject go, string value, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment)
        {
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = value ?? string.Empty;
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            label.margin = Vector4.zero;
            return label;
        }

        /// <summary>둥근 버튼. primary면 강조 바탕, 아니면 패널 바탕. 마우스를 올리면 밝아진다.</summary>
        public static Button Button(Transform parent, UITheme theme, string name, string label, bool primary, float width, float height, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(width, height);
            var layout = go.GetComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;
            var image = go.GetComponent<Image>();
            image.sprite = theme.roundedSprite;
            image.type = theme.roundedSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = primary ? theme.accentSoft : theme.panelRaised;
            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.selectedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.colorMultiplier = 1.4f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick?.Invoke());

            var text = Rect(go.transform, "Label", Vector2.zero, Vector2.one);
            Label(text.gameObject, label, theme.mediumFont, 17f, primary ? theme.accent : theme.text, TextAlignmentOptions.Center);
            return button;
        }

        /// <summary>세로로 버튼을 쌓는 묶음(가운데 정렬).</summary>
        public static RectTransform Column(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, float spacing)
        {
            var rect = Rect(parent, name, anchorMin, anchorMax);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = spacing;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return rect;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
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

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static float FadeIn(float time, float start, float duration) =>
            Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time - start) / Mathf.Max(0.01f, duration)));

        /// <summary>보이기 시작한 묶음만 클릭·선택을 받는다.</summary>
        public static void Show(CanvasGroup group, float alpha)
        {
            group.alpha = alpha;
            bool ready = alpha >= 0.99f;
            group.interactable = ready;
            group.blocksRaycasts = ready;
        }

        public static void Select(Selectable target)
        {
            var events = EventSystem.current;
            if (events != null)
                events.SetSelectedGameObject(target != null ? target.gameObject : null);
        }
    }

    public sealed class TitleScreenContent
    {
        public string title;
        public string subtitle;
        public string resetHeading;
        public string resetBody;
        public string resetConfirm;
        public string resetCancel;
        public string resetFailed;
    }

    /// <summary>
    /// 타이틀 화면. 중앙 제목이 약 0.8초 동안 나타나고 이어서 저장 상태에 맞는 메뉴가 나타난다.
    /// 표시만으로 진행·업무 저장을 바꾸지 않는다. 새로 시작하기는 확인 상자를 거친다.
    /// </summary>
    public class TitleScreenView : MonoBehaviour
    {
        public struct MenuItem
        {
            public string label;
            public bool primary;
            /// <summary>true면 바로 실행하지 않고 초기화 확인을 거친다.</summary>
            public bool confirmReset;
            public Action onClick;
        }

        const float TitleFade = 0.8f;
        const float MenuDelay = 0.8f;
        const float MenuFade = 0.45f;

        UITheme theme;
        TitleScreenContent content;
        CanvasGroup titleGroup;
        CanvasGroup menuGroup;
        CanvasGroup confirmGroup;
        TMP_Text confirmBody;
        Button confirmCancel;
        Button firstMenuButton;
        readonly List<Button> menuButtons = new List<Button>();
        Action pendingReset;
        float shownAt;
        bool menuSelected;

        public bool ConfirmOpen => confirmGroup != null && confirmGroup.gameObject.activeSelf;

        public static TitleScreenView Create(Canvas canvas, UITheme theme, TitleScreenContent content, IReadOnlyList<MenuItem> menu)
        {
            var root = FullScreenParts.Root(canvas, theme, "TitleScreen");
            var view = root.AddComponent<TitleScreenView>();
            view.theme = theme;
            view.content = content;
            view.shownAt = Time.unscaledTime;
            view.Build(root.transform, menu);
            view.Tick();
            return view;
        }

        void Build(Transform root, IReadOnlyList<MenuItem> menu)
        {
            titleGroup = FullScreenParts.Group(root, "Title", Vector2.zero, Vector2.one);
            FullScreenParts.Text(titleGroup.transform, "Name", content.title, theme.boldFont, 64f, theme.text,
                TextAlignmentOptions.Bottom, new Vector2(0.15f, 0.55f), new Vector2(0.85f, 0.70f));
            var sub = FullScreenParts.Text(titleGroup.transform, "Subtitle", content.subtitle, theme.regularFont, 15f, theme.subText,
                TextAlignmentOptions.Top, new Vector2(0.2f, 0.47f), new Vector2(0.8f, 0.535f));
            sub.characterSpacing = 18f;

            menuGroup = FullScreenParts.Group(root, "Menu", new Vector2(0.3f, 0.08f), new Vector2(0.7f, 0.40f));
            var column = FullScreenParts.Column(menuGroup.transform, "Items", Vector2.zero, Vector2.one, 14f);
            foreach (var item in menu)
            {
                var entry = item;
                var button = FullScreenParts.Button(column, theme, "Menu", entry.label, entry.primary, 300f, 50f,
                    () => OnMenu(entry));
                menuButtons.Add(button);
                if (firstMenuButton == null)
                    firstMenuButton = button;
            }

            BuildConfirm(root);
        }

        void BuildConfirm(Transform root)
        {
            confirmGroup = FullScreenParts.Group(root, "ResetConfirm", Vector2.zero, Vector2.one);
            var shade = confirmGroup.gameObject.AddComponent<Image>();
            shade.color = new Color(0f, 0f, 0f, 0.55f);
            shade.raycastTarget = true;
            var panel = FullScreenParts.Rect(confirmGroup.transform, "Panel", new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.7f));
            var image = panel.gameObject.AddComponent<Image>();
            image.sprite = theme.roundedSprite;
            image.type = theme.roundedSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = theme.panel;
            image.raycastTarget = true;
            FullScreenParts.Text(panel, "Heading", content.resetHeading, theme.boldFont, 24f, theme.text,
                TextAlignmentOptions.TopLeft, new Vector2(0.08f, 0.70f), new Vector2(0.92f, 0.88f));
            confirmBody = FullScreenParts.Text(panel, "Body", content.resetBody, theme.regularFont, 17f, theme.subText,
                TextAlignmentOptions.TopLeft, new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.66f));
            var row = FullScreenParts.Rect(panel, "Buttons", new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.26f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.spacing = 12f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            confirmCancel = FullScreenParts.Button(row, theme, "Cancel", content.resetCancel, false, 150f, 46f, CloseConfirm);
            FullScreenParts.Button(row, theme, "Confirm", content.resetConfirm, true, 170f, 46f, ConfirmReset);
            confirmGroup.gameObject.SetActive(false);
        }

        void OnMenu(MenuItem item)
        {
            if (ConfirmOpen)
                return;
            if (item.confirmReset)
            {
                pendingReset = item.onClick;
                confirmBody.text = content.resetBody;
                confirmBody.color = theme.subText;
                confirmGroup.gameObject.SetActive(true);
                FullScreenParts.Show(confirmGroup, 1f);
                SetMenuInteractable(false);
                FullScreenParts.Select(confirmCancel);
                return;
            }
            item.onClick?.Invoke();
        }

        void ConfirmReset() => pendingReset?.Invoke();

        /// <summary>초기화에 실패하면 확인 상자에 알리고 저장·화면은 그대로 둔다.</summary>
        public void ShowResetFailed()
        {
            if (!ConfirmOpen)
                return;
            confirmBody.text = content.resetFailed;
            confirmBody.color = theme.text;
            FullScreenParts.Select(confirmCancel);
        }

        /// <summary>취소: 저장과 화면 상태를 바꾸지 않고 메뉴로 돌아간다.</summary>
        public void CloseConfirm()
        {
            if (!ConfirmOpen)
                return;
            pendingReset = null;
            confirmGroup.gameObject.SetActive(false);
            SetMenuInteractable(true);
            FullScreenParts.Select(firstMenuButton);
        }

        /// <summary>확인 상자가 떠 있는 동안 메뉴는 숨기고 막는다(닫으면 Tick이 다시 보이게 한다).</summary>
        void SetMenuInteractable(bool on)
        {
            menuGroup.interactable = on;
            menuGroup.blocksRaycasts = on;
            if (!on)
                menuGroup.alpha = 0f;
        }

        public void Tick()
        {
            float t = Time.unscaledTime - shownAt;
            titleGroup.alpha = FullScreenParts.FadeIn(t, 0f, TitleFade);
            if (ConfirmOpen)
                return;
            FullScreenParts.Show(menuGroup, FullScreenParts.FadeIn(t, MenuDelay, MenuFade));
            // 메뉴가 다 나타난 뒤 첫 항목을 선택해 Enter·방향키로도 고를 수 있게 한다.
            if (!menuSelected && menuGroup.interactable)
            {
                menuSelected = true;
                FullScreenParts.Select(firstMenuButton);
            }
        }

        /// <summary>검수용: 메뉴 버튼 문구(표시 순서).</summary>
        public IEnumerable<string> MenuLabels()
        {
            foreach (var b in menuButtons)
                yield return b.GetComponentInChildren<TMP_Text>().text;
        }

        /// <summary>검수용: 연출을 끝낸 상태로 맞춘다.</summary>
        public void SkipFade() => shownAt = Time.unscaledTime - (MenuDelay + MenuFade + 1f);
    }
}
