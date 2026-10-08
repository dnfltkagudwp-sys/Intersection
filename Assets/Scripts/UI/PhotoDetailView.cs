using System;
using Intersection.Core;
using Intersection.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 사진 한 장 보기. 휴대전화 안에는 사진과 촬영 날짜·시각만 보이고,
    /// 파일명·출처·기록 코드는 우측 업무 패널에만 표시한다.
    /// </summary>
    public class PhotoDetailView : MonoBehaviour
    {
        [SerializeField] Button backButton;
        [SerializeField] TMP_Text backLabel;
        [SerializeField] TMP_Text dateLabel;
        [SerializeField] TMP_Text timeLabel;
        [SerializeField] RawImage image;
        [SerializeField] AspectRatioFitter fitter;
        [SerializeField] Image glyph;
        [SerializeField] Button prevButton;
        [SerializeField] Button nextButton;
        [SerializeField] TMP_Text prevLabel;
        [SerializeField] TMP_Text nextLabel;
        [SerializeField] SelectableRecord selectable;

        public void Show(PhotoData photo, string backText, string date, string time, bool hasPrev, bool hasNext,
            UITheme theme, Action onBack, Action onPrev, Action onNext)
        {
            backLabel.text = theme.backGlyph + " " + backText;
            dateLabel.text = date;
            timeLabel.text = time;
            prevLabel.text = theme.backGlyph;
            nextLabel.text = theme.chevronGlyph;
            PhotoVisual.Apply(photo, image, glyph, theme, false, fitter);
            selectable.Bind(RecordRef.ForPhoto(photo));

            Wire(backButton, onBack, true);
            Wire(prevButton, onPrev, hasPrev);
            Wire(nextButton, onNext, hasNext);
        }

        static void Wire(Button button, Action action, bool enabled)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action());
            button.interactable = enabled;
            var group = button.GetComponent<CanvasGroup>();
            if (group != null)
                group.alpha = enabled ? 1f : 0.3f;
        }
    }
}
