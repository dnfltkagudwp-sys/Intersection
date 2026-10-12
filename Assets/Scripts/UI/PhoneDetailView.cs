using System;
using System.Collections.Generic;
using System.Linq;
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
        [SerializeField] RectTransform documentRoot;
        [SerializeField] TMP_Text documentBlockTemplate;
        [SerializeField] TMP_Text headline;
        [SerializeField] RectTransform fieldRoot;
        [SerializeField] InfoRowView fieldPrefab;
        [SerializeField] SelectableRecord selectable;

        readonly List<GameObject> fields = new List<GameObject>();
        readonly List<GameObject> blocks = new List<GameObject>();

        /// <param name="selection">열린 기록(페이지·장소·파일·설정 변경) 자체. 선택 모드에서 화면 전체로 선택한다.</param>
        /// <param name="document">미리보기 자리에 그릴 문서 블록(파일 visibleText). 검색과 같은 원본이다.</param>
        public void Show(string title, string backText, string headlineText, Hero hero,
            List<(string label, string value)> fieldValues, UITheme theme, Action onBack, RecordRef selection,
            IReadOnlyList<FileTextBlock> document = null)
        {
            ShowDocument(document, theme);
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

        /// <summary>
        /// 파일 미리보기 문서. 문단은 본문 글꼴, 로그·표는 고정폭 글꼴로 그린다.
        /// 데이터의 `&lt;` 같은 글자가 서식으로 해석되지 않도록 서식 태그는 끈 채 그대로 보여준다.
        /// </summary>
        void ShowDocument(IReadOnlyList<FileTextBlock> document, UITheme theme)
        {
            foreach (var go in blocks)
                UIPool.Discard(go);
            blocks.Clear();
            bool any = document != null && document.Count > 0;
            documentRoot.gameObject.SetActive(any);
            if (!any)
                return;
            foreach (var block in document)
            {
                var t = Instantiate(documentBlockTemplate, documentRoot);
                t.gameObject.SetActive(true);
                t.richText = false;
                bool mono = block.kind != FileTextKind.Text;
                t.font = mono ? theme.monoFont : theme.regularFont;
                t.text = block.kind == FileTextKind.Table ? TableText(block) : string.Join("\n", block.Lines());
                blocks.Add(t.gameObject);
            }
        }

        /// <summary>표를 고정폭 글꼴에서 칸이 맞도록 공백으로 채운다(한글 등 전각 글자는 두 칸).</summary>
        static string TableText(FileTextBlock block)
        {
            var rows = block.Rows().ToList();
            int columns = rows.Count == 0 ? 0 : rows.Max(r => r.Length);
            var widths = new int[columns];
            foreach (var r in rows)
                for (int i = 0; i < r.Length; i++)
                    widths[i] = Math.Max(widths[i], DisplayWidth(r[i]));
            var sb = new System.Text.StringBuilder();
            foreach (var r in rows)
            {
                if (sb.Length > 0)
                    sb.Append('\n');
                for (int i = 0; i < r.Length; i++)
                {
                    sb.Append(r[i]);
                    if (i < r.Length - 1)
                        sb.Append(' ', widths[i] - DisplayWidth(r[i]) + 2);
                }
            }
            return sb.ToString();
        }

        static int DisplayWidth(string s)
        {
            int w = 0;
            foreach (char c in s)
                w += c >= 0x1100 && (c <= 0x115F || (c >= 0x2E80 && c <= 0xA4CF) || (c >= 0xAC00 && c <= 0xD7A3)
                                     || (c >= 0xF900 && c <= 0xFAFF) || (c >= 0xFF00 && c <= 0xFF60)) ? 2 : 1;
            return w;
        }
    }
}
