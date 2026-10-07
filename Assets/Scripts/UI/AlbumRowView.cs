using System;
using Intersection.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>앨범 목록의 한 행: 대표 사진(가장 최근), 앨범 이름, 사진 수.</summary>
    public class AlbumRowView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] RawImage cover;
        [SerializeField] Image coverGlyph;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text count;
        [SerializeField] TMP_Text chevron;

        public void Bind(string titleText, string countText, PhotoData coverPhoto, string chevronGlyph, UITheme theme, Action onClick)
        {
            title.text = titleText;
            count.text = countText;
            chevron.text = chevronGlyph;
            PhotoVisual.Apply(coverPhoto, cover, coverGlyph, theme, true);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());
        }
    }
}
