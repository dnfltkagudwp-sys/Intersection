using System;
using Intersection.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>브라우저·지도·파일·설정 목록의 한 행. 모든 기록이 같은 행 컴포넌트를 쓴다.</summary>
    public class RecordRowView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Image icon;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text subtitle;
        [SerializeField] TMP_Text trailing;
        [SerializeField] TMP_Text chevron;
        [SerializeField] RectTransform textBlock;

        const float TextLeftWithIcon = 58f;
        const float TextLeftNoIcon = 18f;

        public void Bind(PhoneListView.Item item, UITheme theme)
        {
            bool hasIcon = item.icon != null;
            icon.gameObject.SetActive(hasIcon);
            icon.sprite = item.icon;
            textBlock.offsetMin = new Vector2(hasIcon ? TextLeftWithIcon : TextLeftNoIcon, textBlock.offsetMin.y);

            title.text = item.title ?? string.Empty;
            bool hasSub = !string.IsNullOrEmpty(item.subtitle);
            subtitle.gameObject.SetActive(hasSub);
            subtitle.text = item.subtitle ?? string.Empty;
            // 부제가 없으면 제목을 행 가운데에 둔다.
            var titleRect = (RectTransform)title.transform;
            if (hasSub)
            {
                titleRect.anchorMin = new Vector2(0, 1);
                titleRect.anchorMax = new Vector2(1, 1);
                titleRect.offsetMin = new Vector2(0, -36);
                titleRect.offsetMax = new Vector2(0, -10);
            }
            else
            {
                titleRect.anchorMin = Vector2.zero;
                titleRect.anchorMax = Vector2.one;
                titleRect.offsetMin = Vector2.zero;
                titleRect.offsetMax = Vector2.zero;
            }
            title.alignment = TextAlignmentOptions.MidlineLeft;
            trailing.text = item.trailing ?? string.Empty;

            bool clickable = item.onClick != null;
            chevron.text = clickable ? theme.chevronGlyph : string.Empty;
            button.interactable = clickable;
            button.onClick.RemoveAllListeners();
            if (clickable)
                button.onClick.AddListener(() => item.onClick());
        }
    }
}
