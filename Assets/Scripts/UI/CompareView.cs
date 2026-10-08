using System;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 비교 모드. 휴대전화 앱이 아니라 PC 업무 프로그램 화면으로, 중앙 영역을 덮고 두 기록을 같은 크기로 나란히 보여준다.
    /// 두 기록의 관계·일치 여부는 판정하지 않는다.
    /// </summary>
    public class CompareView : MonoBehaviour
    {
        [SerializeField] Button closeButton;
        [SerializeField] ComparePaneView left;
        [SerializeField] ComparePaneView right;

        public ComparePaneView Left => left;
        public ComparePaneView Right => right;
        public bool IsOpen => gameObject.activeSelf;

        /// <summary>패널을 채우기 전에 먼저 연다(레이아웃 크기가 정해져야 대화 말풍선 폭을 계산할 수 있다).</summary>
        public void Open(Action onClose)
        {
            gameObject.SetActive(true);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => onClose());
            Canvas.ForceUpdateCanvases();
        }

        public void Close() => gameObject.SetActive(false);
    }
}
