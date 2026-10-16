using System;
using System.Collections.Generic;
using Intersection.Core;
using Intersection.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 브라우저·지도·파일·설정 앱의 목록 화면. 최상위 화면은 큰 제목, 하위 화면은 뒤로가기 머리말을 쓴다.
    /// 행·구역·개수·순서는 모두 호출하는 쪽이 데이터에서 만든 항목 목록을 그대로 그린다.
    /// </summary>
    public class PhoneListView : MonoBehaviour
    {
        public class Item
        {
            public bool isSection;
            public string title;
            public string subtitle;
            public string trailing;
            public Sprite icon;
            public Action onClick;
            /// <summary>이 원본을 실제로 열어 본 적이 있으면 목록 오른쪽에 확인 표시를 붙인다.</summary>
            public bool viewed;
            /// <summary>선택 모드에서 이 행이 가리키는 기록. null이면 선택 대상이 아니다 (폴더·위치 등).</summary>
            public RecordRef selection;

            /// <summary>목록 머리의 안내 한 줄 (누를 수 없고 기록 수에 들지 않음). 구역 머리와 같은 줄 모양을 흐린 작은 글자로 쓴다.</summary>
            public bool isNote;

            public static Item Section(string label) => new Item { isSection = true, title = label };

            public static Item Note(string label) => new Item { isNote = true, title = label };
        }

        [SerializeField] TMP_Text rootTitle;
        [SerializeField] GameObject header;
        [SerializeField] Button backButton;
        [SerializeField] TMP_Text backLabel;
        [SerializeField] TMP_Text headerTitle;
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform content;
        [SerializeField] RecordRowView rowPrefab;
        [SerializeField] ListSectionView sectionPrefab;
        [SerializeField] TMP_Text emptyLabel;

        readonly List<GameObject> rows = new List<GameObject>();

        public float ScrollPosition => scroll.verticalNormalizedPosition;

        /// <param name="backText">null이면 앱 최상위 화면(큰 제목), 아니면 뒤로가기가 있는 하위 화면.</param>
        public void Show(string title, string backText, List<Item> items, string emptyText, UITheme theme,
            Action onBack, float? scrollPosition)
        {
            bool isRoot = backText == null;
            rootTitle.gameObject.SetActive(isRoot);
            rootTitle.text = isRoot ? title : string.Empty;
            header.SetActive(!isRoot);
            headerTitle.text = isRoot ? string.Empty : title;
            backLabel.text = isRoot ? string.Empty : theme.backGlyph + " " + backText;
            if (!isRoot)
                PhoneHeaderLayout.Fit(headerTitle, backLabel, theme);
            backButton.onClick.RemoveAllListeners();
            if (onBack != null)
                backButton.onClick.AddListener(() => onBack());

            foreach (var go in rows)
                UIPool.Discard(go);
            rows.Clear();

            int records = 0;
            foreach (var item in items)
            {
                if (item.isNote)
                {
                    var note = Instantiate(sectionPrefab, content);
                    note.Bind(item.title);
                    var label = note.GetComponentInChildren<TMP_Text>();
                    label.color = theme.phoneSubText;
                    label.font = theme.regularFont;
                    label.fontSize = Mathf.Min(label.fontSize, 14f);
                    rows.Add(note.gameObject);
                    continue;
                }
                if (item.isSection)
                {
                    var section = Instantiate(sectionPrefab, content);
                    section.Bind(item.title);
                    rows.Add(section.gameObject);
                    continue;
                }
                var row = Instantiate(rowPrefab, content);
                row.Bind(item, theme);
                rows.Add(row.gameObject);
                records++;
            }

            emptyLabel.gameObject.SetActive(records == 0);
            emptyLabel.text = emptyText;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.verticalNormalizedPosition = scrollPosition ?? 1f;
        }
    }

    /// <summary>
    /// 휴대전화 폭이 줄어들면 고정 머리말 여백을 줄이고 글자 크기를 조금만 자동 조절한다.
    /// 넓은 화면에서는 빌더의 140px 여백을 그대로 유지한다.
    /// </summary>
    static class PhoneHeaderLayout
    {
        public static void Fit(TMP_Text centered, TMP_Text backLabel, UITheme theme, params TMP_Text[] companions)
        {
            if (centered == null || backLabel == null)
                return;
            Canvas.ForceUpdateCanvases();
            var header = centered.transform.parent as RectTransform;
            if (header == null || header.rect.width <= 0f)
                return;

            float margin = Mathf.Clamp((header.rect.width - 150f) * 0.5f, 94f, 140f);
            SetMargins(centered, margin);
            centered.enableAutoSizing = true;
            centered.fontSizeMin = Mathf.Min(13f, centered.fontSize);
            centered.fontSizeMax = centered.fontSize;

            if (companions != null)
                foreach (var label in companions)
                    if (label != null)
                        SetMargins(label, margin);

            var back = backLabel.transform.parent as RectTransform;
            if (back != null)
            {
                float preferred = backLabel.GetPreferredValues(backLabel.text).x + 18f;
                back.sizeDelta = new Vector2(Mathf.Clamp(preferred, 52f, margin - 8f), back.sizeDelta.y);
                RoundBack(back, theme);
            }
            backLabel.enableAutoSizing = true;
            backLabel.fontSizeMin = Mathf.Min(12f, backLabel.fontSize);
            backLabel.fontSizeMax = backLabel.fontSize;
        }

        /// <summary>중앙 제목 배치는 유지하면서 뒤로가기 호버 영역만 문구 길이의 타원형으로 줄인다.</summary>
        public static void FitBack(TMP_Text backLabel, UITheme theme, float maxWidth = 132f)
        {
            if (backLabel == null)
                return;
            var back = backLabel.transform.parent as RectTransform;
            if (back == null)
                return;
            float preferred = backLabel.GetPreferredValues(backLabel.text).x + 18f;
            back.sizeDelta = new Vector2(Mathf.Clamp(preferred, 52f, maxWidth), back.sizeDelta.y);
            RoundBack(back, theme);
        }

        static void RoundBack(RectTransform back, UITheme theme)
        {
            var background = back.GetComponent<Image>();
            if (background == null || theme == null || theme.roundedSprite == null)
                return;
            background.sprite = theme.roundedSprite;
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 24f / 17f;
        }

        static void SetMargins(TMP_Text label, float margin)
        {
            var rt = (RectTransform)label.transform;
            rt.offsetMin = new Vector2(margin, rt.offsetMin.y);
            rt.offsetMax = new Vector2(-margin, rt.offsetMax.y);
        }
    }
}
