using System;
using Intersection.Core;
using Intersection.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>사진 그리드의 한 칸. 모든 사진이 같은 컴포넌트·같은 표시 규칙을 쓴다.</summary>
    public class PhotoTileView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] RawImage image;
        [SerializeField] Image glyph;
        [SerializeField] SelectableRecord selectable;

        GameObject viewedBadge;

        public void Bind(PhotoData photo, UITheme theme, bool viewed, Action onClick)
        {
            selectable.Bind(RecordRef.ForPhoto(photo));
            PhotoVisual.Apply(photo, image, glyph, theme, true);
            EnsureViewedBadge(theme);
            viewedBadge.SetActive(viewed);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());
        }

        void EnsureViewedBadge(UITheme theme)
        {
            if (viewedBadge != null)
                return;
            viewedBadge = new GameObject("ViewedBadge", typeof(RectTransform), typeof(Image));
            var badge = (RectTransform)viewedBadge.transform;
            badge.SetParent(transform, false);
            badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(1f, 0f);
            badge.anchoredPosition = new Vector2(-6f, 6f);
            badge.sizeDelta = new Vector2(22f, 22f);
            var background = viewedBadge.GetComponent<Image>();
            background.sprite = theme.circleSprite;
            background.color = new Color(0f, 0f, 0f, 0.65f);
            background.raycastTarget = false;

            var markObject = new GameObject("Mark", typeof(RectTransform), typeof(Image));
            var mark = (RectTransform)markObject.transform;
            mark.SetParent(badge, false);
            mark.anchorMin = Vector2.zero;
            mark.anchorMax = Vector2.one;
            mark.offsetMin = new Vector2(4f, 4f);
            mark.offsetMax = new Vector2(-4f, -4f);
            var markImage = markObject.GetComponent<Image>();
            markImage.sprite = theme.selectionCheck;
            markImage.color = theme.phoneSubText;
            markImage.preserveAspect = true;
            markImage.raycastTarget = false;
        }
    }
}
