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
    /// 브라우저·지도·파일·설정 기록의 상세 화면.
    /// 위쪽 영역(지도·미리보기·페이지)은 실제 자산이 없으면 중립 자리 표시만 보여준다.
    /// 휴대전화 안에는 기기에서 볼 수 있는 정보만 넣고, 기록 코드·출처는 업무 패널에 둔다.
    /// </summary>
    public class PhoneDetailView : MonoBehaviour
    {
        public class Hero
        {
            public Sprite background;
            public Texture texture;
            public Sprite glyph;
            public string message;
        }

        [SerializeField] Button backButton;
        [SerializeField] TMP_Text backLabel;
        [SerializeField] TMP_Text headerTitle;
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform content;
        [SerializeField] GameObject heroRoot;
        [SerializeField] Image heroBackground;
        [SerializeField] RawImage heroTexture;
        [SerializeField] Image heroGlyph;
        [SerializeField] TMP_Text heroMessage;
        [SerializeField] TMP_Text headline;
        [SerializeField] RectTransform fieldRoot;
        [SerializeField] InfoRowView fieldPrefab;
        [SerializeField] SelectableRecord selectable;

        readonly List<GameObject> fields = new List<GameObject>();

        /// <param name="selection">열린 기록(페이지·장소·파일·설정 변경) 자체. 선택 모드에서 화면 전체로 선택한다.</param>
        public void Show(string title, string backText, string headlineText, Hero hero,
            List<(string label, string value)> fieldValues, UITheme theme, Action onBack, RecordRef selection)
        {
            selectable.Bind(selection);
            headerTitle.text = title;
            backLabel.text = theme.backGlyph + " " + backText;
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(() => onBack());

            heroRoot.SetActive(hero != null);
            if (hero != null)
            {
                bool hasTexture = hero.texture != null;
                heroTexture.gameObject.SetActive(hasTexture);
                heroTexture.texture = hero.texture;
                heroBackground.sprite = hero.background;
                heroBackground.color = hero.background != null ? Color.white : theme.photoPlaceholder;
                heroGlyph.gameObject.SetActive(!hasTexture && hero.glyph != null);
                heroGlyph.sprite = hero.glyph;
                heroGlyph.color = hero.background != null ? theme.phoneAccent : theme.photoPlaceholderGlyph;
                heroMessage.gameObject.SetActive(!hasTexture && !string.IsNullOrEmpty(hero.message));
                heroMessage.text = hero.message ?? string.Empty;
            }

            headline.text = headlineText ?? string.Empty;
            headline.gameObject.SetActive(!string.IsNullOrEmpty(headlineText));

            foreach (var go in fields)
                UIPool.Discard(go);
            fields.Clear();
            foreach (var (label, value) in fieldValues)
            {
                if (string.IsNullOrEmpty(value))
                    continue;
                var row = Instantiate(fieldPrefab, fieldRoot);
                row.Bind(label, value, theme.regularFont);
                fields.Add(row.gameObject);
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.verticalNormalizedPosition = 1f;
        }
    }
}
