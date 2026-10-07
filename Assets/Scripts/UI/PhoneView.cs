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
        [SerializeField] AlbumListView albumList;
        [SerializeField] PhotoGridView photoGrid;
        [SerializeField] PhotoDetailView photoDetail;

        IClock clock;
        UIText text;
        PhoneTimeFormat format;
        int lastMinute = -1;

        public MessageListView MessageList => messageList;
        public ChatView Chat => chat;
        public AlbumListView AlbumList => albumList;
        public PhotoGridView PhotoGrid => photoGrid;
        public PhotoDetailView PhotoDetail => photoDetail;

        public enum Screen
        {
            MessageList,
            Chat,
            AlbumList,
            PhotoGrid,
            PhotoDetail,
        }

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

        /// <summary>한 번에 한 앱의 한 화면만 보인다.</summary>
        public void Show(Screen screen)
        {
            messageList.gameObject.SetActive(screen == Screen.MessageList);
            chat.gameObject.SetActive(screen == Screen.Chat);
            albumList.gameObject.SetActive(screen == Screen.AlbumList);
            photoGrid.gameObject.SetActive(screen == Screen.PhotoGrid);
            photoDetail.gameObject.SetActive(screen == Screen.PhotoDetail);
        }

        /// <summary>현재 보이는 화면.</summary>
        public Screen Current =>
            chat.gameObject.activeSelf ? Screen.Chat
            : albumList.gameObject.activeSelf ? Screen.AlbumList
            : photoGrid.gameObject.activeSelf ? Screen.PhotoGrid
            : photoDetail.gameObject.activeSelf ? Screen.PhotoDetail
            : Screen.MessageList;

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
