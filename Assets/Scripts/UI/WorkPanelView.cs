using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Intersection.UI
{
    /// <summary>
    /// 우측 업무 기록. 휴대전화 안에 넣지 않는 기록 ID·사망일 기준 시점·출처를 여기서만 보여준다.
    /// 핀·작업메모·비교는 UI-05/06 범위라 프리팹에서 비활성 상태로 둔다.
    /// </summary>
    public class WorkPanelView : MonoBehaviour
    {
        [SerializeField] TMP_Text recordTitle;
        [SerializeField] RectTransform infoRoot;
        [SerializeField] InfoRowView rowPrefab;

        readonly List<InfoRowView> rows = new List<InfoRowView>();

        public struct Row
        {
            public string label;
            public string value;
            public TMP_FontAsset font;
        }

        public void Show(string title, IEnumerable<Row> info)
        {
            recordTitle.text = title;
            foreach (var r in rows)
                Destroy(r.gameObject);
            rows.Clear();
            foreach (var row in info)
            {
                if (string.IsNullOrEmpty(row.value))
                    continue;
                var view = Instantiate(rowPrefab, infoRoot);
                view.Bind(row.label, row.value, row.font);
                rows.Add(view);
            }
        }
    }
}
