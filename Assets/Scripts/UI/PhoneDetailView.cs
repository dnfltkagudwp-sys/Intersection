using System;
using System.Collections.Generic;
using System.Linq;
using Intersection.Core;
using Intersection.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 브라우저·지도·파일·설정 기록의 상세 화면.
    /// 위쪽 영역(지도·미리보기·페이지)은 실제 자산이 없으면 중립 자리 표시만 보여준다.
    /// 휴대전화 안에는 기기에서 볼 수 있는 정보만 넣고, 기록 코드·출처는 업무 패널에 둔다.
    /// </summary>
    public class PhoneDetailView : MonoBehaviour
    {
        public class Hero
        {
            public Sprite background;
            public Texture texture;
            public Sprite glyph;
            public string message;
        }

        [SerializeField] Button backButton;
        [SerializeField] TMP_Text backLabel;
        [SerializeField] TMP_Text headerTitle;
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform content;
        [SerializeField] GameObject heroRoot;
        [SerializeField] Image heroBackground;
        [SerializeField] RawImage heroTexture;
        [SerializeField] Image heroGlyph;
        [SerializeField] TMP_Text heroMessage;
        [SerializeField] RectTransform documentRoot;
        [SerializeField] TMP_Text documentBlockTemplate;
        [SerializeField] TMP_Text headline;
        [SerializeField] RectTransform fieldRoot;
        [SerializeField] InfoRowView fieldPrefab;
        [SerializeField] SelectableRecord selectable;

        readonly List<GameObject> fields = new List<GameObject>();
        readonly List<GameObject> blocks = new List<GameObject>();

        LayoutElement heroLayout;
        float heroDefaultHeight = -1f;
        /// <summary>미리보기 이미지의 세로/가로. 0이면 기본 높이(자리 표시).</summary>
        float heroAspect;
        bool fitting;
        CaptureZoomView zoom;

        /// <summary>확대 열람이 열려 있는지 (뒤로가기·Esc는 이것부터 닫는다).</summary>
        public bool ZoomOpen => zoom != null && zoom.IsOpen;

        /// <param name="selection">열린 기록(페이지·장소·파일·설정 변경) 자체. 선택 모드에서 화면 전체로 선택한다.</param>
        /// <param name="document">미리보기 자리에 그릴 문서 블록(파일 visibleText). 검색과 같은 원본이다.</param>
        public void Show(string title, string backText, string headlineText, Hero hero,
            List<(string label, string value)> fieldValues, UITheme theme, Action onBack, RecordRef selection,
            IReadOnlyList<FileTextBlock> document = null, UIText text = null)
        {
            if (zoom != null)
                zoom.Close();
            ShowDocument(document, theme);
            selectable.Bind(selection);
            headerTitle.text = title;
            backLabel.text = theme.backGlyph + " " + backText;
            backButton.onClick.RemoveAllListeners();
            // 확대 열람 중이면 뒤로가기는 확대 화면만 닫는다(상세 화면·스크롤 위치는 그대로).
            backButton.onClick.AddListener(() =>
            {
                if (ZoomOpen)
                    zoom.Close();
                else
                    onBack();
            });

            heroRoot.SetActive(hero != null);
            heroAspect = hero != null && hero.texture != null && hero.texture.width > 0 ? (float)hero.texture.height / hero.texture.width : 0f;
            if (hero != null)
            {
                bool hasTexture = hero.texture != null;
                heroTexture.gameObject.SetActive(hasTexture);
                heroTexture.texture = hero.texture;
                // 큰 이미지(화면 캡처 등)를 작게 보여 줄 때 계단·끊김이 없도록 다중 샘플 축소 재질
                heroTexture.material = theme.captureMaterial;
                heroBackground.sprite = hero.background;
                heroBackground.color = hero.background != null ? Color.white : theme.photoPlaceholder;
                heroGlyph.gameObject.SetActive(!hasTexture && hero.glyph != null);
                heroGlyph.sprite = hero.glyph;
                heroGlyph.color = hero.background != null ? theme.phoneAccent : theme.photoPlaceholderGlyph;
                heroMessage.gameObject.SetActive(!hasTexture && !string.IsNullOrEmpty(hero.message));
                heroMessage.text = hero.message ?? string.Empty;
                BindPreviewClick(hasTexture && text != null ? hero.texture : null, theme, text);
            }

            headline.text = headlineText ?? string.Empty;
            headline.gameObject.SetActive(!string.IsNullOrEmpty(headlineText));

            foreach (var go in fields)
                UIPool.Discard(go);
            fields.Clear();
            foreach (var (label, value) in fieldValues)
            {
                if (string.IsNullOrEmpty(value))
                    continue;
                var row = Instantiate(fieldPrefab, fieldRoot);
                row.Bind(label, value, theme.regularFont);
                fields.Add(row.gameObject);
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            FitHero(true);
            scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>
        /// 미리보기 이미지를 누르면 휴대전화 안 확대 열람을 연다(이미지가 있을 때만). 확대 화면은 스크롤 영역 위에 겹쳐
        /// 처음 한 번만 만들고, 닫아도 상세 화면의 스크롤 위치는 그대로다. 열람 상태일 뿐 업무 기록은 바꾸지 않는다.
        /// </summary>
        void BindPreviewClick(Texture texture, UITheme theme, UIText text)
        {
            heroBackground.raycastTarget = texture != null;
            var click = heroRoot.GetComponent<PreviewClick>();
            if (texture == null)
            {
                if (click != null)
                    click.Clicked = null;
                return;
            }
            if (click == null)
                click = heroRoot.AddComponent<PreviewClick>();
            click.Clicked = () =>
            {
                if (zoom == null)
                    zoom = CaptureZoomView.Create((RectTransform)scroll.transform, theme, text);
                zoom.Open(texture);
            };
        }

        // 화면 크기가 바뀌면 다음 레이아웃 갱신 때 다시 맞춘다(레이아웃 계산 중에 강제 재계산하지 않음).
        void OnRectTransformDimensionsChange() => FitHero(false);

        /// <summary>
        /// 미리보기 이미지(휴대전화 화면 캡처 등)는 폭에 맞추고 원래 비율대로 높이를 정한다(늘리거나 자르지 않음).
        /// 이미지가 없으면 빌더가 정한 기본 높이의 자리 표시.
        /// </summary>
        void FitHero(bool immediate)
        {
            if (fitting || heroRoot == null || !heroRoot.activeSelf)
                return;
            if (heroLayout == null)
            {
                heroLayout = heroRoot.GetComponent<LayoutElement>();
                if (heroLayout == null)
                    return;
                heroDefaultHeight = heroLayout.preferredHeight;
            }
            float width = ((RectTransform)heroRoot.transform).rect.width;
            float height = heroAspect > 0 && width > 0 ? Mathf.Round(width * heroAspect) : heroDefaultHeight;
            if (Mathf.Approximately(heroLayout.preferredHeight, height))
                return;
            heroLayout.preferredHeight = height;
            if (!immediate)
                return;
            fitting = true;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            fitting = false;
        }

        /// <summary>
        /// 파일 미리보기 문서. 문단은 본문 글꼴, 로그·표는 고정폭 글꼴로 그린다.
        /// 데이터의 `&lt;` 같은 글자가 서식으로 해석되지 않도록 서식 태그는 끈 채 그대로 보여준다.
        /// </summary>
        void ShowDocument(IReadOnlyList<FileTextBlock> document, UITheme theme)
        {
            foreach (var go in blocks)
                UIPool.Discard(go);
            blocks.Clear();
            bool any = document != null && document.Count > 0;
            documentRoot.gameObject.SetActive(any);
            if (!any)
                return;
            foreach (var block in document)
            {
                var t = Instantiate(documentBlockTemplate, documentRoot);
                t.gameObject.SetActive(true);
                t.richText = false;
                bool mono = block.kind != FileTextKind.Text;
                t.font = mono ? theme.monoFont : theme.regularFont;
                t.text = block.kind == FileTextKind.Table ? TableText(block) : string.Join("\n", block.Lines());
                blocks.Add(block.kind == FileTextKind.Table ? TableScroller(t, theme) : t.gameObject);
            }
        }

        /// <summary>
        /// 표는 줄바꿈하면 칸이 어긋나 읽을 수 없으므로 줄바꿈 없이 그리고, 휴대전화 폭보다 넓으면 가로로 넘긴다
        /// (가로 휠·Shift+휠·가로 끌기·아래 막대). 세로 휠은 문서 전체 스크롤로 넘어간다.
        /// </summary>
        GameObject TableScroller(TMP_Text text, UITheme theme)
        {
            const float barHeight = 6f, barGap = 8f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            var size = text.GetPreferredValues(text.text, 100000f, 0f);

            var root = new GameObject("TableScroll", typeof(RectTransform));
            var rootRt = (RectTransform)root.transform;
            rootRt.SetParent(documentRoot, false);
            rootRt.SetSiblingIndex(text.transform.GetSiblingIndex());
            root.AddComponent<LayoutElement>().preferredHeight = Mathf.Ceil(size.y) + barHeight + barGap;
            // 휠·끌기를 받을 투명 영역
            root.AddComponent<Image>().color = Color.clear;

            var viewport = (RectTransform)new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).transform;
            viewport.SetParent(rootRt, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(0, barHeight + barGap);
            viewport.offsetMax = Vector2.zero;

            var content = (RectTransform)text.transform;
            content.SetParent(viewport, false);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(Mathf.Ceil(size.x) + 2, Mathf.Ceil(size.y));

            var bar = (RectTransform)new GameObject("Scrollbar", typeof(RectTransform)).transform;
            bar.SetParent(rootRt, false);
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = new Vector2(1, 0);
            bar.pivot = new Vector2(0.5f, 0);
            bar.sizeDelta = new Vector2(0, barHeight);
            var track = bar.gameObject.AddComponent<Image>();
            track.sprite = theme.roundedSprite;
            track.type = Image.Type.Sliced;
            track.color = new Color(theme.phoneSubText.r, theme.phoneSubText.g, theme.phoneSubText.b, 0.2f);
            var handle = (RectTransform)new GameObject("Handle", typeof(RectTransform)).transform;
            handle.SetParent(bar, false);
            handle.offsetMin = handle.offsetMax = Vector2.zero;
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = theme.roundedSprite;
            handleImage.type = Image.Type.Sliced;
            handleImage.color = new Color(theme.phoneSubText.r, theme.phoneSubText.g, theme.phoneSubText.b, 0.8f);
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.LeftToRight;

            var horizontal = root.AddComponent<TableScrollRect>();
            horizontal.horizontal = true;
            horizontal.vertical = false;
            horizontal.movementType = ScrollRect.MovementType.Clamped;
            horizontal.scrollSensitivity = scroll.scrollSensitivity;
            horizontal.viewport = viewport;
            horizontal.content = content;
            horizontal.horizontalScrollbar = scrollbar;
            horizontal.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            horizontal.horizontalNormalizedPosition = 0f;
            return root;
        }

        /// <summary>표를 고정폭 글꼴에서 칸이 맞도록 공백으로 채운다(한글 등 전각 글자는 두 칸).</summary>
        static string TableText(FileTextBlock block)
        {
            var rows = block.Rows().ToList();
            int columns = rows.Count == 0 ? 0 : rows.Max(r => r.Length);
            var widths = new int[columns];
            foreach (var r in rows)
                for (int i = 0; i < r.Length; i++)
                    widths[i] = Math.Max(widths[i], DisplayWidth(r[i]));
            var sb = new System.Text.StringBuilder();
            foreach (var r in rows)
            {
                if (sb.Length > 0)
                    sb.Append('\n');
                for (int i = 0; i < r.Length; i++)
                {
                    sb.Append(r[i]);
                    if (i < r.Length - 1)
                        sb.Append(' ', widths[i] - DisplayWidth(r[i]) + 2);
                }
            }
            return sb.ToString();
        }

        static int DisplayWidth(string s)
        {
            int w = 0;
            foreach (char c in s)
                w += c >= 0x1100 && (c <= 0x115F || (c >= 0x2E80 && c <= 0xA4CF) || (c >= 0xAC00 && c <= 0xD7A3)
                                     || (c >= 0xF900 && c <= 0xFAFF) || (c >= 0xFF00 && c <= 0xFF60)) ? 2 : 1;
            return w;
        }
    }
}
