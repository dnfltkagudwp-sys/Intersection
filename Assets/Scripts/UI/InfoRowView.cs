using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>우측 업무 패널의 라벨·값 한 줄.</summary>
    public class InfoRowView : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] TMP_Text value;

        bool wrap;
        float baseHeight = -1f;

        public void Bind(string labelText, string valueText, TMP_FontAsset valueFont)
        {
            label.text = labelText;
            value.text = valueText;
            value.font = valueFont;
        }

        /// <summary>
        /// 값을 말줄임 없이 줄바꿈해 전체를 보여준다(메일 주소·첨부 목록처럼 잘리면 안 되는 값).
        /// 줄 높이는 레이아웃이 잡힌 뒤 <see cref="FitWrapped"/>로 맞춘다. 기본(끔)은 기존처럼 한 줄 말줄임.
        /// </summary>
        public void SetWrap(bool on)
        {
            wrap = on;
            value.textWrappingMode = on ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            value.overflowMode = on ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
        }

        /// <summary>줄바꿈 값이 라벨과 겹치지 않게 값 영역을 라벨 뒤에서 시작하고, 필요한 줄 수만큼 행 높이를 늘린다. 프리팹 높이보다 낮아지지 않는다.</summary>
        public bool FitWrapped()
        {
            if (!wrap || !TryGetComponent<LayoutElement>(out var layout))
                return false;
            const float gap = 16f, margin = 24f;
            var valueRt = value.rectTransform;
            float labelWidth = label.GetPreferredValues(label.text).x + gap;
            if (valueRt.offsetMin.x < labelWidth)
                valueRt.offsetMin = new Vector2(labelWidth, valueRt.offsetMin.y);
            float width = ((RectTransform)transform).rect.width - valueRt.offsetMin.x + valueRt.offsetMax.x;
            if (width <= 0f)
                return false;
            if (baseHeight < 0f)
                baseHeight = layout.preferredHeight;
            float needed = value.GetPreferredValues(value.text, width, 0f).y + margin;
            float height = Mathf.Max(baseHeight, needed);
            if (Mathf.Approximately(layout.preferredHeight, height))
                return false;
            layout.preferredHeight = height;
            return true;
        }
    }
}
