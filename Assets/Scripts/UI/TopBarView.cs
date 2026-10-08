using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 상단 업무 바: 현재 의뢰 · 업무 상태(진행 표시) · 전역 검색 · 업무 알림.
    /// 진행 표시는 실제 비율을 알 수 없으면 움직이는 띠(인디터미네이트)로, 끝났으면 가득 찬 막대로 보인다. 가짜 퍼센트는 없다.
    /// </summary>
    public class TopBarView : MonoBehaviour
    {
        [SerializeField] TMP_Text caseStatus;
        [SerializeField] TMP_Text indexStatus;
        [SerializeField] Image indexFill;
        [Tooltip("진행 중일 때 트랙 안을 오가는 띠")]
        [SerializeField] RectTransform indeterminate;

        [Header("전역 검색")]
        [SerializeField] TMP_InputField searchField;
        [SerializeField] CanvasGroup searchGroup;

        [Header("업무 알림")]
        [SerializeField] Button notificationsButton;
        [SerializeField] Image notificationsBackground;
        [SerializeField] GameObject badge;
        [SerializeField] TMP_Text badgeLabel;

        [Tooltip("인디터미네이트 띠가 트랙을 한 번 지나는 시간(초)")]
        [SerializeField] float sweepSeconds = 1.6f;

        bool running;

        public TMP_InputField SearchField => searchField;
        public bool IsSearchFocused => searchField != null && searchField.isFocused;

        public void Bind(Action<string> onSearchChanged, Action onSearchSelected, Action onNotifications)
        {
            searchField.onValueChanged.AddListener(v => onSearchChanged(v));
            searchField.onSelect.AddListener(_ => onSearchSelected());
            notificationsButton.onClick.AddListener(() => onNotifications());
        }

        /// <summary>indeterminate: 진행 중(비율 없음). progress: 끝났으면 1, 표시 없음이면 0.</summary>
        public void Show(string caseStatusText, string indexStatusText, bool indeterminateProgress, float progress)
        {
            caseStatus.text = caseStatusText;
            indexStatus.text = indexStatusText;
            running = indeterminateProgress;
            indeterminate.gameObject.SetActive(running);
            indexFill.fillAmount = running ? 0f : Mathf.Clamp01(progress);
        }

        /// <summary>비교 중에는 검색을 시작하지 않는다.</summary>
        public void SetSearchEnabled(bool on)
        {
            searchGroup.interactable = on;
            searchGroup.blocksRaycasts = on;
            searchGroup.alpha = on ? 1f : 0.45f;
        }

        public void SetSearchText(string value) => searchField.SetTextWithoutNotify(value ?? string.Empty);

        public void ReleaseSearchFocus()
        {
            if (searchField.isFocused)
                searchField.DeactivateInputField();
        }

        public void ShowUnread(int count, string countText, bool panelOpen, Color openColor, Color idleColor)
        {
            badge.SetActive(count > 0);
            badgeLabel.text = countText;
            notificationsBackground.color = panelOpen ? openColor : idleColor;
        }

        void Update()
        {
            if (!running)
                return;
            var track = (RectTransform)indeterminate.parent;
            float width = track.rect.width;
            float band = indeterminate.rect.width;
            float t = Mathf.Repeat(Time.unscaledTime / Mathf.Max(0.1f, sweepSeconds), 1f);
            indeterminate.anchoredPosition = new Vector2(Mathf.Lerp(-band, width, t), indeterminate.anchoredPosition.y);
        }
    }
}
