using System;
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

        public void Bind(PhotoData photo, UITheme theme, Action onClick)
        {
            PhotoVisual.Apply(photo, image, glyph, theme, true);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());
        }
    }
}
