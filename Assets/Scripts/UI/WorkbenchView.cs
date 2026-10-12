using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 작업함. 휴대전화 자리(중앙)를 덮는 PC 업무 화면으로, 고정·분류·메모한 자료 전체를 넓게 보여준다.
    /// 위에서부터 머리말(제목 · 개수, 닫기) → 비교할 자료 두 칸 · 비교 시작 → 상태 필터 · 의뢰 필터 → 독립 스크롤 목록.
    /// 행을 눌러도 목록은 그대로이고, 선택한 자료의 정보·조작은 우측 패널이 보여준다.
    /// 필터·스크롤·선택은 실행 중에만 기억하는 임시 UI 상태이며 저장하지 않는다. 문구는 모두 호출한 쪽이 문자열·데이터에서 만든다.
    /// </summary>
    public class WorkbenchView : MonoBehaviour
    {
        [SerializeField] TMP_Text heading;
        [SerializeField] TMP_Text countLabel;
        [SerializeField] Button closeButton;

        [Header("비교할 자료 (임시 선택, 저장하지 않음)")]
        [SerializeField] TMP_Text slotsLabel;
        [SerializeField] Button[] slotButtons;
        [SerializeField] TMP_Text[] slotTitles;
        [Tooltip("슬롯 기록의 의뢰 표시명 · 기록 코드")]
        [SerializeField] TMP_Text[] slotMetas;
        [SerializeField] Button[] slotRemoveButtons;
        [SerializeField] Button compareButton;
        [SerializeField] CanvasGroup compareGroup;
        [SerializeField] TMP_Text slotsNote;

        [Header("필터")]
        [Tooltip("상태 필터 버튼 (전체·고정·보존·삭제·메모 순)")]
        [SerializeField] Button[] filterButtons;
        [SerializeField] TMP_Text[] filterLabels;
        [SerializeField] GameObject[] filterOn;
        [SerializeField] TMP_Dropdown caseFilter;

        [Header("목록")]
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform listRoot;
        [SerializeField] TMP_Text emptyLabel;
        [SerializeField] PinnedItemView itemPrefab;

        [Header("키보드 포커스 표시")]
        [SerializeField] RectTransform focusRing;

        readonly List<PinnedItemView> items = new List<PinnedItemView>();
        readonly Vector3[] corners = new Vector3[4];
        Action<int> caseChanged;
        bool resetScroll = true;
        GameObject lastSelected;

        /// <summary>목록 한 번 그리기에 필요한 내용.</summary>
        public class Data
        {
            public string title;
            public string count;
            public string[] filters;
            public int filterIndex;
            public Action<int> onFilter;
            public List<string> cases;
            public int caseIndex;
            public Action<int> onCase;
            public List<WorkPanelView.WorkItem> items;
            public string emptyText;
            public WorkPanelView.RowStyle rowStyle;
            public WorkPanelView.ToggleStyle chipStyle;
        }

        public bool IsOpen => gameObject.activeSelf;
        public bool CaseFilterExpanded => caseFilter != null && caseFilter.IsExpanded;

        /// <summary>키보드로 조작 중이면 포커스 테두리를 보여주고, 포커스가 바뀔 때 스크롤 안으로 끌어온다.</summary>
        public bool KeyboardFocus { get; set; }

        void Awake()
        {
            caseFilter.onValueChanged.AddListener(i => caseChanged?.Invoke(i));
            focusRing.gameObject.SetActive(false);
        }

        public void Open(Action onClose)
        {
            gameObject.SetActive(true);
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => onClose());
        }

        public void Close()
        {
            if (caseFilter.IsExpanded)
                caseFilter.Hide();
            gameObject.SetActive(false);
        }

        /// <summary>다음에 목록을 그릴 때 맨 위에서 시작한다 (필터 변경 등).</summary>
        public void ResetScroll() => resetScroll = true;

        public void Show(Data data)
        {
            heading.text = data.title;
            countLabel.text = data.count;

            for (int i = 0; i < filterButtons.Length; i++)
            {
                bool exists = i < data.filters.Length;
                filterButtons[i].gameObject.SetActive(exists);
                if (!exists)
                    continue;
                int index = i;
                filterLabels[i].text = data.filters[i];
                var c = data.chipStyle;
                bool on = i == data.filterIndex;
                Paint(filterButtons[i], filterLabels[i], on ? c.onColor : c.idleColor, on ? c.onTextColor : c.idleTextColor);
                filterOn[i].SetActive(on);
                filterButtons[i].onClick.RemoveAllListeners();
                filterButtons[i].onClick.AddListener(() => data.onFilter(index));
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

            // 다시 그려도 스크롤 위치(픽셀)는 유지한다. 필터를 바꾸면 맨 위부터.
            var content = scroll.content;
            float keep = resetScroll ? 0f : content.anchoredPosition.y;
            resetScroll = false;
            foreach (var view in items)
                UIPool.Discard(view.gameObject);
            items.Clear();
            var style = data.rowStyle;
            foreach (var item in data.items)
            {
                var view = Instantiate(itemPrefab, listRoot);
                var it = item;
                view.Key = item.key;
                view.Bind(item.title, item.meta, item.badge, item.memoMark, item.pinned, item.selected, item.missing,
                    style.selectedColor, style.idleColor, style.missingColor, () => it.onClick());
                view.BindCompare(item.compare, item.compareLabel, item.onCompare, style.compareOnColor, style.compareOnTextColor,
                    style.compareIdleColor, style.compareIdleTextColor, style.disabledAlpha);
                items.Add(view);
            }
            emptyLabel.text = data.emptyText;
            emptyLabel.gameObject.SetActive(data.items.Count == 0);

            scroll.StopMovement();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float max = Mathf.Max(0f, content.rect.height - scroll.viewport.rect.height);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(keep, 0f, max));
            ApplyNavigation();
        }

        /// <summary>
        /// 비교할 자료 두 칸과 `비교 시작`. 두 칸이 모두 찼을 때만 비교할 수 있고, 자동으로 바꾸거나 채우지 않는다.
        /// note는 두 칸이 찼는데 다른 자료를 넣으려 했을 때의 안내(없으면 숨김).
        /// </summary>
        public void ShowSlots(string label, IList<WorkPanelView.SlotItem> slots, string emptyText, string removeText, string note,
            Action onCompare, Color filledColor, Color emptyTextColor, Color textColor)
        {
            slotsLabel.text = label;
            slotsNote.text = note ?? string.Empty;
            slotsNote.gameObject.SetActive(!string.IsNullOrEmpty(note));
            for (int i = 0; i < slotButtons.Length; i++)
            {
                var slot = i < slots.Count ? slots[i] : null;
                bool filled = slot != null && slot.title != null;
                slotTitles[i].text = filled ? slot.title : emptyText;
                slotTitles[i].color = filled ? textColor : emptyTextColor;
                slotMetas[i].text = filled ? slot.meta ?? string.Empty : string.Empty;
                slotMetas[i].gameObject.SetActive(filled && !string.IsNullOrEmpty(slot.meta));
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
            ApplyNavigation();
        }

        static void Paint(Button button, TMP_Text label, Color background, Color textColor)
        {
            if (button.targetGraphic != null)
                button.targetGraphic.color = background;
            label.color = textColor;
        }

        // ───────────── 키보드 탐색 ─────────────

        /// <summary>탐색 순서: 닫기 → 비교 칸·비교 시작 → 상태 필터 → 의뢰 필터 → 자료 행(본문 · 비교).</summary>
        List<List<Selectable>> Lines()
        {
            var lines = new List<List<Selectable>>();
            void Add(params Selectable[] s) => lines.Add(s.Where(Usable).ToList());
            Add(closeButton);
            var slotLine = new List<Selectable>();
            for (int i = 0; i < slotButtons.Length; i++)
            {
                slotLine.Add(slotButtons[i]);
                slotLine.Add(slotRemoveButtons[i]);
            }
            slotLine.Add(compareButton);
            Add(slotLine.ToArray());
            Add(filterButtons.Cast<Selectable>().ToArray());
            Add(caseFilter);
            foreach (var row in items)
                Add(row.Body, row.CompareButton);
            lines.RemoveAll(l => l.Count == 0);
            return lines;
        }

        static bool Usable(Selectable s) => s != null && s.gameObject.activeInHierarchy && s.IsInteractable();

        public void ApplyNavigation()
        {
            var lines = Lines();
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                for (int j = 0; j < line.Count; j++)
                {
                    line[j].navigation = new Navigation
                    {
                        mode = Navigation.Mode.Explicit,
                        selectOnLeft = j > 0 ? line[j - 1] : null,
                        selectOnRight = j < line.Count - 1 ? line[j + 1] : null,
                        selectOnUp = i > 0 ? lines[i - 1][Mathf.Min(j, lines[i - 1].Count - 1)] : null,
                        selectOnDown = i < lines.Count - 1 ? lines[i + 1][Mathf.Min(j, lines[i + 1].Count - 1)] : null,
                    };
                }
            }
        }

        public bool Owns(GameObject go) => go != null && go.transform.IsChildOf(transform);

        /// <summary>Tab / Shift+Tab. 밖에서 들어오면 켜진 상태 필터부터.</summary>
        public void FocusNext(bool reverse)
        {
            var events = EventSystem.current;
            if (events == null)
                return;
            var flat = Lines().SelectMany(l => l).ToList();
            if (flat.Count == 0)
                return;
            int index = flat.FindIndex(s => s.gameObject == events.currentSelectedGameObject);
            Selectable next;
            if (index < 0)
            {
                var on = filterButtons.Where((b, i) => filterOn[i].activeSelf).FirstOrDefault(Usable);
                next = reverse ? flat[flat.Count - 1] : on ?? flat[0];
            }
            else
                next = flat[(index + (reverse ? -1 : 1) + flat.Count) % flat.Count];
            next.Select();
        }

        /// <summary>다시 그린 행 중 같은 기록 행으로 포커스를 돌려준다(행은 다시 만들어지므로 기록 키로 찾는다).</summary>
        public string FocusedRowKey()
        {
            var current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var row = items.FirstOrDefault(r => r.Body.gameObject == current || r.CompareButton.gameObject == current);
            return row != null ? row.Key : null;
        }

        public void FocusRow(string key)
        {
            var row = key == null ? null : items.FirstOrDefault(r => r.Key == key);
            if (row != null && Usable(row.Body))
                row.Body.Select();
        }

        void LateUpdate()
        {
            var events = EventSystem.current;
            var current = events != null ? events.currentSelectedGameObject : null;
            bool show = KeyboardFocus && current != null && current.activeInHierarchy && Owns(current)
                        && !(current != caseFilter.gameObject && current.transform.IsChildOf(caseFilter.transform));
            if (show && current != lastSelected)
                EnsureVisible((RectTransform)current.transform);
            lastSelected = current;
            focusRing.gameObject.SetActive(show && PlaceRing((RectTransform)current.transform));
        }

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
                min = Vector2.Max(min, (Vector2)parent.InverseTransformPoint(corners[0]));
                max = Vector2.Min(max, (Vector2)parent.InverseTransformPoint(corners[2]));
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

        void EnsureVisible(RectTransform item)
        {
            if (!item.IsChildOf(scroll.content))
                return;
            var viewport = scroll.viewport;
            item.GetWorldCorners(corners);
            float top = viewport.InverseTransformPoint(corners[1]).y;
            float bottom = viewport.InverseTransformPoint(corners[0]).y;
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
