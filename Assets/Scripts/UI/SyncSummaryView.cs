using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 연동 결과 요약. 검색·작업함·비교처럼 휴대전화 자리(중앙)를 덮는 PC 업무 화면 하나로,
    /// 연동으로 새로 접근할 수 있게 된 자료의 출처·앱·수량만 보여준다(증거 ID·중요도·추천·추론 없음).
    /// 분류 행은 그 의뢰의 기존 앱 목록을 연다. 휴대전화·우측 패널 상태는 건드리지 않으므로 닫으면 이전 화면이 그대로 남는다.
    /// </summary>
    public class SyncSummaryView : MonoBehaviour
    {
        [SerializeField] TMP_Text heading;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text countLabel;
        [SerializeField] RectTransform listRoot;
        [Tooltip("분류 한 줄 원형 (Label·Count 자식). 비활성으로 두고 복제해 쓴다.")]
        [SerializeField] GameObject rowTemplate;
        [SerializeField] TMP_Text emptyLabel;
        [SerializeField] Button openButton;
        [SerializeField] TMP_Text openLabel;
        [SerializeField] Button laterButton;
        [SerializeField] TMP_Text laterLabel;

        readonly List<GameObject> rows = new List<GameObject>();

        public class Row
        {
            public string label;
            public string count;
            public Action onOpen;
            public float height;
        }

        public bool IsOpen => gameObject.activeSelf;

        public void Open() => gameObject.SetActive(true);

        public void Close() => gameObject.SetActive(false);

        /// <summary>
        /// 작업 시작 직후의 짧은 처리 연출: 같은 중앙 화면에 처리 문구만 보여주고 분류·버튼은 숨긴다.
        /// 데이터는 이미 저장된 뒤이며, 연출이 끝나면 호출한 쪽이 닫거나 결과 요약으로 바꾼다.
        /// </summary>
        public void ShowProcessing(string headingText, string titleText)
        {
            ConfigureBody(22f, 80f);
            heading.text = headingText;
            title.text = titleText ?? string.Empty;
            countLabel.text = string.Empty;
            foreach (var r in rows)
                UIPool.Discard(r);
            rows.Clear();
            emptyLabel.gameObject.SetActive(false);
            openButton.gameObject.SetActive(false);
            laterButton.gameObject.SetActive(false);
            LayoutRebuilder.ForceRebuildLayoutImmediate(listRoot);
        }

        /// <summary>onOpenResults가 null이면 `연동 결과 보기`를 숨긴다(보여줄 자료가 없을 때).</summary>
        public void Show(string headingText, string titleText, string countText, string emptyText, IList<Row> items,
            string openText, Action onOpenResults, string laterText, Action onLater)
        {
            ConfigureBody(22f, 80f);
            heading.text = headingText;
            title.text = titleText;
            countLabel.text = countText;
            foreach (var r in rows)
                UIPool.Discard(r);
            rows.Clear();
            foreach (var item in items)
            {
                var row = Instantiate(rowTemplate, listRoot);
                row.SetActive(true);
                row.transform.Find("Label").GetComponent<TMP_Text>().text = item.label;
                row.transform.Find("Count").GetComponent<TMP_Text>().text = item.count;
                var button = row.GetComponent<Button>();
                if (item.height > 0f && row.TryGetComponent<LayoutElement>(out var element))
                    element.preferredHeight = item.height;
                button.onClick.RemoveAllListeners();
                var open = item.onOpen;
                button.interactable = open != null;
                if (open != null)
                    button.onClick.AddListener(() => open());
                rows.Add(row);
            }
            emptyLabel.text = emptyText ?? string.Empty;
            emptyLabel.gameObject.SetActive(items.Count == 0);

            openLabel.text = openText;
            openButton.gameObject.SetActive(onOpenResults != null);
            openButton.onClick.RemoveAllListeners();
            if (onOpenResults != null)
                openButton.onClick.AddListener(() => onOpenResults());
            laterLabel.text = laterText ?? string.Empty;
            laterButton.gameObject.SetActive(onLater != null);
            laterButton.onClick.RemoveAllListeners();
            if (onLater != null)
                laterButton.onClick.AddListener(() => onLater());
            LayoutRebuilder.ForceRebuildLayoutImmediate(listRoot);
        }

        /// <summary>연락·최종 처리처럼 본문이 긴 화면에서 본문 높이와 목록 시작점을 조절한다.</summary>
        public void ConfigureBody(float countHeight, float listTop)
        {
            var countRect = (RectTransform)countLabel.transform;
            countRect.sizeDelta = new Vector2(countRect.sizeDelta.x, countHeight);
            listRoot.offsetMax = new Vector2(listRoot.offsetMax.x, -listTop);
            LayoutRebuilder.ForceRebuildLayoutImmediate(listRoot);
        }
    }
}
