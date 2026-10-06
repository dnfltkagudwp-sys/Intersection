using TMPro;
using UnityEngine;

namespace Intersection.UI
{
    /// <summary>우측 업무 패널의 라벨·값 한 줄.</summary>
    public class InfoRowView : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] TMP_Text value;

        public void Bind(string labelText, string valueText, TMP_FontAsset valueFont)
        {
            label.text = labelText;
            value.text = valueText;
            value.font = valueFont;
        }
    }
}
