using System;
using System.Collections.Generic;
using Intersection.Core;
using Intersection.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>메시지 앱 최상위 화면: 대화방 목록. 행은 데이터에서 생성한다.</summary>
    public class MessageListView : MonoBehaviour
    {
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform content;
        [SerializeField] ThreadRowView rowPrefab;
        [SerializeField] TMP_Text emptyLabel;

        readonly List<ThreadRowView> rows = new List<ThreadRowView>();

        public float ScrollPosition => scroll.verticalNormalizedPosition;

        public void Show(List<ThreadEntry> entries, UIText text, PhoneTime time, UITheme theme,
            Action<ThreadEntry> onOpen, float? scrollPosition)
        {
            foreach (var r in rows)
                Destroy(r.gameObject);
            rows.Clear();

            foreach (var entry in entries)
            {
                var row = Instantiate(rowPrefab, content);
                var e = entry;
                row.Bind(entry.displayName,
                    DeviceQuery.Preview(entry.last, text),
                    time.ListLabel(entry.last.time),
                    theme.chevronGlyph,
                    entry.state.unreadCount > 0,
                    entry.state.muted,
                    theme.mutedIcon,
                    () => onOpen(e));
                rows.Add(row);
            }

            emptyLabel.gameObject.SetActive(entries.Count == 0);
            emptyLabel.text = text.Get(UIKeys.MessagesEmpty);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.verticalNormalizedPosition = scrollPosition ?? 1f;
        }
    }
}
