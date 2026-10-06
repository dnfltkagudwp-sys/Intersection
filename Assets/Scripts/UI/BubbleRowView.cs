using Intersection.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 말풍선 한 줄. 본문 길이에 맞춰 말풍선 크기를 계산하고, 발신은 오른쪽·수신은 왼쪽에 둔다.
    /// </summary>
    public class BubbleRowView : MonoBehaviour
    {
        [SerializeField] LayoutElement layout;
        [SerializeField] RectTransform bubble;
        [SerializeField] Image bubbleImage;
        [SerializeField] TMP_Text body;
        [SerializeField] TMP_Text time;
        [SerializeField] TMP_Text sender;

        const float SideMargin = 12f;
        const float TimeGap = 6f;
        const float SenderHeight = 20f;

        public struct Model
        {
            public bool outgoing;
            public string text;
            public AttachmentKind attachment;
            public string timeLabel;   // null이면 시각 숨김
            public string senderLabel; // null이면 발신자명 숨김 (단체방 수신만)
            public float topGap;
        }

        public void Bind(Model m, UITheme theme, float rowWidth)
        {
            bool hasSender = !string.IsNullOrEmpty(m.senderLabel);
            sender.gameObject.SetActive(hasSender);
            sender.text = hasSender ? m.senderLabel : string.Empty;

            time.gameObject.SetActive(!string.IsNullOrEmpty(m.timeLabel));
            time.text = m.timeLabel ?? string.Empty;
            time.fontSize = theme.bubbleTimeSize;

            body.fontSize = theme.bubbleTextSize;
            body.text = m.text;

            Vector2 size;
            var pad = theme.bubblePadding;
            var bodyRect = (RectTransform)body.transform;
            bodyRect.offsetMin = new Vector2(pad.x, pad.y);
            bodyRect.offsetMax = new Vector2(-pad.x, -pad.y);
            if (m.attachment == AttachmentKind.None)
            {
                bubbleImage.color = m.outgoing ? theme.bubbleOutgoing : theme.bubbleIncoming;
                body.color = m.outgoing ? theme.bubbleOutgoingText : theme.bubbleIncomingText;
                body.alignment = TextAlignmentOptions.TopLeft;
                float maxText = rowWidth * theme.bubbleMaxWidthRatio - pad.x * 2f;
                Vector2 natural = body.GetPreferredValues(m.text, Mathf.Infinity, Mathf.Infinity);
                float width = Mathf.Min(natural.x, maxText);
                Vector2 wrapped = body.GetPreferredValues(m.text, width, Mathf.Infinity);
                width = Mathf.Ceil(Mathf.Min(Mathf.Max(wrapped.x, width), maxText)) + 1f;
                size = new Vector2(width + pad.x * 2f, Mathf.Ceil(wrapped.y) + pad.y * 2f);
            }
            else
            {
                // 첨부 실제 자산이 없으므로 교체 가능한 자리 표시 카드로 보여준다.
                bubbleImage.color = theme.attachmentPlaceholder;
                body.color = theme.phoneSubText;
                body.alignment = TextAlignmentOptions.Center;
                size = m.attachment == AttachmentKind.Image ? theme.imageAttachmentSize : theme.locationAttachmentSize;
            }

            float top = m.topGap + (hasSender ? SenderHeight : 0f);
            float x = m.outgoing ? 1f : 0f;
            bubble.anchorMin = bubble.anchorMax = new Vector2(x, 1f);
            bubble.pivot = new Vector2(x, 1f);
            bubble.sizeDelta = size;
            bubble.anchoredPosition = new Vector2(m.outgoing ? -SideMargin : SideMargin, -top);

            var senderRect = (RectTransform)sender.transform;
            senderRect.anchorMin = senderRect.anchorMax = new Vector2(0f, 1f);
            senderRect.pivot = new Vector2(0f, 1f);
            senderRect.sizeDelta = new Vector2(rowWidth - SideMargin * 2f, SenderHeight);
            senderRect.anchoredPosition = new Vector2(SideMargin + 6f, -m.topGap);

            var timeRect = (RectTransform)time.transform;
            timeRect.anchorMin = timeRect.anchorMax = new Vector2(x, 1f);
            timeRect.pivot = new Vector2(m.outgoing ? 1f : 0f, 0f);
            timeRect.sizeDelta = new Vector2(90f, theme.bubbleTimeSize + 4f);
            float timeX = SideMargin + size.x + TimeGap;
            timeRect.anchoredPosition = new Vector2(m.outgoing ? -timeX : timeX, -(top + size.y));
            time.alignment = m.outgoing ? TextAlignmentOptions.BottomRight : TextAlignmentOptions.BottomLeft;

            layout.preferredHeight = top + size.y;
        }
    }
}
