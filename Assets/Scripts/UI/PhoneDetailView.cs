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
            IReadOnlyList<FileTextBlock> document = null, UIText text = null, bool documentLast = false, bool wrapFields = false)
        {
            if (zoom != null)
                zoom.Close();
            ShowDocument(document, theme);
            PlaceDocument(documentLast);
            selectable.Bind(selection);
            headerTitle.text = title;
            backLabel.text = theme.backGlyph + " " + backText;
            PhoneHeaderLayout.Fit(headerTitle, backLabel, theme);
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
                row.SetWrap(wrapFields);
                fields.Add(row.gameObject);
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            // 줄바꿈 값은 실제 폭이 정해진 뒤 행 높이를 맞추고 다시 배치한다.
            if (wrapFields)
            {
                bool resized = false;
                foreach (var go in fields)
                    resized |= go.GetComponent<InfoRowView>().FitWrapped();
                if (resized)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            }
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
        /// <summary>
        /// 문서 블록 위치: 기본은 미리보기 자리(제목 위, 파일 미리보기), documentLast면 정보 줄 아래(메일 본문).
        /// 같은 상세 화면을 앱마다 다시 쓰므로 매번 맞춘다. 씬 구조는 그대로이고 형제 순서만 바꾼다.
        /// </summary>
        void PlaceDocument(bool documentLast)
        {
            int doc = documentRoot.GetSiblingIndex(), field = fieldRoot.GetSiblingIndex();
            if (documentLast && doc < field)
                documentRoot.SetSiblingIndex(field);
            else if (!documentLast && doc > field)
                documentRoot.SetSiblingIndex(heroRoot.transform.GetSiblingIndex() + 1);
        }

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
                if (block.kind == FileTextKind.Table)
                {
                    blocks.Add(TableGrid(block, theme));
                    continue;
                }
                var t = Instantiate(documentBlockTemplate, documentRoot);
                t.gameObject.SetActive(true);
                t.richText = false;
                bool mono = block.kind != FileTextKind.Text;
                t.font = mono ? theme.monoFont : theme.regularFont;
                t.text = string.Join("\n", block.Lines());
                blocks.Add(t.gameObject);
            }
        }

        /// <summary>
        /// 표 블록은 공백 맞춤 텍스트가 아니라 실제 셀로 그린다. 머리글과 첫 열을 구분하고 긴 값은 셀 안에서 줄바꿈한다.
        /// 휴대전화 폭보다 넓으면 가로 휠·Shift+휠·가로 끌기·아래 막대로 넘기며, 세로 입력은 문서 전체 스크롤로 넘긴다.
        /// </summary>
        GameObject TableGrid(FileTextBlock block, UITheme theme)
        {
            const float barHeight = 12f, barGap = 10f;
            const float cellPadX = 10f, cellPadY = 7f;
            var rows = block.Rows().ToList();
            int columns = rows.Count == 0 ? 0 : rows.Max(r => r.Length);
            var widths = new float[columns];
            for (int column = 0; column < columns; column++)
            {
                int units = rows.Max(r => column < r.Length ? DisplayWidth(r[column]) : 0);
                widths[column] = Mathf.Clamp(units * 7f + cellPadX * 2f, 72f, 220f);
            }
            float tableWidth = widths.Sum();

            var root = new GameObject("TableScroll", typeof(RectTransform));
            var rootRt = (RectTransform)root.transform;
            rootRt.SetParent(documentRoot, false);
            // 휠·끌기를 받을 투명 영역
            var input = root.AddComponent<Image>();
            input.color = Color.clear;

            var viewport = (RectTransform)new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).transform;
            viewport.SetParent(rootRt, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(0, barHeight + barGap);
            viewport.offsetMax = Vector2.zero;

            var table = (RectTransform)new GameObject("Grid", typeof(RectTransform), typeof(Image)).transform;
            table.SetParent(viewport, false);
            table.anchorMin = table.anchorMax = table.pivot = new Vector2(0, 1);
            table.anchoredPosition = Vector2.zero;
            table.GetComponent<Image>().color = new Color(theme.phoneSeparator.r, theme.phoneSeparator.g, theme.phoneSeparator.b, 0.9f);

            float y = 0f;
            for (int row = 0; row < rows.Count; row++)
            {
                var cells = new List<RectTransform>();
                float x = 0f;
                float rowHeight = row == 0 ? 38f : 34f;
                for (int column = 0; column < columns; column++)
                {
                    string value = column < rows[row].Length ? rows[row][column] : string.Empty;
                    var cell = (RectTransform)new GameObject($"Cell_{row}_{column}", typeof(RectTransform), typeof(Image)).transform;
                    cell.SetParent(table, false);
                    cell.anchorMin = cell.anchorMax = cell.pivot = new Vector2(0, 1);
                    cell.anchoredPosition = new Vector2(x + 1f, -y - 1f);
                    cell.sizeDelta = new Vector2(widths[column] - 1f, rowHeight - 1f);
                    var background = cell.GetComponent<Image>();
                    if (row == 0)
                        background.color = Color.Lerp(theme.phoneField, theme.phoneAccent, 0.18f);
                    else if (column == 0)
                        background.color = Color.Lerp(theme.phoneScreen, theme.phoneField, 0.78f);
                    else
                        background.color = Color.Lerp(theme.phoneScreen, theme.phoneField, row % 2 == 0 ? 0.38f : 0.58f);

                    var labelRt = (RectTransform)new GameObject("Label", typeof(RectTransform)).transform;
                    labelRt.SetParent(cell, false);
                    labelRt.anchorMin = Vector2.zero;
                    labelRt.anchorMax = Vector2.one;
                    labelRt.offsetMin = new Vector2(cellPadX, cellPadY);
                    labelRt.offsetMax = new Vector2(-cellPadX, -cellPadY);
                    var label = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
                    label.font = row == 0 ? theme.boldFont : theme.regularFont;
                    label.fontSize = row == 0 ? 13f : 12.5f;
                    label.color = row == 0 ? theme.phoneText : (string.IsNullOrEmpty(value) ? theme.phoneSubText : theme.phoneText);
                    label.alignment = TextAlignmentOptions.TopLeft;
                    label.richText = false;
                    label.textWrappingMode = TextWrappingModes.Normal;
                    label.overflowMode = TextOverflowModes.Overflow;
                    label.raycastTarget = false;
                    label.text = value;
                    rowHeight = Mathf.Max(rowHeight,
                        Mathf.Ceil(label.GetPreferredValues(value, widths[column] - cellPadX * 2f, 10000f).y) + cellPadY * 2f);
                    cells.Add(cell);
                    x += widths[column];
                }
                foreach (var cell in cells)
                    cell.sizeDelta = new Vector2(cell.sizeDelta.x, rowHeight - 1f);
                y += rowHeight;
            }
            table.sizeDelta = new Vector2(Mathf.Ceil(tableWidth), Mathf.Ceil(y));
            root.AddComponent<LayoutElement>().preferredHeight = Mathf.Ceil(y) + barHeight + barGap;

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
            horizontal.content = table;
            horizontal.horizontalScrollbar = scrollbar;
            horizontal.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            horizontal.horizontalNormalizedPosition = 0f;
            return root;
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
