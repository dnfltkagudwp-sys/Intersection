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
    /// 위: 머리말(현재 열람·선택한 기록·작업함·의뢰 요청) → 기록 정보 → 보조 행동 → 고정 → 처리 후보(보존·삭제 토글) → 작업메모.
    /// 아래(휴대전화 화면일 때): 업무 안내 한 줄 · `고정 자료 N건` 빠른 목록(최근 고정 최대 3개) · 비교 상태 한 줄.
    /// 작업함이 열려 있으면 이 패널은 작업함에서 고른 자료의 상세·조작만 보여주고, 아래에는 안내 한 줄만 남는다.
    /// </summary>
    public class WorkPanelView : MonoBehaviour
    {
        public enum Mode
        {
            /// <summary>휴대전화 화면: 기록 정보 + 고정 자료 빠른 목록 + 비교 상태 줄.</summary>
            Simple,
            /// <summary>작업함이 열린 동안: 선택한 자료의 상세·조작.</summary>
            Workbench,
        }

        [SerializeField] TMP_Text heading;
        [SerializeField] Button backLink;
        [SerializeField] TMP_Text backLinkLabel;
        [Tooltip("기록 정보 스크롤 영역. 돌아가기 버튼이 보일 때 그 아래로 내린다.")]
        [SerializeField] RectTransform recordScroll;
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
        [Tooltip("기존 씬에 남은 삭제 후보 버튼 참조. 실행 시 숨기며 새 UI에는 생성하지 않는다.")]
        [SerializeField] Button deleteButton;
        [SerializeField] GameObject keepOn;
        [Tooltip("보존 지정 라벨·토글 줄. 의뢰 요청처럼 지정하지 않는 기록이면 숨긴다.")]
        [SerializeField] GameObject[] classParts;
        [SerializeField] GameObject memoRoot;
        [SerializeField] TMP_InputField memoField;

        [Header("고정 자료 빠른 목록 (최근 고정 최대 3개)")]
        [SerializeField] RectTransform recordArea;
        [SerializeField] RectTransform bottomArea;
        [SerializeField] RectTransform tutorialRect;
        [SerializeField] TMP_Text tutorialLine;
        [Tooltip("`고정 자료 N건` 제목. 누르면 작업함을 고정 필터로 연다.")]
        [SerializeField] Button listHeader;
        [SerializeField] TMP_Text listHeaderLabel;
        [Tooltip("`작업함 열기`. 작업함을 고정 필터로 연다.")]
        [SerializeField] Button workbenchButton;
        [SerializeField] RectTransform listViewport;
        [SerializeField] RectTransform listRoot;
        [SerializeField] PinnedItemView itemPrefab;
        [SerializeField] TMP_Text listEmpty;

        [Header("비교 상태 한 줄 (비교 칸이 하나라도 찼을 때만)")]
        [SerializeField] Button compareStatus;
        [SerializeField] TMP_Text compareStatusLabel;

        [Header("키보드 포커스 표시")]
        [SerializeField] RectTransform focusRing;

        [Header("아래 영역 높이")]
        [SerializeField] float bottomMargin = 22f;
        [SerializeField] float recordGap = 10f;
        [SerializeField] float itemHeight = 56f;
        [SerializeField] float itemSpacing = 6f;
        [SerializeField] float compareStatusHeight = 36f;
        [Tooltip("패널 위쪽에서 기록 정보 영역이 시작하는 위치 (빌더 값과 같게)")]
        [SerializeField] float recordTop = 72f;

        RectTransform recordContent;

        readonly List<InfoRowView> rows = new List<InfoRowView>();
        readonly List<PinnedItemView> quickItems = new List<PinnedItemView>();
        Action<string> memoChanged;
        int itemCount;
        Mode mode = Mode.Simple;
        GameObject lastSelected;
        readonly Vector3[] corners = new Vector3[4];

        public struct Row
        {
            public string label;
            public string value;
            public TMP_FontAsset font;
        }

        public class Actions
        {
            public bool pinned;
            /// <summary>false면 보존 지정을 숨긴다 (의뢰 요청 등).</summary>
            public bool showClassification = true;
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
            /// <summary>소유자(의뢰 표시명) · 기록 코드.</summary>
            public string meta;
            public Action onOpen;
            public Action onRemove;
        }

        public class WorkItem
        {
            public string key;
            public string title;
            public string meta;
            public string badge;
            /// <summary>작업메모가 있으면 표시 문구, 없으면 null.</summary>
            public string memoMark;
            public bool pinned;
            public bool selected;
            public bool missing;
            public Action onClick;
            public PinnedItemView.CompareState compare;
            public string compareLabel;
            public Action onCompare;
        }

        public class RowStyle
        {
            public Color selectedColor;
            public Color idleColor;
            public Color missingColor;
            public Color compareOnColor;
            public Color compareOnTextColor;
            public Color compareIdleColor;
            public Color compareIdleTextColor;
            public float disabledAlpha;
        }

        public class ToggleStyle
        {
            public Color onColor;
            public Color onTextColor;
            public Color idleColor;
            public Color idleTextColor;
        }

        /// <summary>다시 그리기 전 키보드 포커스 위치. 행은 다시 만들어지므로 기록 키로 기억한다.</summary>
        public struct FocusToken
        {
            public bool valid;
            public GameObject target;
            public string rowKey;
            public bool rowCompare;
        }

        public bool IsEditingMemo => memoField != null && memoField.isFocused;
        public Mode CurrentMode => mode;

        /// <summary>키보드로 조작 중이면 포커스 테두리를 보여주고, 포커스가 바뀔 때 스크롤 안으로 끌어온다. 마우스를 쓰면 끈다.</summary>
        public bool KeyboardFocus { get; set; }

        void Awake()
        {
            memoField.onEndEdit.AddListener(v => memoChanged?.Invoke(v));
            memoField.onDeselect.AddListener(v => memoChanged?.Invoke(v));
            focusRing.gameObject.SetActive(false);
        }

        void OnRectTransformDimensionsChange()
        {
            if (bottomArea != null && compareStatus != null && listHeader != null)
                LayoutBottom();
        }

        /// <summary>빠른 목록의 `고정 자료 N건` 제목과 `작업함 열기`: 둘 다 작업함을 고정 필터로 연다.</summary>
        public void BindWorkbench(Action openPinned)
        {
            listHeader.onClick.RemoveAllListeners();
            listHeader.onClick.AddListener(() => openPinned());
            workbenchButton.onClick.RemoveAllListeners();
            workbenchButton.onClick.AddListener(() => openPinned());
        }

        /// <summary>휴대전화 화면(빠른 목록·비교 상태 줄 표시)과 작업함 상세(아래에는 안내 한 줄만)를 바꾼다.</summary>
        public void SetMode(Mode next)
        {
            mode = next;
            LayoutBottom();
        }

        // ───────────── 머리말·기록 정보 ─────────────

        const float BackGap = 10f;

        /// <summary>
        /// 패널 머리말. onBack이 있으면 패널 맨 위 왼쪽에 backText 돌아가기 버튼(`< 현재 화면` / `< 작업 기록`)을 보여주고
        /// 기록 정보는 그 아래에서 시작한다.
        /// </summary>
        public void SetHeading(string text, Action onBack, string backText = null)
        {
            heading.text = text;
            backLink.gameObject.SetActive(onBack != null);
            float top = onBack != null ? ((RectTransform)backLink.transform).rect.height + BackGap : 0f;
            recordScroll.offsetMax = new Vector2(recordScroll.offsetMax.x, -top);
            backLink.onClick.RemoveAllListeners();
            if (onBack == null)
                return;
            if (backText != null)
                backLinkLabel.text = backText;
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

        bool statusVisible;

        /// <summary>
        /// 작업함 밖에서 비교 칸 상태를 잊지 않게 하는 한 줄 (`비교 1/2 · 자료 하나 더 필요` / `비교 2/2 · 비교 시작`).
        /// label이 null이면(두 칸 모두 비어 있음) 숨긴다. 누르면 onClick(작업함 열기 또는 비교 시작).
        /// </summary>
        public void ShowCompareStatus(string label, Action onClick, Color background, Color textColor)
        {
            statusVisible = !string.IsNullOrEmpty(label);
            compareStatusLabel.text = label ?? string.Empty;
            Paint(compareStatus, compareStatusLabel, background, textColor);
            compareStatus.onClick.RemoveAllListeners();
            if (onClick != null)
                compareStatus.onClick.AddListener(() => onClick());
            LayoutBottom();
        }

        /// <summary>패널에 보이는 기록의 핀·보존 지정·작업메모. null이면 조작을 숨긴다(목록 화면 등).</summary>
        public void SetActions(Actions actions)
        {
            // Existing scenes still serialize the retired button; remove its hit target and layout slot.
            if (deleteButton != null)
            {
                deleteButton.onClick.RemoveAllListeners();
                deleteButton.gameObject.SetActive(false);
            }
            actionsRoot.SetActive(actions != null);
            memoRoot.SetActive(actions != null);
            foreach (var part in classParts)
                part.SetActive(actions != null && actions.showClassification);
            memoChanged = null;
            if (actions != null)
            {
                pinLabel.text = actions.pinned ? actions.unpinText : actions.pinText;
                Paint(pinButton, pinLabel, actions.pinned ? actions.idleColor : actions.primaryColor,
                    actions.pinned ? actions.idleTextColor : actions.primaryTextColor);
                PaintToggle(keepButton, keepLabel, keepOn, actions.classification == Classification.Keep,
                    actions.onColor, actions.onTextColor, actions.idleColor, actions.idleTextColor);
                Wire(pinButton, actions.togglePin);
                Wire(keepButton, () => actions.toggleClass(Classification.Keep));
                memoField.SetTextWithoutNotify(actions.memo ?? string.Empty);
                memoChanged = actions.memoChanged;
            }
            else
            {
                memoField.SetTextWithoutNotify(string.Empty);
            }
            if (recordArea.gameObject.activeInHierarchy)
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

        // ───────────── 고정 자료 빠른 목록 ─────────────

        /// <summary>최근에 고정한 자료 몇 개(호출한 쪽이 최대 개수로 자른 목록)와 전체 고정 수 제목.</summary>
        public void ShowPinnedList(List<WorkItem> list, string headerText, string emptyText, RowStyle style)
        {
            foreach (var view in quickItems)
                UIPool.Discard(view.gameObject);
            quickItems.Clear();
            foreach (var item in list)
            {
                var view = Instantiate(itemPrefab, listRoot);
                var it = item;
                view.Key = item.key;
                view.Bind(item.title, item.meta, item.badge, item.memoMark, item.pinned, item.selected, item.missing,
                    style.selectedColor, style.idleColor, style.missingColor, () => it.onClick());
                // 빠른 목록은 이동용 북마크라 `+ 비교`는 두지 않는다(비교는 상세·작업함에서).
                view.BindCompare(PinnedItemView.CompareState.Hidden, null, null, default, default, default, default, 1f);
                quickItems.Add(view);
            }
            itemCount = list.Count;
            listHeaderLabel.text = headerText;
            listEmpty.text = emptyText;
            LayoutBottom();
        }

        public void ShowTutorial(string line)
        {
            tutorialLine.gameObject.SetActive(!string.IsNullOrEmpty(line));
            tutorialLine.text = line ?? string.Empty;
            LayoutBottom();
        }

        // ───────────── 배치 ─────────────

        /// <summary>기록 영역(본문·정보·보조 행동·고정·분류·작업메모)을 스크롤 없이 모두 보여주는 데 필요한 높이.</summary>
        float RecordNeed()
        {
            if (recordScroll == null)
                return 0f;
            if (recordContent == null)
            {
                var scroll = recordScroll.GetComponentInChildren<ScrollRect>(true);
                recordContent = scroll != null ? scroll.content : null;
            }
            if (recordContent == null || !recordContent.gameObject.activeInHierarchy)
                return 0f;
            LayoutRebuilder.ForceRebuildLayoutImmediate(recordContent);
            // 돌아가기 버튼이 보이면 기록 스크롤이 그만큼 내려가 있다.
            return LayoutUtility.GetPreferredHeight(recordContent) - recordScroll.offsetMax.y;
        }

        /// <summary>
        /// 아래 영역 높이를 정한다. 우선순위: 현재 자료 정보·조작·작업메모 → 비교 상태 → 고정 자료 빠른 목록.
        /// 기록 영역에 필요한 높이를 먼저 남기고, 남는 높이로 안내 한 줄(조건일 때) → 비교 상태 한 줄 → `고정 자료 N건` 제목을 놓은 뒤
        /// 빠른 목록은 들어가는 줄 수만 보여준다(최대 3개, 모자라면 제목 한 줄로 접힘). 제목은 늘 남겨 작업함으로 갈 수 있게 한다.
        /// 기록 자체가 패널보다 길면 빠른 목록은 제목만 남고 기록 영역이 스크롤된다.
        /// 작업함이 열려 있으면 안내 한 줄만 남긴다.
        /// </summary>
        void LayoutBottom()
        {
            float panelHeight = ((RectTransform)transform).rect.height;
            bool simple = mode == Mode.Simple;
            float budget = panelHeight - recordTop - bottomMargin - recordGap - RecordNeed();
            float y = 12f;
            if (tutorialLine.gameObject.activeSelf)
            {
                Band(tutorialRect, y, 20f);
                y += 26f;
            }

            bool empty = itemCount == 0;
            listHeader.gameObject.SetActive(simple);
            compareStatus.gameObject.SetActive(simple && statusVisible);
            int shown = 0;
            bool showEmpty = false;
            if (simple)
            {
                if (statusVisible)
                {
                    Band((RectTransform)compareStatus.transform, y, compareStatusHeight);
                    y += compareStatusHeight + 10f;
                }
                Band((RectTransform)listHeader.transform, y, 24f);
                y += 24f;
                const float gap = 6f;
                if (empty)
                {
                    showEmpty = y + gap + 20f <= budget;
                    if (showEmpty)
                    {
                        Band((RectTransform)listEmpty.transform, y + gap, 20f);
                        y += gap + 20f;
                    }
                }
                else
                {
                    float room = budget - y - gap;
                    shown = Mathf.Clamp(Mathf.FloorToInt((room + itemSpacing) / (itemHeight + itemSpacing)), 0, itemCount);
                    if (shown > 0)
                    {
                        float height = shown * itemHeight + (shown - 1) * itemSpacing;
                        Band(listViewport, y + gap, height);
                        y += gap + height;
                    }
                }
            }
            else if (y <= 12f)
                y = 0f;
            listEmpty.gameObject.SetActive(simple && showEmpty);
            listViewport.gameObject.SetActive(simple && shown > 0);
            for (int i = 0; i < quickItems.Count; i++)
                quickItems[i].gameObject.SetActive(i < shown);

            bottomArea.gameObject.SetActive(y > 0f);
            bottomArea.sizeDelta = new Vector2(bottomArea.sizeDelta.x, y);
            float top = bottomMargin + y + (y > 0f ? recordGap : 0f);
            recordArea.offsetMin = new Vector2(recordArea.offsetMin.x, top);
            ApplyNavigation();
        }

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

        static void PaintToggle(Button button, TMP_Text label, GameObject outline, bool on,
            Color onColor, Color onTextColor, Color idleColor, Color idleTextColor)
        {
            Paint(button, label, on ? onColor : idleColor, on ? onTextColor : idleTextColor);
            outline.SetActive(on);
        }

        static void Wire(Button button, Action action)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action());
        }

        // ───────────── 키보드 탐색 ─────────────

        /// <summary>
        /// 패널의 키보드 탐색 순서. 한 줄은 좌우(←→)로, 줄 사이는 상하(↑↓)로, Tab은 전체를 순서대로 돈다.
        /// 휴대전화 쪽으로 자동 탐색이 새지 않도록 패널 안의 조작만 담는다. 작업메모 입력창은 선택 즉시 입력이 시작되므로 넣지 않는다.
        /// </summary>
        List<List<Selectable>> Lines()
        {
            var lines = new List<List<Selectable>>();
            void Add(params Selectable[] items) => lines.Add(items.Where(Usable).ToList());

            Add(backLink);
            Add(sourceButton, compareToggle);
            Add(pinButton);
            Add(keepButton);
            if (mode == Mode.Simple)
            {
                Add(listHeader, workbenchButton);
                if (listViewport.gameObject.activeInHierarchy)
                {
                    foreach (var row in quickItems)
                        Add(row.Body);
                }
                Add(compareStatus);
            }
            lines.RemoveAll(l => l.Count == 0);
            return lines;
        }

        static bool Usable(Selectable s) => s != null && s.gameObject.activeInHierarchy && s.IsInteractable();

        /// <summary>패널 조작의 탐색 방향을 명시적으로 정한다. 다시 그릴 때마다 호출한다.</summary>
        public void ApplyNavigation()
        {
            if (memoField == null || compareStatus == null)
                return;
            var lines = Lines();
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                for (int j = 0; j < line.Count; j++)
                {
                    var nav = new Navigation
                    {
                        mode = Navigation.Mode.Explicit,
                        selectOnLeft = j > 0 ? line[j - 1] : null,
                        selectOnRight = j < line.Count - 1 ? line[j + 1] : null,
                        selectOnUp = i > 0 ? Nearest(lines[i - 1], j) : null,
                        selectOnDown = i < lines.Count - 1 ? Nearest(lines[i + 1], j) : null,
                    };
                    line[j].navigation = nav;
                }
            }
            memoField.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        static Selectable Nearest(List<Selectable> line, int index) => line[Mathf.Min(index, line.Count - 1)];

        public bool Owns(GameObject go) => go != null && go.transform.IsChildOf(transform);

        /// <summary>Tab / Shift+Tab: 패널 안에서 다음(이전) 조작으로 옮긴다. 패널 밖에 있었으면 기본 위치로 들어온다.</summary>
        public void FocusNext(bool reverse)
        {
            var events = EventSystem.current;
            if (events == null)
                return;
            var flat = Lines().SelectMany(l => l).ToList();
            if (flat.Count == 0)
                return;
            var current = events.currentSelectedGameObject;
            int index = flat.FindIndex(s => s.gameObject == current);
            Selectable next;
            if (index < 0)
                next = reverse ? flat[flat.Count - 1] : DefaultStop() ?? flat[0];
            else
                next = flat[(index + (reverse ? -1 : 1) + flat.Count) % flat.Count];
            next.Select();
        }

        /// <summary>처음 들어올 때의 위치: 맨 위 조작(돌아가기 버튼이 있으면 그것).</summary>
        Selectable DefaultStop() => Lines().SelectMany(l => l).FirstOrDefault();

        public FocusToken CaptureFocus()
        {
            var current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (!Owns(current))
                return default;
            var token = new FocusToken { valid = true, target = current };
            foreach (var row in quickItems)
            {
                if (row.Body.gameObject == current)
                {
                    token.rowKey = row.Key;
                    token.rowCompare = false;
                    break;
                }
            }
            return token;
        }

        /// <summary>
        /// 다시 그린 뒤 키보드 포커스를 되살린다: 같은 기록의 행(같은 버튼) → 그대로 쓸 수 있는 조작 →
        /// fallbackRowKey 행(상세에서 목록으로 돌아올 때 보던 기록) → 기본 위치.
        /// </summary>
        public void RestoreFocus(FocusToken token, string fallbackRowKey)
        {
            if (!token.valid)
                return;
            Selectable next = null;
            var visible = mode == Mode.Simple && listViewport.gameObject.activeInHierarchy ? quickItems : new List<PinnedItemView>();
            if (token.rowKey != null)
            {
                var row = visible.FirstOrDefault(r => r.Key == token.rowKey);
                if (row != null)
                    next = token.rowCompare && Usable(row.CompareButton) ? row.CompareButton : row.Body;
            }
            if (next == null && token.rowKey == null && token.target != null)
            {
                var same = token.target.GetComponent<Selectable>();
                if (Usable(same))
                    next = same;
            }
            if (next == null && fallbackRowKey != null)
                next = visible.FirstOrDefault(r => r.Key == fallbackRowKey)?.Body;
            if (next == null)
                next = DefaultStop();
            if (next != null && Usable(next))
                next.Select();
        }

        // ───────────── 포커스 테두리 ─────────────

        void LateUpdate()
        {
            var events = EventSystem.current;
            var current = events != null ? events.currentSelectedGameObject : null;
            bool show = KeyboardFocus && current != null && current.activeInHierarchy && Owns(current)
                        && current != memoField.gameObject;
            if (show && current != lastSelected)
                EnsureVisible((RectTransform)current.transform);
            lastSelected = current;
            focusRing.gameObject.SetActive(show && PlaceRing((RectTransform)current.transform));
        }

        /// <summary>포커스한 조작 둘레에 테두리를 맞춘다. 스크롤 영역 안이면 보이는 부분까지만 그린다.</summary>
        bool PlaceRing(RectTransform target)
        {
            var parent = (RectTransform)focusRing.parent;
            target.GetWorldCorners(corners);
            Vector2 min = parent.InverseTransformPoint(corners[0]);
            Vector2 max = parent.InverseTransformPoint(corners[2]);
            var mask = target.GetComponentInParent<RectMask2D>();
            if (mask != null && mask.transform.IsChildOf(transform))
            {
                ((RectTransform)mask.transform).GetWorldCorners(corners);
                Vector2 clipMin = parent.InverseTransformPoint(corners[0]);
                Vector2 clipMax = parent.InverseTransformPoint(corners[2]);
                min = Vector2.Max(min, clipMin);
                max = Vector2.Min(max, clipMax);
                if (max.y - min.y < 4f || max.x - min.x < 4f)
                    return false;
            }
            const float pad = 2f;
            focusRing.anchorMin = focusRing.anchorMax = focusRing.pivot = new Vector2(0.5f, 0.5f);
            focusRing.localPosition = (min + max) * 0.5f;
            focusRing.sizeDelta = max - min + Vector2.one * (pad * 2f);
            focusRing.SetAsLastSibling();
            return true;
        }

        /// <summary>키보드로 옮긴 조작이 스크롤 밖에 있으면 보이도록 스크롤한다.</summary>
        static void EnsureVisible(RectTransform item)
        {
            var scroll = item.GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.content == null || !item.IsChildOf(scroll.content))
                return;
            var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            var c = new Vector3[4];
            item.GetWorldCorners(c);
            float top = viewport.InverseTransformPoint(c[1]).y;
            float bottom = viewport.InverseTransformPoint(c[0]).y;
            var view = viewport.rect;
            var content = scroll.content;
            float y = content.anchoredPosition.y;
            if (top > view.yMax)
                y -= top - view.yMax;
            else if (bottom < view.yMin)
                y += view.yMin - bottom;
            else
                return;
            float max = Mathf.Max(0f, content.rect.height - view.height);
            scroll.StopMovement();
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(y, 0f, max));
        }
    }
}
