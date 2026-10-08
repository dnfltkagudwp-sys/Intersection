using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 업무 알림 목록 (상단 `알림` 아래). 알림을 눌러도 앱이나 기록을 열지 않고 읽음으로만 바꾼다.
    /// 플레이어가 의뢰와 앱을 직접 고른다. 삭제 기능은 없다.
    /// </summary>
    public class NotificationPanelView : MonoBehaviour
    {
        [Tooltip("목록 카드. 높이는 알림 수에 맞춰 정한다.")]
        [SerializeField] RectTransform card;
        [SerializeField] TMP_Text heading;
        [SerializeField] Button markAllButton;
        [SerializeField] TMP_Text markAllLabel;
        [SerializeField] Button blocker;
        [SerializeField] RectTransform listRoot;
        [SerializeField] TMP_Text emptyLabel;
        [SerializeField] NotificationRowView rowPrefab;
        [SerializeField] float rowHeight = 48f;
        [SerializeField] float rowSpacing = 4f;
        [SerializeField] float chromeHeight = 68f;
        [SerializeField] float minHeight = 110f;
        [SerializeField] float maxHeight = 420f;

        readonly List<GameObject> rows = new List<GameObject>();

        public class Item
        {
            public string text;
            public bool read;
            public Action onClick;
        }

        public bool IsOpen => gameObject.activeSelf;

        public void Open(Action onClose)
        {
            gameObject.SetActive(true);
            blocker.onClick.RemoveAllListeners();
            blocker.onClick.AddListener(() => onClose());
        }

        public void Close() => gameObject.SetActive(false);

        public void Show(string headingText, string markAllText, string emptyText, IList<Item> items, Action onMarkAll,
            Color unreadColor, Color readColor)
        {
            heading.text = headingText;
            markAllLabel.text = markAllText;
            markAllButton.onClick.RemoveAllListeners();
            bool anyUnread = false;
            foreach (var i in items)
                anyUnread |= !i.read;
            markAllButton.interactable = anyUnread;
            if (anyUnread)
                markAllButton.onClick.AddListener(() => onMarkAll());
            foreach (var r in rows)
                UIPool.Discard(r);
            rows.Clear();
            foreach (var i in items)
            {
                var row = Instantiate(rowPrefab, listRoot);
                row.Bind(i.text, i.read, unreadColor, readColor, i.onClick);
                rows.Add(row.gameObject);
            }
            emptyLabel.text = emptyText;
            emptyLabel.gameObject.SetActive(items.Count == 0);
            // 알림 수에 맞춰 카드 높이를 정한다(많으면 최대 높이에서 목록만 스크롤).
            float content = items.Count * rowHeight + Mathf.Max(0, items.Count - 1) * rowSpacing;
            card.sizeDelta = new Vector2(card.sizeDelta.x, Mathf.Clamp(chromeHeight + content, minHeight, maxHeight));
            LayoutRebuilder.ForceRebuildLayoutImmediate(listRoot);
        }
    }
}
