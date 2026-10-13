using System;
using System.Collections.Generic;
using Intersection.Core;
using Intersection.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>미리보기 이미지를 누르면 열람을 연다. 기록 고르기 중이면 누르기를 바깥(화면 선택)으로 넘긴다.</summary>
    public class PreviewClick : MonoBehaviour, IPointerClickHandler
    {
        public Action Clicked;

        public void OnPointerClick(PointerEventData e)
        {
            if (SelectionBus.Active || e.dragging)
            {
                if (transform.parent != null)
                    ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, e, ExecuteEvents.pointerClickHandler);
                return;
            }
            Clicked?.Invoke();
        }
    }

    /// <summary>
    /// 휴대전화 안에서 캡처 이미지를 확대해 보는 열람 화면. 상세 화면의 스크롤 영역 위에 겹쳐 열고(휴대전화 프레임 안),
    /// 닫으면 그대로 사라져 상세 화면의 스크롤 위치가 남는다. 열람 상태일 뿐 고정·분류·메모·비교 이력에 영향을 주지 않는다.
    /// 처음에는 너비 맞춤(맨 위부터), 휠로 확대·축소(포인터 기준), 끌어서 이동, 더블클릭으로 너비 맞춤 ↔ 읽기 배율,
    /// `화면 맞춤`(전체 보기)은 보조 버튼(전체 보기 중에는 `너비 맞춤`으로 바뀜).
    /// 배율 한계·단계는 테마, 조작 문구는 문자열 표에서 읽는다. 배율 %는 원본 픽셀 대비다.
    /// </summary>
    public class CaptureZoomView : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        static readonly List<CaptureZoomView> open = new List<CaptureZoomView>();
        /// <summary>확대 화면 아래 조작 막대 전용 높이와 막대 높이 (휴대전화 UI 단위).</summary>
        const float ControlStrip = 44f, ControlHeight = 32f;

        RectTransform viewport;
        RectTransform content;
        RawImage image;
        TMP_Text percentLabel;
        UITheme theme;
        UIText text;
        float scale;
        bool dragging;
        /// <summary>지금 맞춤 방식 (크기 변경 때 다시 맞출 방식, 맞춤 버튼 문구).</summary>
        FitMode mode;
        TMP_Text fitLabel;

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>가장 최근에 연 확대 화면을 닫는다(Esc 한 단계). 닫은 것이 있으면 true.</summary>
        public static bool CloseTopmost()
        {
            for (int i = open.Count - 1; i >= 0; i--)
            {
                if (open[i] == null || !open[i].IsOpen)
                {
                    open.RemoveAt(i);
                    continue;
                }
                // 다른 화면에 가려 보이지 않는 확대 화면은 건너뛴다(보이는 것부터 한 단계씩).
                if (!open[i].gameObject.activeInHierarchy)
                    continue;
                open[i].Close();
                return true;
            }
            return false;
        }

        /// <summary>host(상세 화면의 스크롤 영역)와 같은 자리를 덮는 확대 화면을 만든다. 처음 한 번만 만든다.</summary>
        public static CaptureZoomView Create(RectTransform host, UITheme theme, UIText text)
        {
            var root = new GameObject("CaptureZoom", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(host.parent, false);
            root.anchorMin = host.anchorMin;
            root.anchorMax = host.anchorMax;
            root.offsetMin = host.offsetMin;
            root.offsetMax = host.offsetMax;
            root.pivot = host.pivot;
            root.SetAsLastSibling();
            var bg = root.gameObject.AddComponent<Image>();
            bg.color = theme.phoneScreen;

            var view = root.gameObject.AddComponent<CaptureZoomView>();
            view.theme = theme;
            view.text = text;

            // 아래에 조작 막대 전용 높이를 두어 막대가 사진·본문을 덮지 않게 한다.
            view.viewport = Child("Viewport", root);
            Stretch(view.viewport);
            view.viewport.offsetMin = new Vector2(0, ControlStrip);
            view.viewport.gameObject.AddComponent<RectMask2D>();
            view.content = Child("Image", view.viewport);
            view.content.anchorMin = view.content.anchorMax = view.content.pivot = new Vector2(0.5f, 0.5f);
            view.image = view.content.gameObject.AddComponent<RawImage>();
            view.image.raycastTarget = false;
            view.image.material = theme.captureMaterial;

            // 아래 작은 조작 막대: − · 배율 · + · 화면 맞춤
            var bar = Child("Controls", root);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0);
            bar.pivot = new Vector2(0.5f, 0);
            bar.anchoredPosition = new Vector2(0, (ControlStrip - ControlHeight) * 0.5f);
            bar.sizeDelta = new Vector2(236, ControlHeight);
            var barBg = bar.gameObject.AddComponent<Image>();
            barBg.sprite = theme.roundedSprite;
            barBg.type = Image.Type.Sliced;
            barBg.color = new Color(theme.phoneField.r, theme.phoneField.g, theme.phoneField.b, 0.92f);
            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 3, 3);
            layout.spacing = 2;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            view.Button(bar, text.Get(UIKeys.ZoomOut), 36, () => view.Step(-1));
            view.percentLabel = view.Label(bar, string.Empty, 54);
            view.Button(bar, text.Get(UIKeys.ZoomIn), 36, () => view.Step(1));
            view.fitLabel = view.Button(bar, text.Get(UIKeys.ZoomFit), 88, view.ToggleWhole);

            root.gameObject.SetActive(false);
            return view;
        }

        /// <summary>처음에는 너비 맞춤(상세 화면과 같거나 더 큰 폭)으로, 캡처 맨 위부터 연다.</summary>
        public void Open(Texture texture)
        {
            image.texture = texture;
            content.sizeDelta = new Vector2(texture.width, texture.height);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            open.Remove(this);
            open.Add(this);
            Canvas.ForceUpdateCanvases();
            ShowWidth(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
            open.Remove(this);
        }

        void OnDisable() => open.Remove(this);

        // 휴대전화·비교 칸 크기가 바뀌면(해상도 변경 등) 같은 맞춤 방식으로 다시 맞추고, 아니면 배율 한계와 이동 범위를 다시 고정한다.
        void OnRectTransformDimensionsChange()
        {
            if (!isActiveAndEnabled || image == null || image.texture == null || scale <= 0)
                return;
            if (mode == FitMode.Width)
                ShowWidth(false);
            else if (mode == FitMode.Whole)
                ShowWhole();
            else
                SetScale(scale, Vector2.zero, false);
        }

        enum FitMode { Width, Whole, Free }

        /// <summary>캡처 전체가 한 번에 보이는 배율 (최소 배율).</summary>
        float WholeScale
        {
            get
            {
                var tex = image.texture;
                var size = viewport.rect.size;
                if (tex == null || tex.width == 0 || tex.height == 0 || size.x <= 0 || size.y <= 0)
                    return 1f;
                return Mathf.Min(size.x / tex.width, size.y / tex.height);
            }
        }

        /// <summary>캡처 폭이 화면 폭에 꼭 맞는 배율 (처음 여는 배율).</summary>
        float WidthScale
        {
            get
            {
                var tex = image.texture;
                float w = viewport.rect.width;
                return tex == null || tex.width == 0 || w <= 0 ? 1f : Mathf.Max(WholeScale, w / tex.width);
            }
        }

        float MaxScale => Mathf.Max(WidthScale, theme.zoomMaxPercent / 100f);

        /// <summary>읽기 배율. 너비 맞춤이 이미 그보다 크면(넓은 비교 칸 등) 두 단계 더 키운다.</summary>
        float ReadScale
        {
            get
            {
                float read = theme.zoomReadPercent / 100f;
                if (read <= WidthScale * 1.05f)
                    read = WidthScale * theme.zoomStep * theme.zoomStep;
                return Mathf.Clamp(read, WholeScale, MaxScale);
            }
        }

        static bool Near(float a, float b) => Mathf.Abs(a - b) <= b * 0.01f;

        /// <summary>너비 맞춤. top이면 캡처 맨 위, 아니면 지금 보던 높이를 유지한다.</summary>
        void ShowWidth(bool top)
        {
            SetScale(WidthScale, Vector2.zero, false);
            float my = Mathf.Max(0, (content.sizeDelta.y * scale - viewport.rect.height) * 0.5f);
            content.anchoredPosition = Clamp(new Vector2(0, top ? -my : content.anchoredPosition.y));
            SetMode(FitMode.Width);
        }

        void ShowWhole()
        {
            SetScale(WholeScale, Vector2.zero, true);
            SetMode(FitMode.Whole);
        }

        /// <summary>`화면 맞춤`(전체 보기) ↔ `너비 맞춤`. 전체 보기 중에는 버튼이 `너비 맞춤`으로 바뀐다.</summary>
        void ToggleWhole()
        {
            if (mode == FitMode.Whole)
                ShowWidth(true);
            else
                ShowWhole();
        }

        void Step(int direction)
        {
            SetScale(scale * Mathf.Pow(theme.zoomStep, direction), Vector2.zero, false);
            UpdateMode();
        }

        void UpdateMode() => SetMode(Near(scale, WidthScale) ? FitMode.Width : Near(scale, WholeScale) ? FitMode.Whole : FitMode.Free);

        void SetMode(FitMode value)
        {
            mode = value;
            fitLabel.text = text.Get(mode == FitMode.Whole ? UIKeys.ZoomFitWidth : UIKeys.ZoomFit);
        }

        /// <summary>배율을 바꾸되 pivot(뷰포트 가운데 기준 좌표) 아래의 지점이 그대로 있게 한다.</summary>
        void SetScale(float value, Vector2 pivot, bool center)
        {
            float old = scale <= 0 ? WidthScale : scale;
            scale = Mathf.Clamp(value, WholeScale, MaxScale);
            content.localScale = new Vector3(scale, scale, 1);
            var pos = center ? Vector2.zero : pivot - (pivot - content.anchoredPosition) * (scale / old);
            content.anchoredPosition = Clamp(pos);
            percentLabel.text = text.Format(UIKeys.ZoomPercent, ("n", Mathf.RoundToInt(scale * 100).ToString()));
        }

        Vector2 Clamp(Vector2 pos)
        {
            var size = content.sizeDelta * scale;
            var view = viewport.rect.size;
            float mx = Mathf.Max(0, (size.x - view.x) * 0.5f), my = Mathf.Max(0, (size.y - view.y) * 0.5f);
            return new Vector2(Mathf.Clamp(pos.x, -mx, mx), Mathf.Clamp(pos.y, -my, my));
        }

        Vector2 Local(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, e.position, EventCamera(e), out var p);
            return p - viewport.rect.center;
        }

        /// <summary>이벤트에 카메라 정보가 없으면 이 화면이 속한 캔버스의 카메라를 쓴다.</summary>
        Camera EventCamera(PointerEventData e)
        {
            var c = e.pressEventCamera != null ? e.pressEventCamera : e.enterEventCamera;
            if (c != null)
                return c;
            var canvas = GetComponentInParent<Canvas>();
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
        }

        public void OnScroll(PointerEventData e)
        {
            if (Mathf.Approximately(e.scrollDelta.y, 0))
                return;
            SetScale(scale * Mathf.Pow(theme.zoomStep, Mathf.Sign(e.scrollDelta.y)), Local(e), false);
            UpdateMode();
        }

        public void OnBeginDrag(PointerEventData e) => dragging = true;

        public void OnDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, e.position, EventCamera(e), out var now);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, e.position - e.delta, EventCamera(e), out var before);
            content.anchoredPosition = Clamp(content.anchoredPosition + (now - before));
        }

        public void OnEndDrag(PointerEventData e) => dragging = false;

        /// <summary>더블클릭: 너비 맞춤(또는 전체 보기) → 읽기 배율, 그 밖 → 너비 맞춤.</summary>
        public void OnPointerClick(PointerEventData e)
        {
            if (dragging || e.clickCount < 2)
                return;
            if (scale <= WidthScale * 1.01f)
            {
                SetScale(ReadScale, Local(e), false);
                // 읽기 배율은 누른 높이를 유지하되 글이 시작되는 왼쪽 끝에 맞춘다.
                float mx = Mathf.Max(0, (content.sizeDelta.x * scale - viewport.rect.width) * 0.5f);
                content.anchoredPosition = Clamp(new Vector2(mx, content.anchoredPosition.y));
                SetMode(FitMode.Free);
            }
            else
            {
                SetScale(WidthScale, Local(e), false);
                content.anchoredPosition = Clamp(new Vector2(0, content.anchoredPosition.y));
                SetMode(FitMode.Width);
            }
        }

        // ── 부품

        TMP_Text Button(RectTransform parent, string label, float width, Action onClick)
        {
            var rt = Child("Button", parent);
            rt.gameObject.AddComponent<LayoutElement>().preferredWidth = width;
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = Color.clear;
            var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() => onClick());
            var t = Label(rt, label, 0);
            Stretch(t.rectTransform);
            return t;
        }

        TMP_Text Label(RectTransform parent, string value, float width)
        {
            var rt = Child("Label", parent);
            if (width > 0)
                rt.gameObject.AddComponent<LayoutElement>().preferredWidth = width;
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = theme.mediumFont;
            t.fontSize = 13;
            t.color = theme.phoneText;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            t.text = value;
            return t;
        }

        static RectTransform Child(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
