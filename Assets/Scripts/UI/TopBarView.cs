using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>상단 업무 바. 전역 검색·알림은 UI-07 범위라 프리팹에서 비활성 상태로 둔다.</summary>
    public class TopBarView : MonoBehaviour
    {
        [SerializeField] TMP_Text caseStatus;
        [SerializeField] TMP_Text indexStatus;
        [SerializeField] Image indexFill;

        public void Show(string caseStatusText, string indexStatusText, float indexProgress)
        {
            caseStatus.text = caseStatusText;
            indexStatus.text = indexStatusText;
            indexFill.fillAmount = Mathf.Clamp01(indexProgress);
        }
    }
}
