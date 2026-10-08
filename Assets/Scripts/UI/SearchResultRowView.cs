using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>전역 검색 결과 한 줄: 표시 제목 · 일치한 문구 일부 · 앱 · 출처 · 표시 날짜.</summary>
    public class SearchResultRowView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text snippet;
        [SerializeField] TMP_Text meta;

        public void Bind(string titleText, string snippetText, string metaText, Action onOpen)
        {
            title.text = titleText;
            snippet.text = snippetText ?? string.Empty;
            snippet.gameObject.SetActive(!string.IsNullOrEmpty(snippetText));
            meta.text = metaText;
            button.onClick.RemoveAllListeners();
            button.interactable = onOpen != null;
            if (onOpen != null)
                button.onClick.AddListener(() => onOpen());
        }
    }
}
