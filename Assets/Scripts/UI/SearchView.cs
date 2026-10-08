using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 전역 검색 결과. 휴대전화 앱이 아니라 PC 업무 화면으로 중앙을 덮고, 결과를 의뢰 · 원래 앱별로 묶어 보여준다.
    /// 휴대전화 탐색 상태와 우측 패널은 건드리지 않으므로 닫으면 검색 전 화면이 그대로 남는다.
    /// </summary>
    public class SearchView : MonoBehaviour
    {
        [SerializeField] TMP_Text heading;
        [SerializeField] TMP_Text countLabel;
        [SerializeField] Button closeButton;
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform listRoot;
        [SerializeField] TMP_Text emptyLabel;
        [SerializeField] SearchResultRowView rowPrefab;
        [SerializeField] ListSectionView sectionPrefab;

        readonly List<GameObject> items = new List<GameObject>();

        public class Row
        {
            public string title;
            /// <summary>일치 부분을 강조한 리치 텍스트 (원문은 noparse로 감싼다).</summary>
            public string snippet;
            public string meta;
            public Action onOpen;
        }

        public class Group
        {
            public string header;
            public List<Row> rows = new List<Row>();
        }

        public bool IsOpen => gameObject.activeSelf;

        public void Open(Action onClose)
        {
            gameObject.SetActive(true);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => onClose());
        }

        public void Close() => gameObject.SetActive(false);

        /// <summary>결과를 다시 그린다. resetScroll이면 맨 위에서 시작한다(검색어가 바뀐 경우).</summary>
        public void Show(string headingText, string countText, string emptyText, IList<Group> groups, bool resetScroll)
        {
            heading.text = headingText;
            countLabel.text = countText ?? string.Empty;
            foreach (var go in items)
                UIPool.Discard(go);
            items.Clear();
            foreach (var g in groups)
            {
                var section = Instantiate(sectionPrefab, listRoot);
                section.Bind(g.header);
                items.Add(section.gameObject);
                foreach (var r in g.rows)
                {
                    var row = Instantiate(rowPrefab, listRoot);
                    row.Bind(r.title, r.snippet, r.meta, r.onOpen);
                    items.Add(row.gameObject);
                }
            }
            emptyLabel.text = emptyText ?? string.Empty;
            emptyLabel.gameObject.SetActive(groups.Count == 0 && !string.IsNullOrEmpty(emptyText));
            LayoutRebuilder.ForceRebuildLayoutImmediate(listRoot);
            if (resetScroll)
            {
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = 1f;
            }
        }
    }
}
