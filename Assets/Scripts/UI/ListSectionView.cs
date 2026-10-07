using TMPro;
using UnityEngine;

namespace Intersection.UI
{
    /// <summary>목록 안의 구역 제목 (예: 검색 결과의 대화 / 메시지).</summary>
    public class ListSectionView : MonoBehaviour
    {
        [SerializeField] TMP_Text label;

        public void Bind(string text) => label.text = text;
    }
}
