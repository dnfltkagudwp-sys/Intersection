using Intersection.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 사진 한 장을 RawImage에 표시하는 공통 규칙. 실제 이미지가 없으면 중립 자리 표시(회색 면 + 일반 그림 기호)를 쓴다.
    /// 자리 표시는 모든 사진에 같은 방식으로 적용되어 특정 사진만 눈에 띄지 않는다.
    /// </summary>
    public static class PhotoVisual
    {
        /// <param name="crop">true면 영역을 꽉 채우도록 가운데를 잘라낸다(썸네일). false면 비율을 유지해 맞춘다(한 장 보기).</param>
        public static void Apply(PhotoData photo, RawImage target, Image glyph, UITheme theme, bool crop,
            AspectRatioFitter fitter = null)
        {
            var tex = photo != null ? photo.image : null;
            glyph.gameObject.SetActive(tex == null);
            glyph.sprite = theme.photoGlyph;
            glyph.color = theme.photoPlaceholderGlyph;

            if (tex == null)
            {
                target.texture = null;
                target.uvRect = new Rect(0, 0, 1, 1);
                target.color = theme.photoPlaceholder;
                if (fitter != null)
                    fitter.aspectRatio = 3f / 4f;
                return;
            }

            target.texture = tex;
            target.color = Color.white;
            float aspect = (float)tex.width / tex.height;
            if (fitter != null)
                fitter.aspectRatio = aspect;
            if (!crop)
            {
                target.uvRect = new Rect(0, 0, 1, 1);
                return;
            }
            var rect = target.rectTransform.rect;
            float boxAspect = rect.height > 0 ? rect.width / rect.height : 1f;
            if (aspect > boxAspect)
            {
                float w = boxAspect / aspect;
                target.uvRect = new Rect((1 - w) / 2f, 0, w, 1);
            }
            else
            {
                float h = aspect / boxAspect;
                target.uvRect = new Rect(0, (1 - h) / 2f, 1, h);
            }
        }
    }
}
