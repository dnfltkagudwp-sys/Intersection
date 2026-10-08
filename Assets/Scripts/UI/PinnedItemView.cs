using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 우측 작업 기록 목록 한 줄. 핀한 기록은 핀 아이콘, 처리 후보는 보존·삭제 배지로 구분한다.
    /// 핀한 기록에는 작은 비교 버튼(`+ 비교` / `비교 중`)이 있어 우측 상세를 거치지 않고 비교 칸에 넣고 뺄 수 있다.
    /// 원본을 찾지 못하면 누락 상태로 표시한다.
    /// </summary>
    public class PinnedItemView : MonoBehaviour
    {
        public enum CompareState
        {
            /// <summary>핀하지 않았거나 비교할 수 없는 기록.</summary>
            Hidden,
            /// <summary>비교 칸에 넣을 수 있음.</summary>
            Add,
            /// <summary>비교 칸에 들어가 있음 (다시 누르면 뺌).</summary>
            InSlot,
            /// <summary>비교 칸이 가득 차서 지금은 넣을 수 없음.</summary>
            Disabled,
        }

        [SerializeField] Button button;
        [SerializeField] Image background;
        [SerializeField] GameObject pinIcon;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text meta;
        [SerializeField] TMP_Text badge;

        [Header("비교 버튼")]
        [SerializeField] Button compareButton;
        [SerializeField] Image compareBackground;
        [SerializeField] TMP_Text compareLabel;
        [SerializeField] CanvasGroup compareGroup;

        Color titleColor;
        bool captured;

        public void Bind(string titleText, string metaText, string badgeText, bool pinned, bool selected, bool missing,
            Color selectedColor, Color idleColor, Color missingColor, Action onClick)
        {
            if (!captured)
            {
                titleColor = title.color;
                captured = true;
            }
            pinIcon.SetActive(pinned);
            title.text = titleText;
            title.color = missing ? missingColor : titleColor;
            meta.text = metaText;
            badge.text = badgeText ?? string.Empty;
            badge.gameObject.SetActive(!string.IsNullOrEmpty(badgeText));
            background.color = selected ? selectedColor : idleColor;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());
        }

        public void BindCompare(CompareState state, string label, Action onCompare,
            Color onColor, Color onTextColor, Color idleColor, Color idleTextColor, float disabledAlpha)
        {
            compareButton.gameObject.SetActive(state != CompareState.Hidden);
            compareButton.onClick.RemoveAllListeners();
            if (state == CompareState.Hidden)
                return;
            bool on = state == CompareState.InSlot;
            compareLabel.text = label;
            compareBackground.color = on ? onColor : idleColor;
            compareLabel.color = on ? onTextColor : idleTextColor;
            bool enabled = state != CompareState.Disabled;
            compareButton.interactable = enabled;
            compareGroup.alpha = enabled ? 1f : disabledAlpha;
            if (enabled && onCompare != null)
                compareButton.onClick.AddListener(() => onCompare());
        }
    }
}
