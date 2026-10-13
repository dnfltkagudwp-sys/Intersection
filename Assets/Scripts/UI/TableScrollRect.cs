using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 파일 미리보기 표처럼 가로로만 넘기는 영역. 세로 휠·세로 끌기는 바깥(세로) 스크롤로 넘겨
    /// 표 위에 포인터가 있어도 문서 전체를 계속 내릴 수 있게 한다. 가로 휠(트랙패드)·Shift+휠·가로 끌기만 표를 넘긴다.
    /// </summary>
    public class TableScrollRect : ScrollRect
    {
        bool toParent;

        public override void OnScroll(PointerEventData data)
        {
            var d = data.scrollDelta;
            bool shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
            if (!shift && Mathf.Abs(d.y) >= Mathf.Abs(d.x))
            {
                Forward(data, ExecuteEvents.scrollHandler);
                return;
            }
            base.OnScroll(data);
        }

        public override void OnInitializePotentialDrag(PointerEventData data)
        {
            Forward(data, ExecuteEvents.initializePotentialDrag);
            base.OnInitializePotentialDrag(data);
        }

        public override void OnBeginDrag(PointerEventData data)
        {
            toParent = Mathf.Abs(data.delta.y) > Mathf.Abs(data.delta.x);
            if (toParent)
                Forward(data, ExecuteEvents.beginDragHandler);
            else
                base.OnBeginDrag(data);
        }

        public override void OnDrag(PointerEventData data)
        {
            if (toParent)
                Forward(data, ExecuteEvents.dragHandler);
            else
                base.OnDrag(data);
        }

        public override void OnEndDrag(PointerEventData data)
        {
            if (toParent)
                Forward(data, ExecuteEvents.endDragHandler);
            else
                base.OnEndDrag(data);
            toParent = false;
        }

        void Forward<T>(PointerEventData data, ExecuteEvents.EventFunction<T> handler) where T : IEventSystemHandler
        {
            if (transform.parent != null)
                ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, data, handler);
        }
    }
}
