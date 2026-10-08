using System;
using System.Collections.Generic;
using System.Linq;
using Intersection.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 우측 업무 기록. 휴대전화 안에 넣지 않는 기록 코드·사망일 기준 시점·출처·업무 상태를 여기서만 보여준다.
    /// 위: 머리말(현재 열람·선택한 기록·작업 기록) → 기록 정보 → 핀 → 처리 후보(보존·삭제 토글) → 작업메모.
    /// 아래: 업무 진행 한 줄과 접을 수 있는 작업 기록 목록. 비교(UI-06)는 아직 비활성이다.
    /// </summary>
    public class WorkPanelView : MonoBehaviour
    {
        [SerializeField] TMP_Text heading;
        [SerializeField] Button backLink;
        [SerializeField] TMP_Text recordTitle;
        [SerializeField] TMP_Text recordBody;
        [SerializeField] RectTransform infoRoot;
        [SerializeField] InfoRowView rowPrefab;

        [Header("보조 행동 (작업 기록을 띄웠을 때만): 원본 열기 · 비교에 추가/빼기")]
        [SerializeField] GameObject toolsRoot;
        [SerializeField] Button sourceButton;
        [SerializeField] TMP_Text sourceLabel;
        [SerializeField] Button compareToggle;
        [SerializeField] TMP_Text compareToggleLabel;
        [SerializeField] TMP_Text toolsNote;

        [Header("기록 조작")]
        [SerializeField] GameObject actionsRoot;
        [SerializeField] Button pinButton;
        [SerializeField] TMP_Text pinLabel;
        [SerializeField] Button keepButton;
        [SerializeField] TMP_Text keepLabel;
        [SerializeField] Button deleteButton;
        [SerializeField] TMP_Text deleteLabel;
        [SerializeField] GameObject keepOn;
        [SerializeField] GameObject deleteOn;
        [SerializeField] GameObject memoRoot;
        [SerializeField] TMP_InputField memoField;

        [Header("작업 기록 목록")]
        [SerializeField] RectTransform recordArea;
        [SerializeField] RectTransform bottomArea;
        [SerializeField] RectTransform tutorialRect;
        [SerializeField] TMP_Text tutorialLine;
        [SerializeField] Button listHeader;
        [SerializeField] TMP_Text listHeaderLabel;
        [SerializeField] TMP_Text listToggleLabel;
        [SerializeField] RectTransform listViewport;
        [SerializeField] RectTransform listRoot;
        [SerializeField] PinnedItemView itemPrefab;
        [SerializeField] TMP_Text listEmpty;

        [Header("비교할 기록 (임시 선택, 저장하지 않음)")]
        [SerializeField] TMP_Text slotsLabel;
        [SerializeField] RectTransform slotsRow;
        [Tooltip("두 칸이 찼을 때 다른 기록을 넣으려면 먼저 빼라는 안내")]
        [SerializeField] TMP_Text slotsNote;
        [SerializeField] Button[] slotButtons;
        [SerializeField] TMP_Text[] slotTitles;
        [SerializeField] Button[] slotRemoveButtons;
        [SerializeField] RectTransform compareRect;
        [SerializeField] Button compareButton;
        [SerializeField] CanvasGroup compareGroup;

        [Header("작업 기록 높이")]
        [SerializeField] float bottomMargin = 22f;
        [SerializeField] float recordGap = 10f;
        [SerializeField] float itemHeight = 56f;
        [SerializeField] float itemSpacing = 6f;
        [Tooltip("작업 기록 목록이 우측 패널 높이에서 차지할 수 있는 최대 비율")]
        [SerializeField, Range(0.2f, 0.6f)] float maxListShare = 0.3f;

        const string CollapsedPref = "workPanel.listCollapsed";

        readonly List<InfoRowView> rows = new List<InfoRowView>();
        readonly List<GameObject> items = new List<GameObject>();
        Action<string> memoChanged;
        int itemCount;
        bool collapsed;
        string collapseText;
        string expandText;

        public struct Row
        {
            public string label;
            public string value;
            public TMP_FontAsset font;
        }

        public class Actions
        {
            public bool pinned;
            public Classification classification;
            public string memo;
            public string pinText;
            public string unpinText;
            public Color primaryColor;
            public Color primaryTextColor;
            public Color onColor;
            public Color onTextColor;
            public Color idleColor;
            public Color idleTextColor;
            public Action togglePin;
            public Action<Classification> toggleClass;
            public Action<string> memoChanged;
        }

        public class SlotItem
        {
            /// <summary>null이면 빈 슬롯.</summary>
            public string title;
            public Action onOpen;
            public Action onRemove;
        }

        public class WorkItem
        {
            public string title;
            public string meta;
            public string badge;
            public bool pinned;
            public bool selected;
            public bool missing;
            public Action onClick;
            public PinnedItemView.CompareState compare;
            public string compareLabel;
            public Action onCompare;
        }

        public struct CompareButtonStyle
        {
            public Color onColor;
            public Color onTextColor;
            public Color idleColor;
            public Color idleTextColor;
            public float disabledAlpha;
        }

        public bool IsEditingMemo => memoField != null && memoField.isFocused;

        void Awake()
        {
            memoField.onEndEdit.AddListener(v => memoChanged?.Invoke(v));
            memoField.onDeselect.AddListener(v => memoChanged?.Invoke(v));
            try { collapsed = PlayerPrefs.GetInt(CollapsedPref, 0) == 1; } catch { collapsed = false; }
            listHeader.onClick.AddListener(ToggleCollapsed);
        }

        void OnRectTransformDimensionsChange()
        {
            if (bottomArea != null && slotsLabel != null && slotsNote != null && compareRect != null)
                LayoutBottom();
        }

        // ───────────── 머리말·기록 정보 ─────────────

        /// <summary>패널 머리말. onBack이 있으면 `< 현재 화면` 링크를 보여준다.</summary>
        public void SetHeading(string text, Action onBack)
        {
            heading.text = text;
            backLink.gameObject.SetActive(onBack != null);
            backLink.onClick.RemoveAllListeners();
            if (onBack != null)
                backLink.onClick.AddListener(() => onBack());
        }

        public void Show(string title, IEnumerable<Row> info) => Show(title, null, info);

        /// <summary>기록 정보만 그린다. 조작은 SetActions로 따로 정한다.</summary>
        public void Show(string title, string body, IEnumerable<Row> info)
        {
            recordTitle.text = title;
            recordBody.text = body ?? string.Empty;
            recordBody.gameObject.SetActive(!string.IsNullOrEmpty(body));

            foreach (var r in rows)
                UIPool.Discard(r.gameObject);
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

        /// <summary>
        /// 보조 행동. 각 행동은 label과 onClick이 있으면 버튼을, note가 있으면 그 이유·안내를 한 줄로 보여준다.
        /// 아무것도 없으면 줄 전체를 숨긴다.
        /// </summary>
        public void SetRecordTools(string sourceText, Action onSource, string sourceNote,
            string compareText, Action onCompare, string compareNote)
        {
            Tool(sourceButton, sourceLabel, sourceText, onSource);
            Tool(compareToggle, compareToggleLabel, compareText, onCompare);
            string note = string.Join("\n", new[] { sourceNote, compareNote }.Where(n => !string.IsNullOrEmpty(n)));
            toolsNote.text = note;
            toolsNote.gameObject.SetActive(note.Length > 0);
            bool anyButton = onSource != null || onCompare != null;
            sourceButton.transform.parent.gameObject.SetActive(anyButton);
            toolsRoot.SetActive(anyButton || note.Length > 0);
        }

        static void Tool(Button button, TMP_Text label, string text, Action onClick)
        {
            button.gameObject.SetActive(onClick != null);
            button.onClick.RemoveAllListeners();
            if (onClick == null)
                return;
            label.text = text;
            button.onClick.AddListener(() => onClick());
        }

        /// <summary>비교할 기록 두 슬롯과 비교 버튼. 두 슬롯이 모두 찼을 때만 비교할 수 있다.</summary>
        public void ShowCompareSlots(string header, string note, IList<SlotItem> slots, string emptyText, string removeText, Action onCompare,
            Color filledColor, Color emptyTextColor, Color textColor)
        {
            slotsLabel.text = header;
            slotsNote.text = note ?? string.Empty;
            slotsNote.gameObject.SetActive(!string.IsNullOrEmpty(note));
            for (int i = 0; i < slotButtons.Length; i++)
            {
                var slot = i < slots.Count ? slots[i] : null;
                bool filled = slot != null && slot.title != null;
                slotTitles[i].text = filled ? slot.title : emptyText;
                slotTitles[i].color = filled ? textColor : emptyTextColor;
                slotButtons[i].interactable = filled;
                if (slotButtons[i].targetGraphic != null)
                    slotButtons[i].targetGraphic.color = filled ? filledColor : Color.clear;
                slotButtons[i].onClick.RemoveAllListeners();
                slotRemoveButtons[i].onClick.RemoveAllListeners();
                slotRemoveButtons[i].gameObject.SetActive(filled);
                slotRemoveButtons[i].GetComponentInChildren<TMP_Text>().text = removeText;
                if (!filled)
                    continue;
                var s = slot;
                slotButtons[i].onClick.AddListener(() => s.onOpen());
                slotRemoveButtons[i].onClick.AddListener(() => s.onRemove());
            }
            compareButton.onClick.RemoveAllListeners();
            bool ready = onCompare != null;
            compareButton.interactable = ready;
            compareGroup.alpha = ready ? 1f : 0.4f;
            compareGroup.interactable = ready;
            compareGroup.blocksRaycasts = ready;
            if (ready)
                compareButton.onClick.AddListener(() => onCompare());
            LayoutBottom();
        }

        /// <summary>패널에 보이는 기록의 핀·처리 후보·작업메모. null이면 조작을 숨긴다(목록 화면 등).</summary>
        public void SetActions(Actions actions)
        {
            actionsRoot.SetActive(actions != null);
            memoRoot.SetActive(actions != null);
            memoChanged = null;
            if (actions != null)
            {
                pinLabel.text = actions.pinned ? actions.unpinText : actions.pinText;
                Paint(pinButton, pinLabel, actions.pinned ? actions.idleColor : actions.primaryColor,
                    actions.pinned ? actions.idleTextColor : actions.primaryTextColor);
                PaintToggle(keepButton, keepLabel, keepOn, actions.classification == Classification.Keep, actions);
                PaintToggle(deleteButton, deleteLabel, deleteOn, actions.classification == Classification.Delete, actions);
                Wire(pinButton, actions.togglePin);
                Wire(keepButton, () => actions.toggleClass(Classification.Keep));
                Wire(deleteButton, () => actions.toggleClass(Classification.Delete));
                memoField.SetTextWithoutNotify(actions.memo ?? string.Empty);
                memoChanged = actions.memoChanged;
            }
            else
            {
                memoField.SetTextWithoutNotify(string.Empty);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)recordTitle.transform.parent);
        }

        /// <summary>메모 입력 중 Esc: 지금 입력값을 저장하고 입력창 포커스만 해제한다.</summary>
        public void EndMemoEdit()
        {
            memoChanged?.Invoke(memoField.text);
            if (memoField.isFocused)
                memoField.DeactivateInputField();
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject == memoField.gameObject)
                events.SetSelectedGameObject(null);
        }

        // ───────────── 작업 기록 목록 ─────────────

        public void ShowWorkList(List<WorkItem> list, string headerText, string emptyText, string collapse, string expand,
            Color selectedColor, Color idleColor, Color missingColor, CompareButtonStyle compareStyle)
        {
            // 다시 그려도 목록 스크롤 위치는 유지한다.
            var listScroll = listViewport.GetComponent<ScrollRect>();
            float keep = listScroll != null ? listScroll.verticalNormalizedPosition : 1f;
            keep = float.IsNaN(keep) ? 1f : Mathf.Clamp01(keep);
            foreach (var go in items)
                UIPool.Discard(go);
            items.Clear();
            foreach (var item in list)
            {
                var view = Instantiate(itemPrefab, listRoot);
                var it = item;
                view.Bind(item.title, item.meta, item.badge, item.pinned, item.selected, item.missing,
                    selectedColor, idleColor, missingColor, () => it.onClick());
                view.BindCompare(item.compare, item.compareLabel, item.onCompare, compareStyle.onColor, compareStyle.onTextColor,
                    compareStyle.idleColor, compareStyle.idleTextColor, compareStyle.disabledAlpha);
                items.Add(view.gameObject);
            }
            itemCount = list.Count;
            listHeaderLabel.text = headerText;
            listEmpty.text = emptyText;
            collapseText = collapse;
            expandText = expand;
            LayoutBottom();
            if (listScroll != null && listScroll.content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(listScroll.content);
                listScroll.verticalNormalizedPosition = keep;
            }
        }

        public void ShowTutorial(string line)
        {
            tutorialLine.gameObject.SetActive(!string.IsNullOrEmpty(line));
            tutorialLine.text = line ?? string.Empty;
            LayoutBottom();
        }

        void ToggleCollapsed()
        {
            if (itemCount == 0)
                return;
            collapsed = !collapsed;
            try { PlayerPrefs.SetInt(CollapsedPref, collapsed ? 1 : 0); } catch { /* 화면 설정 저장 실패는 무시 */ }
            LayoutBottom();
        }

        /// <summary>
        /// 아래 영역 높이를 내용에 맞춘다. 기록이 없으면 한 줄 안내, 접으면 머리말만,
        /// 펼치면 필요한 만큼 늘리되 패널 높이의 maxListShare를 넘지 않게 해 위쪽 기록·조작 영역을 지킨다.
        /// </summary>
        void LayoutBottom()
        {
            float panelHeight = ((RectTransform)transform).rect.height;
            float y = 12f;
            if (tutorialLine.gameObject.activeSelf)
            {
                Band(tutorialRect, y, 20f);
                y += 26f;
            }
            Band((RectTransform)listHeader.transform, y, 24f);
            y += 30f;

            bool empty = itemCount == 0;
            bool open = !empty && !collapsed;
            listEmpty.gameObject.SetActive(empty);
            listViewport.gameObject.SetActive(open);
            listToggleLabel.gameObject.SetActive(!empty);
            listToggleLabel.text = collapsed ? expandText : collapseText;
            if (empty)
            {
                Band((RectTransform)listEmpty.transform, y, 20f);
                y += 20f;
            }
            else if (open)
            {
                float content = itemCount * itemHeight + (itemCount - 1) * itemSpacing + 4f;
                float fixedPart = y + 12f + SlotsHeight + NoteHeight + 12f + compareRect.rect.height;
                float max = Mathf.Max(itemHeight * 2 + itemSpacing, panelHeight * maxListShare - fixedPart);
                float height = Mathf.Min(content, max);
                Band(listViewport, y, height);
                y += height;
            }
            // 비교할 기록 슬롯 (라벨 + 두 칸)
            y += 12f;
            Band((RectTransform)slotsLabel.transform, y, 18f);
            Band(slotsRow, y + 24f, 30f);
            y += SlotsHeight;
            if (slotsNote.gameObject.activeSelf)
            {
                Band((RectTransform)slotsNote.transform, y + 4f, 16f);
                y += NoteHeight;
            }
            y += 12f + compareRect.rect.height;

            bottomArea.sizeDelta = new Vector2(bottomArea.sizeDelta.x, y);
            recordArea.offsetMin = new Vector2(recordArea.offsetMin.x, bottomMargin + y + recordGap);
        }

        const float SlotsHeight = 54f;
        float NoteHeight => slotsNote != null && slotsNote.gameObject.activeSelf ? 20f : 0f;

        /// <summary>부모 위쪽에서 y만큼 내려온 곳에 높이 h의 가로 띠로 배치한다.</summary>
        static void Band(RectTransform rt, float y, float h)
        {
            rt.anchorMin = new Vector2(rt.anchorMin.x, 1);
            rt.anchorMax = new Vector2(rt.anchorMax.x, 1);
            rt.offsetMax = new Vector2(rt.offsetMax.x, -y);
            rt.offsetMin = new Vector2(rt.offsetMin.x, -(y + h));
        }

        static void Paint(Button button, TMP_Text label, Color background, Color textColor)
        {
            if (button.targetGraphic != null)
                button.targetGraphic.color = background;
            label.color = textColor;
        }

        static void PaintToggle(Button button, TMP_Text label, GameObject outline, bool on, Actions a)
        {
            Paint(button, label, on ? a.onColor : a.idleColor, on ? a.onTextColor : a.idleTextColor);
            outline.SetActive(on);
        }

        static void Wire(Button button, Action action)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action());
        }
    }
}
