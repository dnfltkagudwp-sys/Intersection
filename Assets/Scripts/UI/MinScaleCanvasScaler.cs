using Intersection.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 화면 크기에 맞춰 1920×1080 기준 UI를 늘리고 줄이되, 테마의 최소 배율(minUIScale) 아래로는 줄이지 않는다.
    /// 1366×768에서 0.71배로 줄면 우측 패널 글자가 8~9px로 작아지므로, 대신 기준 단위 화면을 넓혀
    /// 좌·우 패널은 같은 크기를 유지하고 중앙(휴대전화 높이에 맞춰 크기가 정해짐)이 줄어든다.
    /// </summary>
    [AddComponentMenu("Layout/Min Scale Canvas Scaler")]
    public class MinScaleCanvasScaler : CanvasScaler
    {
        [SerializeField] UITheme theme;

        protected override void HandleScaleWithScreenSize()
        {
            var canvas = GetComponent<Canvas>();
            Vector2 screen = canvas != null ? canvas.renderingDisplaySize : new Vector2(Screen.width, Screen.height);
            if (screen.x <= 0f || screen.y <= 0f)
            {
                base.HandleScaleWithScreenSize();
                return;
            }
            float logWidth = Mathf.Log(screen.x / m_ReferenceResolution.x, 2f);
            float logHeight = Mathf.Log(screen.y / m_ReferenceResolution.y, 2f);
            float scale = Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, m_MatchWidthOrHeight));
            float min = theme != null ? theme.minUIScale : 0f;
            SetScaleFactor(Mathf.Max(scale, min));
            SetReferencePixelsPerUnit(m_ReferencePixelsPerUnit);
        }
    }
}
