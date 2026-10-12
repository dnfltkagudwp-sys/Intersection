using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 상단 업무 바: 현재 의뢰 · 업무 상태 컨트롤(진행 표시) · 전역 검색 · 업무 알림.
    /// 업무 상태 컨트롤은 현재 의뢰의 통합 업무 상태를 한 줄로 보여주고, 누를 수 있는 상태(요청 확인·검토 진행·연동 시작·연동 결과)면 버튼이 된다.
    /// 진행 표시는 실제 비율을 알면 그 비율, 모르면 움직이는 띠(인디터미네이트), 끝났으면 가득 찬 막대로 보인다. 가짜 퍼센트는 없다.
    /// </summary>
    public class TopBarView : MonoBehaviour
    {
        [SerializeField] TMP_Text caseStatus;
        [SerializeField] TMP_Text indexStatus;
        [SerializeField] Image indexFill;
        [Tooltip("진행 중일 때 트랙 안을 오가는 띠")]
        [SerializeField] RectTransform indeterminate;

        [Header("업무 상태 컨트롤")]
        [SerializeField] Button statusButton;
        [SerializeField] Image statusBackground;
        [Tooltip("문구 너비에 맞춰 늘고 줄어드는 컨트롤 영역 (오른쪽 기준)")]
        [SerializeField] RectTransform statusRect;
        [SerializeField] RectTransform statusTrack;
        [SerializeField] float statusPadding = 12f;
        [SerializeField] float statusMinWidth = 120f;
        [SerializeField] float statusMaxWidth = 300f;
        [SerializeField] float trackMaxWidth = 160f;

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
        Action onStatus;

        /// <summary>업무 상태 컨트롤의 모양.</summary>
        public enum StatusStyle
        {
            /// <summary>누를 수 없는 상태 문구 (기기 접근 대기·인덱싱 중 등).</summary>
            Plain,
            /// <summary>누르면 관련 화면을 여는 상태 (요청 확인 필요·검토 진행·연동 완료).</summary>
            Link,
            /// <summary>강조된 실행 동작 (연동 데이터 조회 시작).</summary>
            Primary,
        }

        public struct Status
        {
            public string text;
            public StatusStyle style;
            /// <summary>비율을 알 수 없는 진행 중.</summary>
            public bool indeterminate;
            /// <summary>0~1. 비율을 알면 그 값, 끝났으면 1.</summary>
            public float progress;
            /// <summary>팝오버가 열려 있는 상태(눌린 모양).</summary>
            public bool open;
        }

        public struct Palette
        {
            public Color linkColor;
            public Color openColor;
            public Color primaryColor;
            public Color plainText;
            public Color linkText;
            public Color primaryText;
        }

        public TMP_InputField SearchField => searchField;
        public bool IsSearchFocused => searchField != null && searchField.isFocused;

        public void Bind(Action<string> onSearchChanged, Action onSearchSelected, Action onNotifications, Action onStatusClicked)
        {
            searchField.onValueChanged.AddListener(v => onSearchChanged(v));
            searchField.onSelect.AddListener(_ => onSearchSelected());
            notificationsButton.onClick.AddListener(() => onNotifications());
            onStatus = onStatusClicked;
            statusButton.onClick.AddListener(() =>
            {
                if (statusButton.interactable)
                    onStatus?.Invoke();
            });
        }

        public void Show(string caseStatusText, Status status, Palette palette)
        {
            caseStatus.text = caseStatusText;
            indexStatus.text = status.text ?? string.Empty;
            running = status.indeterminate;
            indeterminate.gameObject.SetActive(running);
            indexFill.fillAmount = running ? 0f : Mathf.Clamp01(status.progress);

            bool clickable = status.style != StatusStyle.Plain;
            statusButton.interactable = clickable;
            statusBackground.color = status.style == StatusStyle.Primary ? palette.primaryColor
                : status.style == StatusStyle.Link ? (status.open ? palette.openColor : palette.linkColor)
                : Color.clear;
            indexStatus.color = status.style == StatusStyle.Primary ? palette.primaryText
                : status.style == StatusStyle.Link ? palette.linkText : palette.plainText;
            indexStatus.fontStyle = status.style == StatusStyle.Primary ? FontStyles.Bold : FontStyles.Normal;

            // 문구 너비에 맞춰 컨트롤을 줄이고 늘린다(오른쪽 기준이라 검색·알림과 겹치지 않는다).
            float textWidth = indexStatus.GetPreferredValues(indexStatus.text).x;
            float width = Mathf.Clamp(textWidth + statusPadding * 2f, statusMinWidth, statusMaxWidth);
            statusRect.sizeDelta = new Vector2(width, statusRect.sizeDelta.y);
            statusTrack.sizeDelta = new Vector2(Mathf.Min(trackMaxWidth, width - statusPadding * 2f), statusTrack.sizeDelta.y);
        }

        /// <summary>비교 중에는 검색과 업무 상태 컨트롤을 쓰지 않는다.</summary>
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
