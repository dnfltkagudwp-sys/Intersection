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
    /// 간단한 패널: 위 머리말(현재 열람·선택한 기록·작업 기록) → 기록 정보 → 핀 → 처리 후보(보존·삭제 토글) → 작업메모,
    /// 아래 업무 진행 한 줄 · 접을 수 있는 작업 기록 빠른 목록 · 비교 칸.
    /// 전체 보기: 패널 전체를 작업 기록 목록(필터·독립 스크롤)으로 쓰고, 행을 누르면 같은 자리에 상세를 띄운다.
    /// 어느 모드든 아래의 업무 진행 한 줄 · 비교 칸 · 비교 버튼은 고정이다.
    /// </summary>
    public class WorkPanelView : MonoBehaviour
    {
        public enum Mode
        {
            /// <summary>기록 정보 + 작업 기록 빠른 목록.</summary>
            Simple,
            /// <summary>전체 보기 목록.</summary>
            FullList,
            /// <summary>전체 보기에서 연 기록 상세.</summary>
            FullDetail,
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

        [Header("의뢰 진행 작업 (의뢰 요청을 띄웠을 때만): 인벤토리 생성 등")]
        [SerializeField] RectTransform jobsRoot;
        [Tooltip("작업 한 줄 원형 (Button·Status 자식). 비활성으로 두고 복제해 쓴다.")]
        [SerializeField] GameObject jobTemplate;

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

        [Header("작업 기록 빠른 목록")]
        [SerializeField] RectTransform recordArea;
        [SerializeField] RectTransform bottomArea;
        [SerializeField] RectTransform tutorialRect;
        [SerializeField] TMP_Text tutorialLine;
        [SerializeField] Button listHeader;
        [SerializeField] TMP_Text listHeaderLabel;
        [SerializeField] TMP_Text listToggleLabel;
        [SerializeField] Button fullViewButton;
        [SerializeField] RectTransform listViewport;
        [SerializeField] RectTransform listRoot;
        [SerializeField] PinnedItemView itemPrefab;
        [SerializeField] TMP_Text listEmpty;

        [Header("작업 기록 전체 보기")]
        [SerializeField] RectTransform fullArea;
        [SerializeField] TMP_Text fullTitle;
        [SerializeField] TMP_Text fullCount;
        [SerializeField] Button fullBack;
        [SerializeField] TMP_Text fullBackLabel;
        [Tooltip("상태 필터 버튼 (전체·핀·보존·삭제·메모 순)")]
        [SerializeField] Button[] filterButtons;
        [SerializeField] TMP_Text[] filterLabels;
        [SerializeField] GameObject[] filterOn;
        [SerializeField] TMP_Dropdown caseFilter;
        [SerializeField] ScrollRect fullScroll;
        [SerializeField] RectTransform fullListRoot;
        [SerializeField] TMP_Text fullEmpty;

        [Header("비교할 기록 (임시 선택, 저장하지 않음)")]
        [SerializeField] TMP_Text slotsLabel;
        [SerializeField] RectTransform slotsRow;
        [Tooltip("두 칸이 찼을 때 다른 기록을 넣으려면 먼저 빼라는 안내")]
        [SerializeField] TMP_Text slotsNote;
        [SerializeField] Button[] slotButtons;
        [SerializeField] TMP_Text[] slotTitles;
        [Tooltip("슬롯 기록의 소유자 · 기록 코드 (필터로 목록에서 숨겨져도 칸에서 알아볼 수 있게)")]
        [SerializeField] TMP_Text[] slotMetas;
        [SerializeField] Button[] slotRemoveButtons;
        [SerializeField] RectTransform compareRect;
        [SerializeField] Button compareButton;
        [SerializeField] CanvasGroup compareGroup;

        [Header("키보드 포커스 표시")]
        [SerializeField] RectTransform focusRing;

        [Header("작업 기록 높이")]
        [SerializeField] float bottomMargin = 22f;
        [SerializeField] float recordGap = 10f;
        [SerializeField] float itemHeight = 56f;
        [SerializeField] float itemSpacing = 6f;
        [Tooltip("작업 기록 빠른 목록이 우측 패널 높이에서 차지할 수 있는 최대 비율")]
        [SerializeField, Range(0.2f, 0.6f)] float maxListShare = 0.3f;

        const string CollapsedPref = "workPanel.listCollapsed";

        readonly List<InfoRowView> rows = new List<InfoRowView>();
        readonly List<PinnedItemView> quickItems = new List<PinnedItemView>();
        readonly List<PinnedItemView> fullItems = new List<PinnedItemView>();
        Action<string> memoChanged;
        Action<int> caseChanged;
        int itemCount;
        bool collapsed;
        string collapseText;
        string expandText;
        Mode mode = Mode.Simple;
        float savedFullY;
        bool restoreFull;
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

        /// <summary>전체 보기 목록 한 번 그리기에 필요한 내용. 필터 이름·의뢰 이름은 모두 호출한 쪽이 문자열·데이터에서 만든다.</summary>
        public class FullList
        {
            public string title;
            public string count;
            public string backText;
            public Action onBack;
            public string[] filters;
            public int filterIndex;
            public Action<int> onFilter;
            public List<string> cases;
            public int caseIndex;
            public Action<int> onCase;
            public List<WorkItem> items;
            public string emptyText;
            public RowStyle rowStyle;
            public ToggleStyle chipStyle;
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
        public bool CaseFilterExpanded => caseFilter != null && caseFilter.IsExpanded;

        /// <summary>키보드로 조작 중이면 포커스 테두리를 보여주고, 포커스가 바뀔 때 스크롤 안으로 끌어온다. 마우스를 쓰면 끈다.</summary>
        public bool KeyboardFocus { get; set; }

        void Awake()
        {
            memoField.onEndEdit.AddListener(v => memoChanged?.Invoke(v));
            memoField.onDeselect.AddListener(v => memoChanged?.Invoke(v));
            try { collapsed = PlayerPrefs.GetInt(CollapsedPref, 0) == 1; } catch { collapsed = false; }
            listHeader.onClick.AddListener(ToggleCollapsed);
            caseFilter.onValueChanged.AddListener(i => caseChanged?.Invoke(i));
            fullArea.gameObject.SetActive(false);
            focusRing.gameObject.SetActive(false);
        }

        void OnRectTransformDimensionsChange()
        {
            if (bottomArea != null && slotsLabel != null && slotsNote != null && compareRect != null && fullArea != null)
                LayoutBottom();
        }

        /// <summary>작업 기록 머리말의 `전체 보기` 버튼.</summary>
        public void BindFullView(Action open)
        {
            fullViewButton.onClick.RemoveAllListeners();
            fullViewButton.onClick.AddListener(() => open());
        }

        /// <summary>
        /// 간단한 패널·전체 목록·전체 보기 상세를 바꾼다. 전체 목록을 떠날 때 스크롤 위치를 기억해 돌아올 때 되살린다.
        /// </summary>
        public void SetMode(Mode next)
        {
            if (mode == Mode.FullList && next != Mode.FullList && fullScroll.content != null)
            {
                savedFullY = fullScroll.content.anchoredPosition.y;
                restoreFull = true;
            }
            mode = next;
            recordArea.gameObject.SetActive(next != Mode.FullList);
            fullArea.gameObject.SetActive(next == Mode.FullList);
            LayoutBottom();
        }

        /// <summary>다음에 전체 목록을 그릴 때 맨 위에서 시작한다 (전체 보기를 새로 열 때·필터·기기 변경).</summary>
        public void ResetFullScroll()
        {
            savedFullY = 0f;
            restoreFull = true;
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
        public class JobItem
        {
            public string buttonText;
            public string statusText;
            /// <summary>null이면 지금 누를 수 없다(조건 전·진행 중·완료).</summary>
            public Action onStart;
        }

        public struct JobStyle
        {
            public Color readyColor;
            public Color readyTextColor;
            public Color idleColor;
            public Color idleTextColor;
        }

        readonly List<Button> jobButtons = new List<Button>();

        /// <summary>
        /// 의뢰 요청의 진행 작업. 버튼은 조건을 만족했을 때만 누를 수 있고, 아래에 업무 상태만 한 줄로 보여준다
        /// (다음에 볼 앱을 지시하지 않는다). 빈 목록이면 구역을 숨긴다.
        /// </summary>
        public void SetJobs(IList<JobItem> jobs, JobStyle style)
        {
            foreach (Transform child in jobsRoot)
            {
                if (child.gameObject != jobTemplate)
                    UIPool.Discard(child.gameObject);
            }
            jobButtons.Clear();
            jobsRoot.gameObject.SetActive(jobs != null && jobs.Count > 0);
            if (jobs == null)
                return;
            foreach (var job in jobs)
            {
                var row = Instantiate(jobTemplate, jobsRoot);
                row.SetActive(true);
                var button = row.GetComponentInChildren<Button>(true);
                var label = button.GetComponentInChildren<TMP_Text>(true);
                var status = row.transform.Find("Status").GetComponent<TMP_Text>();
                label.text = job.buttonText;
                bool ready = job.onStart != null;
                Paint(button, label, ready ? style.readyColor : style.idleColor, ready ? style.readyTextColor : style.idleTextColor);
                button.interactable = ready;
                button.onClick.RemoveAllListeners();
                if (ready)
                {
                    var start = job.onStart;
                    button.onClick.AddListener(() => start());
                }
                status.text = job.statusText ?? string.Empty;
                status.gameObject.SetActive(!string.IsNullOrEmpty(job.statusText));
                jobButtons.Add(button);
            }
        }

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
                slotMetas[i].text = filled ? slot.meta ?? string.Empty : string.Empty;
                bool twoLines = filled && !string.IsNullOrEmpty(slot.meta);
                slotMetas[i].gameObject.SetActive(twoLines);
                // 두 줄이면 제목을 위쪽에, 빈 칸 안내처럼 한 줄이면 칸 가운데에 둔다.
                var titleRect = slotTitles[i].rectTransform;
                titleRect.anchorMin = new Vector2(0f, twoLines ? 1f : 0f);
                titleRect.anchorMax = new Vector2(1f, 1f);
                titleRect.offsetMin = new Vector2(titleRect.offsetMin.x, twoLines ? -21f : 0f);
                titleRect.offsetMax = new Vector2(titleRect.offsetMax.x, twoLines ? -4f : 0f);
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
                PaintToggle(keepButton, keepLabel, keepOn, actions.classification == Classification.Keep,
                    actions.onColor, actions.onTextColor, actions.idleColor, actions.idleTextColor);
                PaintToggle(deleteButton, deleteLabel, deleteOn, actions.classification == Classification.Delete,
                    actions.onColor, actions.onTextColor, actions.idleColor, actions.idleTextColor);
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

        // ───────────── 작업 기록 빠른 목록 ─────────────

        public void ShowWorkList(List<WorkItem> list, string headerText, string emptyText, string collapse, string expand, RowStyle style)
        {
            // 다시 그려도 목록 스크롤 위치는 유지한다.
            var listScroll = listViewport.GetComponent<ScrollRect>();
            float keep = listScroll != null ? listScroll.verticalNormalizedPosition : 1f;
            keep = float.IsNaN(keep) ? 1f : Mathf.Clamp01(keep);
            Spawn(list, listRoot, quickItems, style);
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

        void Spawn(List<WorkItem> list, RectTransform parent, List<PinnedItemView> views, RowStyle style)
        {
            foreach (var view in views)
                UIPool.Discard(view.gameObject);
            views.Clear();
            foreach (var item in list)
            {
                var view = Instantiate(itemPrefab, parent);
                var it = item;
                view.Key = item.key;
                view.Bind(item.title, item.meta, item.badge, item.memoMark, item.pinned, item.selected, item.missing,
                    style.selectedColor, style.idleColor, style.missingColor, () => it.onClick());
                view.BindCompare(item.compare, item.compareLabel, item.onCompare, style.compareOnColor, style.compareOnTextColor,
                    style.compareIdleColor, style.compareIdleTextColor, style.disabledAlpha);
                views.Add(view);
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

        // ───────────── 작업 기록 전체 보기 ─────────────

        /// <summary>
        /// 전체 목록을 그린다. 다시 그려도 스크롤 위치(픽셀)는 유지하고, 상세·비교에서 돌아오면 떠날 때 위치를,
        /// ResetFullScroll 뒤에는 맨 위를 쓴다.
        /// </summary>
        public void ShowFullList(FullList data)
        {
            var content = fullScroll.content;
            float keep = restoreFull ? savedFullY : content.anchoredPosition.y;
            restoreFull = false;

            fullTitle.text = data.title;
            fullCount.text = data.count;
            fullBackLabel.text = data.backText;
            fullBack.onClick.RemoveAllListeners();
            fullBack.onClick.AddListener(() => data.onBack());

            for (int i = 0; i < filterButtons.Length; i++)
            {
                bool exists = i < data.filters.Length;
                filterButtons[i].gameObject.SetActive(exists);
                if (!exists)
                    continue;
                int index = i;
                filterLabels[i].text = data.filters[i];
                var c = data.chipStyle;
                PaintToggle(filterButtons[i], filterLabels[i], filterOn[i], i == data.filterIndex,
                    c.onColor, c.onTextColor, c.idleColor, c.idleTextColor);
                Wire(filterButtons[i], () => data.onFilter(index));
            }

            caseChanged = null;
            var current = caseFilter.options.Select(o => o.text).ToList();
            if (!current.SequenceEqual(data.cases))
            {
                caseFilter.ClearOptions();
                caseFilter.AddOptions(data.cases);
            }
            caseFilter.SetValueWithoutNotify(data.caseIndex);
            caseFilter.RefreshShownValue();
            caseChanged = data.onCase;

            Spawn(data.items, fullListRoot, fullItems, data.rowStyle);
            fullEmpty.text = data.emptyText;
            fullEmpty.gameObject.SetActive(data.items.Count == 0);

            fullScroll.StopMovement();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float max = Mathf.Max(0f, content.rect.height - fullScroll.viewport.rect.height);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(keep, 0f, max));
            ApplyNavigation();
        }

        // ───────────── 배치 ─────────────

        /// <summary>
        /// 아래 영역 높이를 내용에 맞춘다. 간단한 패널: 기록이 없으면 한 줄 안내, 접으면 머리말만,
        /// 펼치면 필요한 만큼 늘리되 패널 높이의 maxListShare를 넘지 않게 해 위쪽 기록·조작 영역을 지킨다.
        /// 전체 보기: 빠른 목록 없이 업무 진행 한 줄 · 비교 칸 · 비교 버튼만 아래에 고정한다.
        /// </summary>
        void LayoutBottom()
        {
            float panelHeight = ((RectTransform)transform).rect.height;
            bool simple = mode == Mode.Simple;
            float y = 12f;
            if (tutorialLine.gameObject.activeSelf)
            {
                Band(tutorialRect, y, 20f);
                y += 26f;
            }

            bool empty = itemCount == 0;
            bool open = simple && !empty && !collapsed;
            listHeader.gameObject.SetActive(simple);
            listEmpty.gameObject.SetActive(simple && empty);
            listViewport.gameObject.SetActive(open);
            listToggleLabel.gameObject.SetActive(!empty);
            listToggleLabel.text = collapsed ? expandText : collapseText;
            if (simple)
            {
                Band((RectTransform)listHeader.transform, y, 24f);
                y += 30f;
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
                y += 12f;
            }
            // 비교할 기록 슬롯 (라벨 + 두 칸)
            Band((RectTransform)slotsLabel.transform, y, 18f);
            Band(slotsRow, y + 24f, SlotHeight);
            y += SlotsHeight;
            if (slotsNote.gameObject.activeSelf)
            {
                Band((RectTransform)slotsNote.transform, y + 4f, 16f);
                y += NoteHeight;
            }
            y += 12f + compareRect.rect.height;

            bottomArea.sizeDelta = new Vector2(bottomArea.sizeDelta.x, y);
            float top = bottomMargin + y + recordGap;
            recordArea.offsetMin = new Vector2(recordArea.offsetMin.x, top);
            fullArea.offsetMin = new Vector2(fullArea.offsetMin.x, top);
            ApplyNavigation();
        }

        const float SlotHeight = 40f;
        const float SlotsHeight = 24f + SlotHeight;
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

            if (mode == Mode.FullList)
            {
                Add(fullBack);
                Add(filterButtons.Cast<Selectable>().ToArray());
                Add(caseFilter);
                foreach (var row in fullItems)
                    Add(row.Body, row.CompareButton);
            }
            else
            {
                Add(backLink);
                Add(sourceButton, compareToggle);
                foreach (var b in jobButtons)
                    Add(b);
                Add(pinButton);
                Add(keepButton, deleteButton);
                if (mode == Mode.Simple)
                {
                    Add(listHeader, fullViewButton);
                    if (listViewport.gameObject.activeInHierarchy)
                    {
                        foreach (var row in quickItems)
                            Add(row.Body, row.CompareButton);
                    }
                }
            }
            var slotLine = new List<Selectable>();
            for (int i = 0; i < slotButtons.Length; i++)
            {
                slotLine.Add(slotButtons[i]);
                slotLine.Add(slotRemoveButtons[i]);
            }
            Add(slotLine.ToArray());
            Add(compareButton);
            lines.RemoveAll(l => l.Count == 0);
            return lines;
        }

        static bool Usable(Selectable s) => s != null && s.gameObject.activeInHierarchy && s.IsInteractable();

        /// <summary>패널 조작의 탐색 방향을 명시적으로 정한다. 다시 그릴 때마다 호출한다.</summary>
        public void ApplyNavigation()
        {
            if (memoField == null || fullArea == null)
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

        /// <summary>처음 들어올 때의 위치: 전체 목록은 켜진 상태 필터, 상세는 돌아가기 링크.</summary>
        Selectable DefaultStop()
        {
            if (mode == Mode.FullList)
            {
                var on = filterButtons.Where((b, i) => filterOn[i].activeSelf).FirstOrDefault(Usable);
                return on ?? filterButtons.FirstOrDefault(Usable);
            }
            return Lines().SelectMany(l => l).FirstOrDefault();
        }

        public FocusToken CaptureFocus()
        {
            var current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (!Owns(current))
                return default;
            var token = new FocusToken { valid = true, target = current };
            foreach (var row in quickItems.Concat(fullItems))
            {
                if (row.Body.gameObject == current || row.CompareButton.gameObject == current)
                {
                    token.rowKey = row.Key;
                    token.rowCompare = row.CompareButton.gameObject == current;
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
            var visible = mode == Mode.FullList ? fullItems
                : mode == Mode.Simple && listViewport.gameObject.activeInHierarchy ? quickItems : new List<PinnedItemView>();
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
                        && !InsideCaseList(current) && current != memoField.gameObject;
            if (show && current != lastSelected)
                EnsureVisible((RectTransform)current.transform);
            lastSelected = current;
            focusRing.gameObject.SetActive(show && PlaceRing((RectTransform)current.transform));
        }

        /// <summary>펼친 의뢰 드롭다운 목록 안의 항목은 드롭다운 자체 강조를 쓴다.</summary>
        bool InsideCaseList(GameObject go) => go != caseFilter.gameObject && go.transform.IsChildOf(caseFilter.transform);

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
