using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>업무 알림 한 줄. 읽지 않은 알림은 앞에 점과 진한 글자로 구분한다.</summary>
    public class NotificationRowView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] GameObject unreadDot;
        [SerializeField] TMP_Text label;

        public void Bind(string text, bool read, Color unreadColor, Color readColor, Action onClick)
        {
            label.text = text;
            label.color = read ? readColor : unreadColor;
            unreadDot.SetActive(!read);
            button.onClick.RemoveAllListeners();
            if (onClick != null)
                button.onClick.AddListener(() => onClick());
        }
    }
}
