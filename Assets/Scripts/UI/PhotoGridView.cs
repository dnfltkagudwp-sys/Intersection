using System;
using System.Collections.Generic;
using Intersection.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>앨범의 사진 그리드. 실제 사진 앱처럼 최신 사진이 보이는 아래쪽에서 열린다.</summary>
    public class PhotoGridView : MonoBehaviour
    {
        [SerializeField] Button backButton;
        [SerializeField] TMP_Text backLabel;
        [SerializeField] TMP_Text title;
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform content;
        [SerializeField] GridLayoutGroup grid;
        [SerializeField] PhotoTileView tilePrefab;
        [SerializeField] TMP_Text emptyLabel;

        readonly List<GameObject> items = new List<GameObject>();

        public float ScrollPosition => scroll.verticalNormalizedPosition;

        public void Show(string albumTitle, string backText, List<PhotoData> photos, UITheme theme, string emptyText,
            Action onBack, Action<PhotoData> onOpen, Func<PhotoData, bool> isViewed,
            Func<PhotoData, string> dateLabel, float? scrollPosition)
        {
            title.text = albumTitle;
            backLabel.text = theme.backGlyph + " " + backText;
            PhoneHeaderLayout.Fit(title, backLabel);
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(() => onBack());

            foreach (var go in items)
                UIPool.Discard(go);
            items.Clear();

            Canvas.ForceUpdateCanvases();
            int columns = Mathf.Max(1, theme.photoGridColumns);
            float spacing = theme.photoGridSpacing;
            float cell = Mathf.Floor((content.rect.width - spacing * (columns - 1)) / columns);
            EnsureGroupedLayout();

            float top = 4f;
            int start = 0;
            while (start < photos.Count)
            {
                int day = photos[start].takenAt.dayOffset;
                int end = start + 1;
                while (end < photos.Count && photos[end].takenAt.dayOffset == day)
                    end++;
                top += AddDateSection(photos, start, end, columns, spacing, cell, top, theme,
                    dateLabel != null ? dateLabel(photos[start]) : string.Empty, onOpen, isViewed);
                top += 12f;
                start = end;
            }
            content.sizeDelta = new Vector2(content.sizeDelta.x, photos.Count == 0 ? 0f : top - 4f);

            emptyLabel.gameObject.SetActive(photos.Count == 0);
            emptyLabel.text = emptyText;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.verticalNormalizedPosition = scrollPosition ?? 0f;
        }

        void EnsureGroupedLayout()
        {
            grid.enabled = false;
            var fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                fitter.enabled = false;
        }

        float AddDateSection(List<PhotoData> photos, int start, int end, int columns, float spacing, float cell,
            float top, UITheme theme, string date, Action<PhotoData> onOpen, Func<PhotoData, bool> isViewed)
        {
            const float headerHeight = 30f;
            int count = end - start;
            int rows = Mathf.CeilToInt(count / (float)columns);
            float gridHeight = rows * cell + Mathf.Max(0, rows - 1) * spacing;

            var section = (RectTransform)new GameObject("DateSection", typeof(RectTransform), typeof(LayoutElement)).transform;
            section.SetParent(content, false);
            section.anchorMin = new Vector2(0, 1);
            section.anchorMax = new Vector2(1, 1);
            section.pivot = new Vector2(0.5f, 1);
            section.anchoredPosition = new Vector2(0, -top);
            section.sizeDelta = new Vector2(0, headerHeight + gridHeight);
            items.Add(section.gameObject);

            var headerRt = (RectTransform)new GameObject("Date", typeof(RectTransform)).transform;
            headerRt.SetParent(section, false);
            headerRt.anchorMin = new Vector2(0, 1);
            headerRt.anchorMax = new Vector2(1, 1);
            headerRt.pivot = new Vector2(0.5f, 1);
            headerRt.anchoredPosition = new Vector2(0, -2f);
            headerRt.sizeDelta = new Vector2(0, 24f);
            var header = headerRt.gameObject.AddComponent<TextMeshProUGUI>();
            header.font = theme.boldFont;
            header.fontSize = 14f;
            header.color = theme.phoneSubText;
            header.alignment = TextAlignmentOptions.MidlineLeft;
            header.raycastTarget = false;
            header.text = date;

            var gridRoot = (RectTransform)new GameObject("Photos", typeof(RectTransform), typeof(GridLayoutGroup)).transform;
            gridRoot.SetParent(section, false);
            gridRoot.anchorMin = new Vector2(0, 1);
            gridRoot.anchorMax = new Vector2(1, 1);
            gridRoot.pivot = new Vector2(0.5f, 1);
            gridRoot.anchoredPosition = new Vector2(0, -headerHeight);
            gridRoot.sizeDelta = new Vector2(0, gridHeight);
            var dayGrid = gridRoot.GetComponent<GridLayoutGroup>();
            dayGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            dayGrid.constraintCount = columns;
            dayGrid.cellSize = new Vector2(cell, cell);
            dayGrid.spacing = new Vector2(spacing, spacing);
            dayGrid.childAlignment = TextAnchor.UpperLeft;

            for (int i = start; i < end; i++)
            {
                var photo = photos[i];
                var tile = Instantiate(tilePrefab, gridRoot);
                ((RectTransform)tile.transform).sizeDelta = dayGrid.cellSize;
                tile.Bind(photo, theme, isViewed != null && isViewed(photo), () => onOpen(photo));
            }
            return headerHeight + gridHeight;
        }
    }
}
