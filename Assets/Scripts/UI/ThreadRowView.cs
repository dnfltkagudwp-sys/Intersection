using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>메시지 앱 대화 목록의 한 행. 핵심 대화와 일상 대화가 같은 컴포넌트를 쓴다.</summary>
    public class ThreadRowView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text preview;
        [SerializeField] TMP_Text date;
        [SerializeField] TMP_Text chevron;
        [SerializeField] GameObject unreadDot;
        [SerializeField] Image mutedIcon;

        public void Bind(string titleText, string previewText, string dateText, string chevronGlyph,
            bool unread, bool muted, Sprite mutedSprite, Action onClick)
        {
            title.text = titleText;
            preview.text = previewText;
            date.text = dateText;
            chevron.text = chevronGlyph;
            unreadDot.SetActive(unread);
            // 알림 끔 아이콘은 테마에 아이콘이 있을 때만 표시한다.
            mutedIcon.gameObject.SetActive(muted && mutedSprite != null);
            mutedIcon.sprite = mutedSprite;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());
        }
    }
}
