using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 검토 진행 팝오버 (상단 업무 상태 컨트롤 아래). 온보딩 검토 묶음의 분류별 확인 수량과 `검토 계속`을 보여준다.
    /// 분류 행은 그 분류에서 아직 열지 않은 다음 원본을, `검토 계속`은 묶음 전체에서 다음 원본을 연다.
    /// 증거 ID·관계·정답은 표시하지 않는다. 바깥을 누르면 닫힌다.
    /// </summary>
    public class ReviewPopoverView : MonoBehaviour
    {
        [SerializeField] RectTransform card;
        [SerializeField] TMP_Text heading;
        [SerializeField] TMP_Text body;
        [SerializeField] Button blocker;
        [SerializeField] RectTransform listRoot;
        [Tooltip("분류 한 줄 원형 (Label·Count 자식). 비활성으로 두고 복제해 쓴다.")]
        [SerializeField] GameObject rowTemplate;
        [SerializeField] Button continueButton;
        [SerializeField] TMP_Text continueLabel;
        [SerializeField] float rowHeight = 40f;
        [SerializeField] float rowSpacing = 4f;
        [Tooltip("제목·버튼 등 목록 밖 높이 (설명 줄 제외)")]
        [SerializeField] float chromeHeight = 112f;
        [SerializeField] float bodyHeight = 22f;

        readonly List<GameObject> rows = new List<GameObject>();

        public class Row
        {
            public string label;
            public string count;
            public bool done;
            /// <summary>null이면 누를 수 없다(그 분류를 모두 확인함).</summary>
            public Action onOpen;
        }

        public struct Palette
        {
            public Color rowColor;
            public Color text;
            public Color doneText;
            public Color accent;
        }

        public bool IsOpen => gameObject.activeSelf;

        public void Open(Action onClose)
        {
            gameObject.SetActive(true);
            blocker.onClick.RemoveAllListeners();
            blocker.onClick.AddListener(() => onClose());
        }

        public void Close() => gameObject.SetActive(false);

        /// <summary>bodyText가 있으면 제목 아래 한 줄로 보여준다(완료 설명). onContinue가 null이면 `검토 계속`을 숨긴다.</summary>
        public void Show(string headingText, string bodyText, IList<Row> items, string continueText, Action onContinue, Palette palette)
        {
            heading.text = headingText;
            bool hasBody = !string.IsNullOrEmpty(bodyText);
            body.text = bodyText ?? string.Empty;
            body.gameObject.SetActive(hasBody);
            listRoot.offsetMax = new Vector2(listRoot.offsetMax.x, -(52f + (hasBody ? bodyHeight + 4f : 0f)));
            // `검토 계속`이 없으면 버튼 자리만큼 목록을 아래로 늘린다(카드 높이도 같이 줄어든다).
            listRoot.offsetMin = new Vector2(listRoot.offsetMin.x, onContinue != null ? 58f : 12f);

            foreach (var r in rows)
                UIPool.Discard(r);
            rows.Clear();
            foreach (var item in items)
            {
                var row = Instantiate(rowTemplate, listRoot);
                row.SetActive(true);
                var button = row.GetComponent<Button>();
                var label = row.transform.Find("Label").GetComponent<TMP_Text>();
                var count = row.transform.Find("Count").GetComponent<TMP_Text>();
                label.text = item.label;
                count.text = item.count;
                label.color = item.done ? palette.doneText : palette.text;
                count.color = item.done ? palette.doneText : palette.accent;
                row.GetComponent<Image>().color = palette.rowColor;
                button.interactable = item.onOpen != null;
                button.onClick.RemoveAllListeners();
                if (item.onOpen != null)
                {
                    var open = item.onOpen;
                    button.onClick.AddListener(() => open());
                }
                rows.Add(row);
            }

            continueLabel.text = continueText;
            continueButton.gameObject.SetActive(onContinue != null);
            continueButton.onClick.RemoveAllListeners();
            if (onContinue != null)
                continueButton.onClick.AddListener(() => onContinue());

            float list = items.Count * rowHeight + Mathf.Max(0, items.Count - 1) * rowSpacing;
            float height = chromeHeight + list + (hasBody ? bodyHeight + 4f : 0f) - (onContinue == null ? 46f : 0f);
            card.sizeDelta = new Vector2(card.sizeDelta.x, height);
            LayoutRebuilder.ForceRebuildLayoutImmediate(listRoot);
        }
    }
}
