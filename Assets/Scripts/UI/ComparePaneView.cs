using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 비교 화면의 한쪽. 위는 기록 정보(기기 소유자·종류·출처·시점·중립 코드), 아래는 원본 표현이다.
    /// 원본 표현은 휴대전화와 같은 대화·사진·상세 화면을 읽기 전용으로 쓰고, 의뢰 요청은 업무 카드로 보여준다.
    /// 크기는 고정이며 내용이 길면 이 패널 안에서만 스크롤한다.
    /// </summary>
    public class ComparePaneView : MonoBehaviour
    {
        [SerializeField] TMP_Text ownerLabel;
        [SerializeField] RectTransform infoRoot;
        [SerializeField] InfoRowView rowPrefab;
        [SerializeField] GameObject screenRoot;
        [SerializeField] ChatView chat;
        [SerializeField] PhotoDetailView photo;
        [SerializeField] PhoneDetailView detail;
        [SerializeField] GameObject cardRoot;
        [SerializeField] ScrollRect cardScroll;
        [SerializeField] TMP_Text cardTitle;
        [SerializeField] TMP_Text cardBody;

        readonly List<GameObject> rows = new List<GameObject>();

        public ChatView Chat => chat;
        public ScrollRect CardScroll => cardScroll;

        public void SetMeta(string owner, IEnumerable<WorkPanelView.Row> info)
        {
            ownerLabel.text = owner;
            foreach (var r in rows)
                UIPool.Discard(r);
            rows.Clear();
            foreach (var row in info)
            {
                if (string.IsNullOrEmpty(row.value))
                    continue;
                var view = Instantiate(rowPrefab, infoRoot);
                view.Bind(row.label, row.value, row.font);
                rows.Add(view.gameObject);
            }
        }

        public ChatView ShowChat() => Mode(chat.gameObject).chat;
        public PhotoDetailView ShowPhoto() => Mode(photo.gameObject).photo;
        public PhoneDetailView ShowDetail() => Mode(detail.gameObject).detail;

        public void ShowCard(string title, string body)
        {
            Mode(cardRoot);
            cardTitle.text = title;
            cardBody.text = body ?? string.Empty;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(cardScroll.content);
            cardScroll.verticalNormalizedPosition = 1f;
        }

        ComparePaneView Mode(GameObject active)
        {
            screenRoot.SetActive(active != cardRoot);
            cardRoot.SetActive(active == cardRoot);
            chat.gameObject.SetActive(active == chat.gameObject);
            photo.gameObject.SetActive(active == photo.gameObject);
            detail.gameObject.SetActive(active == detail.gameObject);
            return this;
        }
    }
}
