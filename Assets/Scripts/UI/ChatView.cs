using System;
using System.Collections.Generic;
using Intersection.Core;
using Intersection.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>대화방 상세. 실제 문자 앱처럼 최신 메시지가 보이는 아래쪽에서 열린다.</summary>
    public class ChatView : MonoBehaviour
    {
        [SerializeField] Button backButton;
        [SerializeField] TMP_Text backLabel;
        [SerializeField] TMP_Text title;
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform content;
        [SerializeField] BubbleRowView bubblePrefab;
        [SerializeField] DateSeparatorView separatorPrefab;

        readonly List<GameObject> items = new List<GameObject>();

        public float ScrollPosition => scroll.verticalNormalizedPosition;

        public void Show(ThreadData thread, CaseData device, string displayName, string backText,
            GameConfig config, UIText text, PhoneTime time, Action onBack, float? scrollPosition)
        {
            var theme = config.theme;
            title.text = displayName;
            backLabel.text = theme.backGlyph + " " + backText;
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(() => onBack());

            foreach (var go in items)
                Destroy(go);
            items.Clear();

            Canvas.ForceUpdateCanvases();
            float rowWidth = content.rect.width;
            var messages = DeviceQuery.Ordered(thread);
            MessageData prev = null;
            for (int i = 0; i < messages.Count; i++)
            {
                var m = messages[i];
                var next = i + 1 < messages.Count ? messages[i + 1] : null;
                bool newSection = prev == null || m.time.TotalMinutes - prev.time.TotalMinutes >= config.separatorGapMinutes
                    || m.time.dayOffset != prev.time.dayOffset;
                if (newSection)
                {
                    var sep = Instantiate(separatorPrefab, content);
                    sep.Bind(time.Separator(m.time));
                    items.Add(sep.gameObject);
                }

                bool outgoing = m.sender == device.owner;
                bool sameAsPrev = !newSection && prev != null && prev.sender == m.sender;
                bool nextContinues = next != null && next.sender == m.sender
                    && next.time.TotalMinutes == m.time.TotalMinutes;

                var row = Instantiate(bubblePrefab, content);
                row.Bind(new BubbleRowView.Model
                {
                    outgoing = outgoing,
                    text = BodyText(m, text),
                    attachment = m.attachment,
                    // 같은 사람이 같은 분에 이어 보낸 메시지는 마지막 말풍선에만 시각을 붙인다.
                    timeLabel = nextContinues ? null : time.Time(m.time),
                    senderLabel = thread.IsGroup && !outgoing && !sameAsPrev ? DeviceQuery.ContactName(m.sender, device) : null,
                    topGap = newSection ? 0f : sameAsPrev ? theme.bubbleGroupGap : theme.bubbleSenderGap,
                }, theme, rowWidth);
                items.Add(row.gameObject);
                prev = m;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.verticalNormalizedPosition = scrollPosition ?? 0f;
        }

        static string BodyText(MessageData m, UIText text)
        {
            if (m.attachment == AttachmentKind.Image)
                return text.Get(UIKeys.AttachmentImage);
            if (m.attachment == AttachmentKind.Location)
                return string.IsNullOrEmpty(m.attachmentLabel) ? text.Get(UIKeys.AttachmentLocation) : m.attachmentLabel;
            return m.body;
        }
    }
}
