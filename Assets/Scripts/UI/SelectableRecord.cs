using Intersection.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 선택 가능한 화면 요소. 평상시에는 아무것도 표시하지 않고,
    /// 선택 모드에서만 호버·키보드 포커스에 얇은 테두리, 선택 항목에 작은 체크, 핀한 항목에 작은 핀 표시를 보여준다.
    /// </summary>
    public class SelectableRecord : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] Button button;
        [Tooltip("켜면 이 버튼은 선택 모드에서 선택에만 쓰인다 (말풍선·상세 화면). 끄면 기존 이동 버튼을 함께 쓴다.")]
        [SerializeField] bool selectOnClick;
        [SerializeField] GameObject outline;
        [SerializeField] GameObject check;
        [SerializeField] GameObject pinMark;

        RecordRef target;
        bool hover;
        bool focus;

        public RecordRef Target => target;

        void Awake()
        {
            if (selectOnClick && button != null)
            {
                button.onClick.AddListener(() =>
                {
                    if (SelectionBus.Active && target != null)
                        SelectionBus.SelectRequested?.Invoke(target);
                });
            }
        }

        void OnEnable()
        {
            SelectionBus.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            SelectionBus.Changed -= Refresh;
            hover = focus = false;
        }

        /// <summary>null이면 이 요소는 선택 대상이 아니다 (예: 폴더·앨범).</summary>
        public void Bind(RecordRef record)
        {
            target = record;
            Refresh();
        }

        public void Refresh()
        {
            bool on = SelectionBus.Active && target != null;
            bool selected = on && SelectionBus.SelectedKey == target.Key;
            outline.SetActive(on && (hover || focus || selected));
            check.SetActive(selected);
            pinMark.SetActive(on && SelectionBus.IsPinned(target.Key));
            if (selectOnClick && button != null)
            {
                // 선택 전용 버튼은 선택 모드에서만 눌리고 키보드 이동 대상이 된다.
                button.interactable = on;
                if (button.targetGraphic != null)
                    button.targetGraphic.raycastTarget = on;
            }
        }

        public void OnPointerEnter(PointerEventData e) { hover = true; Refresh(); }
        public void OnPointerExit(PointerEventData e) { hover = false; Refresh(); }
        public void OnSelect(BaseEventData e) { focus = true; Refresh(); }
        public void OnDeselect(BaseEventData e) { focus = false; Refresh(); }
    }
}
