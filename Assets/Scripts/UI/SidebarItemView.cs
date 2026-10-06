using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>좌측 의뢰·앱 목록의 한 항목.</summary>
    public class SidebarItemView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Image background;
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text badge;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text detail;

        public void Bind(string badgeText, string titleText, string detailText, bool selected, bool interactable,
            Color selectedColor, Color idleColor, Action onClick)
        {
            if (badge != null)
                badge.text = badgeText;
            title.text = titleText;
            detail.text = detailText ?? string.Empty;
            detail.gameObject.SetActive(!string.IsNullOrEmpty(detailText));
            background.color = selected ? selectedColor : idleColor;
            button.interactable = interactable;
            group.alpha = interactable ? 1f : 0.42f;
            button.onClick.RemoveAllListeners();
            if (onClick != null)
                button.onClick.AddListener(() => onClick());
        }
    }
}
