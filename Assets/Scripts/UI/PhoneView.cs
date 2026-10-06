using Intersection.Core;
using TMPro;
using UnityEngine;

namespace Intersection.UI
{
    /// <summary>휴대전화 프레임: 상태바 시계와 앱 화면 전환.</summary>
    public class PhoneView : MonoBehaviour
    {
        [SerializeField] TMP_Text clockLabel;
        [SerializeField] MessageListView messageList;
        [SerializeField] ChatView chat;

        IClock clock;
        UIText text;
        PhoneTimeFormat format;
        int lastMinute = -1;

        public MessageListView MessageList => messageList;
        public ChatView Chat => chat;

        struct PhoneTimeFormat
        {
            public string pattern;
            public System.Globalization.CultureInfo culture;
        }

        public void Init(IClock clock, UIText text, System.Globalization.CultureInfo culture)
        {
            this.clock = clock;
            this.text = text;
            format = new PhoneTimeFormat { pattern = text.Get(UIKeys.FormatStatusClock), culture = culture };
            RefreshClock(true);
        }

        public void ShowList()
        {
            messageList.gameObject.SetActive(true);
            chat.gameObject.SetActive(false);
        }

        public void ShowChat()
        {
            messageList.gameObject.SetActive(false);
            chat.gameObject.SetActive(true);
        }

        void Update() => RefreshClock(false);

        void OnApplicationFocus(bool focus)
        {
            if (focus)
                RefreshClock(true);
        }

        void RefreshClock(bool force)
        {
            if (clock == null)
                return;
            var now = clock.Now;
            int minute = now.Hour * 60 + now.Minute;
            if (!force && minute == lastMinute)
                return;
            lastMinute = minute;
            clockLabel.text = now.ToString(format.pattern, format.culture);
        }
    }
}
