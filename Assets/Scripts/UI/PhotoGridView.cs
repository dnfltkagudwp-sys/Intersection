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
            Action onBack, Action<PhotoData> onOpen, float? scrollPosition)
        {
            title.text = albumTitle;
            backLabel.text = theme.backGlyph + " " + backText;
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(() => onBack());

            foreach (var go in items)
                UIPool.Discard(go);
            items.Clear();

            Canvas.ForceUpdateCanvases();
            int columns = Mathf.Max(1, theme.photoGridColumns);
            float spacing = theme.photoGridSpacing;
            float cell = Mathf.Floor((content.rect.width - spacing * (columns - 1)) / columns);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.spacing = new Vector2(spacing, spacing);
            grid.cellSize = new Vector2(cell, cell);

            foreach (var photo in photos)
            {
                var p = photo;
                var tile = Instantiate(tilePrefab, content);
                items.Add(tile.gameObject);
                // 잘라내기 영역 계산을 위해 셀 크기를 먼저 맞춘다.
                ((RectTransform)tile.transform).sizeDelta = grid.cellSize;
                tile.Bind(photo, theme, () => onOpen(p));
            }

            emptyLabel.gameObject.SetActive(photos.Count == 0);
            emptyLabel.text = emptyText;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.verticalNormalizedPosition = scrollPosition ?? 0f;
        }
    }
}
