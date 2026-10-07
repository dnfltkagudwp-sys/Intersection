using System;
using System.Collections.Generic;
using Intersection.Core;
using Intersection.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 메시지 앱 최상위 화면: 대화방 목록과 앱 내부 검색 결과.
    /// 행은 모두 데이터에서 생성하며, 일반 대화와 핵심 대화가 같은 행 컴포넌트를 쓴다.
    /// </summary>
    public class MessageListView : MonoBehaviour
    {
        [SerializeField] TMP_InputField searchField;
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform content;
        [SerializeField] ThreadRowView rowPrefab;
        [SerializeField] ListSectionView sectionPrefab;
        [SerializeField] TMP_Text emptyLabel;

        readonly List<GameObject> items = new List<GameObject>();

        public event Action<string> QueryChanged;

        public float ScrollPosition => scroll.verticalNormalizedPosition;

        void Awake() => searchField.onValueChanged.AddListener(q => QueryChanged?.Invoke(q));

        /// <summary>기기를 바꿀 때 그 기기의 검색어로 입력창을 맞춘다(변경 이벤트 없이).</summary>
        public void SetQuery(string query)
        {
            if (searchField.text != (query ?? string.Empty))
                searchField.SetTextWithoutNotify(query ?? string.Empty);
        }

        public void Show(List<ThreadEntry> entries, UIText text, PhoneTime time, UITheme theme,
            Action<ThreadEntry, MessageData> onOpen, float? scrollPosition)
        {
            Clear();
            foreach (var entry in entries)
                AddRow(entry, null, text, time, theme, onOpen);
            Finish(entries.Count == 0, text.Get(UIKeys.MessagesEmpty), scrollPosition);
        }

        public void ShowResults(List<SearchResult> threadResults, List<SearchResult> messageResults,
            UIText text, PhoneTime time, UITheme theme, Action<ThreadEntry, MessageData> onOpen, float? scrollPosition)
        {
            Clear();
            if (threadResults.Count > 0)
            {
                AddSection(text.Get(UIKeys.SearchSectionThreads));
                foreach (var r in threadResults)
                    AddRow(r.entry, null, text, time, theme, onOpen);
            }
            if (messageResults.Count > 0)
            {
                AddSection(text.Get(UIKeys.SearchSectionMessages));
                foreach (var r in messageResults)
                    AddRow(r.entry, r.message, text, time, theme, onOpen);
            }
            Finish(threadResults.Count + messageResults.Count == 0, text.Get(UIKeys.SearchEmpty), scrollPosition);
        }

        void Clear()
        {
            foreach (var go in items)
                UIPool.Discard(go);
            items.Clear();
        }

        void AddSection(string label)
        {
            var section = Instantiate(sectionPrefab, content);
            section.Bind(label);
            items.Add(section.gameObject);
        }

        /// <summary>message가 null이면 대화방 행(마지막 메시지 미리보기), 아니면 해당 메시지를 보여주는 검색 결과 행.</summary>
        void AddRow(ThreadEntry entry, MessageData message, UIText text, PhoneTime time, UITheme theme,
            Action<ThreadEntry, MessageData> onOpen)
        {
            var row = Instantiate(rowPrefab, content);
            var shown = message ?? entry.last;
            row.Bind(entry.displayName,
                message == null ? DeviceQuery.Preview(shown, text) : DeviceQuery.DisplayText(message, text).Replace('\n', ' '),
                time.ListLabel(shown.time),
                theme.chevronGlyph,
                message == null && entry.state.unreadCount > 0,
                entry.state.muted,
                theme.mutedIcon,
                () => onOpen(entry, message));
            items.Add(row.gameObject);
        }

        void Finish(bool empty, string emptyText, float? scrollPosition)
        {
            emptyLabel.gameObject.SetActive(empty);
            emptyLabel.text = emptyText;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.verticalNormalizedPosition = scrollPosition ?? 1f;
        }
    }
}
