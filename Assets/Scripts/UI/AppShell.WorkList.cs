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
    /// 작업함과 고정 자료 빠른 목록.
    /// 작업함은 휴대전화 자리를 덮는 중앙 PC 업무 화면으로, 고정·분류·메모한 자료 전체를 필터와 함께 보여준다.
    /// 행을 누르면 목록은 그대로 두고 우측 패널에 그 자료의 상세·조작을 띄운다(`원본 열기`를 눌러야 휴대전화로 이동).
    /// 우측 아래 빠른 목록은 최근에 고정한 자료 몇 개만 보여주는 북마크라, 누르면 원본으로 바로 이동한다.
    /// 작업함의 열림·필터·스크롤·선택은 실행 중에만 기억하는 임시 UI 상태라 업무 저장 파일에 넣지 않는다.
    /// </summary>
    public partial class AppShell
    {
        [Header("작업함 진입 (중앙 툴바)")]
        [SerializeField] Button workRecordsButton;
        [SerializeField] TMP_Text workRecordsLabel;
        [SerializeField] Image workRecordsBackground;
        [SerializeField] WorkbenchView workbench;

        /// <summary>작업함 상태 필터. 순서는 필터 버튼 순서와 같다.</summary>
        enum WorkFilter
        {
            All,
            Pinned,
            Keep,
            Delete,
            Memo,
        }

        static readonly string[] FilterKeys =
            { UIKeys.FilterAll, UIKeys.FilterPinned, UIKeys.FilterKeep, UIKeys.FilterDelete, UIKeys.FilterMemo };

        /// <summary>빠른 고정 목록에 보여줄 최대 개수. 전체 고정 수는 제목에 표시한다.</summary>
        const int QuickPinnedMax = 3;

        WorkFilter workFilter;
        /// <summary>의뢰 필터(기기 ID). null이면 모든 의뢰.</summary>
        string caseFilterId;
        readonly List<string> caseFilterIds = new List<string>();
        /// <summary>작업함에서 고른 자료. 우측 패널이 이 자료의 상세·조작을 보여준다. 다시 열어도 유지한다(실행 중에만).</summary>
        RecordRef workbenchSelected;

        bool WorkbenchOpen => workbench.IsOpen;

        string CurrentDeviceId => session.CurrentCase != null ? session.CurrentCase.Id : null;

        /// <summary>
        /// 상단 `작업함 N건`: 작업함을 연다. 처음에는 `전체`·`모든 의뢰`, 같은 실행 중 다시 열면 이전 필터·스크롤·선택 그대로.
        /// 이미 열려 있으면 아무것도 바꾸지 않는다. 비교 중에는 받지 않는다.
        /// </summary>
        void ShowWorkRecords() => OpenWorkbench(null);

        /// <summary>빠른 목록의 `고정 자료 N건` 제목·`작업함 열기`, 비교 1/2 상태 줄에서 연다.</summary>
        void OpenWorkbenchPinned() => OpenWorkbench(WorkFilter.Pinned);

        /// <summary>작업함을 연다. filter가 있으면 그 필터로 바꾸고(목록 맨 위부터), 없으면 이전 상태 그대로.</summary>
        void OpenWorkbench(WorkFilter? filter)
        {
            if (comparing)
                return;
            if (filter.HasValue && filter.Value != workFilter)
            {
                workFilter = filter.Value;
                workbench.ResetScroll();
            }
            if (WorkbenchOpen)
            {
                if (filter.HasValue)
                    RenderWorkbench();
                return;
            }
            if (selecting)
                SetSelecting(false);
            // 검색·작업함·비교는 중앙에 하나만 보인다. 휴대전화 상태와 우측 현재 열람은 그대로 둔다.
            CloseSearch();
            CloseNotifications();
            CloseReviewPopover();
            CloseSummary();
            SaveScroll();
            compareWarnKey = null;
            workbench.Open(CloseWorkbench);
            RefreshWorkPanel();
        }

        /// <summary>`< 현재 화면`·`작업함 닫기`·Esc: 작업함을 닫고 보던 휴대전화 화면과 우측 패널로 돌아간다.</summary>
        void CloseWorkbench()
        {
            if (!WorkbenchOpen)
                return;
            workbench.Close();
            workbench.KeyboardFocus = false;
            compareWarnKey = null;
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            RefreshWorkPanel();
        }

        /// <summary>좌측 의뢰·앱·요청을 누르면 중앙의 검색·작업함·연동 결과 요약을 닫고 휴대전화 화면으로 돌아간다(비교는 좌측을 막으므로 해당 없음).</summary>
        void CloseCenterScreens()
        {
            CloseSearch();
            CloseReviewPopover();
            CloseSummary();
            if (WorkbenchOpen)
            {
                workbench.Close();
                workbench.KeyboardFocus = false;
                compareWarnKey = null;
            }
        }

        /// <summary>작업함 행 본문: 목록은 그대로 두고 우측 패널에 상세를 띄운다. 의뢰 요청은 좌측에서 열 때와 같이 열람으로 기록한다.</summary>
        void SelectWorkbench(RecordRef target)
        {
            if (target.kind == RecordKind.Request
                && RecordAccess.Check(target, config.database, session.Stage, text) == AccessState.Accessible)
                store.MarkViewed(target);
            workbenchSelected = target;
            compareWarnKey = null;
            RefreshWorkPanel();
        }

        /// <summary>고정·분류·메모 중 하나라도 있는 기록 (열람만 한 기록 제외). 목록과 상단 개수가 같은 기준을 쓴다.</summary>
        static bool IsWorked(WorkEntry e) =>
            e.pinned || e.classification != Classification.Unclassified || !string.IsNullOrEmpty(e.memo);

        /// <summary>상단 `작업함 {count}건`: 필터와 무관한 전체 작업함 자료 수(IsWorked 기준), 작업함이 열려 있으면 켜진 상태.</summary>
        void RenderWorkToolbar()
        {
            workRecordsLabel.text = text.Format(UIKeys.ToolbarWorkRecords,
                ("count", store.All.Count(IsWorked).ToString(time.Culture)));
            workRecordsBackground.color = WorkbenchOpen ? Theme.accentSoft : Theme.panelRaised;
            workRecordsLabel.color = WorkbenchOpen ? Theme.accent : Theme.text;
        }

        void SetWorkFilter(int index)
        {
            if ((int)workFilter == index)
                return;
            workFilter = (WorkFilter)index;
            workbench.ResetScroll();
            RenderWorkbench();
        }

        void SetCaseFilter(int index)
        {
            string id = index > 0 && index - 1 < caseFilterIds.Count ? caseFilterIds[index - 1] : null;
            if (id == caseFilterId)
                return;
            caseFilterId = id;
            workbench.ResetScroll();
            RenderWorkbench();
        }

        bool Matches(WorkEntry e)
        {
            if (caseFilterId != null && e.target.deviceId != caseFilterId)
                return false;
            switch (workFilter)
            {
                case WorkFilter.Pinned: return e.pinned;
                case WorkFilter.Keep: return e.classification == Classification.Keep;
                case WorkFilter.Delete: return e.classification == Classification.Delete;
                case WorkFilter.Memo: return !string.IsNullOrEmpty(e.memo);
                default: return true;
            }
        }

        /// <summary>작업함 행·슬롯·의뢰 필터에 쓰는 의뢰 표시명. 원본을 찾지 못한 기록도 저장된 기기 ID로 찾는다.</summary>
        string CaseNameOf(RecordRef target, ResolvedRecord res)
        {
            var device = res.device != null ? res.device
                : config.database.cases.FirstOrDefault(c => c != null && c.Id == target.deviceId);
            return device != null ? device.DisplayName : string.Empty;
        }

        string WorkMeta(RecordRef target, ResolvedRecord res) =>
            text.Format(UIKeys.WorkItemMeta, ("owner", CaseNameOf(target, res)),
                ("code", RecordCode.Format(text, RecordResolver.PrefixFor(target.kind), target.recordId, target.deviceId)));

        /// <summary>작업함·빠른 목록을 다시 그린다. 업무 상태가 바뀔 때마다 RefreshWorkPanel에서 부른다.</summary>
        void RenderWorkList()
        {
            RenderPinnedList();
            if (WorkbenchOpen)
                RenderWorkbench();
        }

        /// <summary>
        /// 우측 아래 `고정 자료 N건`: 최근에 고정한 자료부터 최대 QuickPinnedMax개. 고정 자료가 없어도 제목·`작업함 열기`는 쓸 수 있다.
        /// </summary>
        void RenderPinnedList()
        {
            var pinned = store.RecentlyPinned();
            var quick = pinned.Take(QuickPinnedMax)
                .Select(e => WorkItem(e, RecordResolver.Resolve(e.target, config.database, text), null, OpenPinned))
                .ToList();
            workPanel.ShowPinnedList(quick, text.Format(UIKeys.PanelWorkListHeader, ("count", pinned.Count.ToString(time.Culture))),
                text.Get(UIKeys.PanelPinnedEmpty), RowStyle());
        }

        /// <summary>
        /// 작업함 목록. 고정·분류·메모 중 하나라도 있는 기록만(열람만 한 기록 제외).
        /// 고정한 기록이 위(고정 순서), 나머지는 기기별(현재 기기 먼저)로 묶어 의뢰 요청 → 원본 시각 최신순 → 불변 키 순.
        /// </summary>
        void RenderWorkbench()
        {
            var deviceOrder = Cases().Select((c, i) => (c.Id, i)).ToDictionary(x => x.Id, x => x.i);
            string currentDevice = CurrentDeviceId;

            var worked = store.All
                .Where(IsWorked)
                .Select(e => (entry: e, res: RecordResolver.Resolve(e.target, config.database, text)))
                .ToList();

            int DeviceRank(string id) =>
                id == currentDevice ? -1 : id != null && deviceOrder.TryGetValue(id, out var i) ? i : int.MaxValue;
            int TimeRank((WorkEntry entry, ResolvedRecord res) x) =>
                x.entry.target.kind == RecordKind.Request ? 0 : x.res.found && x.res.hasTime ? 1 : 2;
            int Minutes(ResolvedRecord r) => !r.found || !r.hasTime ? 0 : (r.hasEnd ? r.endTime : r.time).TotalMinutes;

            var ordered = worked.Where(x => x.entry.pinned).OrderBy(x => x.entry.pinOrder)
                .Concat(worked.Where(x => !x.entry.pinned)
                    .OrderBy(x => DeviceRank(x.entry.target.deviceId))
                    .ThenBy(TimeRank)
                    .ThenByDescending(x => Minutes(x.res))
                    .ThenBy(x => x.entry.target.Key, System.StringComparer.Ordinal))
                .ToList();

            // 의뢰 필터 항목: `모든 의뢰` + 데이터의 의뢰 표시명(의뢰 순서). 사라진 의뢰를 가리키던 필터는 해제한다.
            caseFilterIds.Clear();
            caseFilterIds.AddRange(Cases().Select(c => c.Id));
            if (caseFilterId != null && !caseFilterIds.Contains(caseFilterId))
                caseFilterId = null;
            var caseNames = new List<string> { text.Get(UIKeys.FilterAllCases) };
            caseNames.AddRange(Cases().Select(c => c.DisplayName));

            // 필터는 보이는 행만 바꾼다. 고정·분류·메모·비교 슬롯은 그대로다.
            string selectedKey = workbenchSelected?.Key;
            var shown = ordered.Where(x => Matches(x.entry)).Select(x => WorkItem(x.entry, x.res, selectedKey, SelectWorkbench)).ToList();
            bool filtered = workFilter != WorkFilter.All || caseFilterId != null;
            string total = ordered.Count.ToString(time.Culture);
            string focusedRow = workbench.FocusedRowKey();
            workbench.Show(new WorkbenchView.Data
            {
                title = text.Get(UIKeys.FullViewTitle),
                count = filtered
                    ? text.Format(UIKeys.FullViewCountFiltered, ("shown", shown.Count.ToString(time.Culture)), ("total", total))
                    : text.Format(UIKeys.FullViewCount, ("total", total)),
                filters = FilterKeys.Select(k => text.Get(k)).ToArray(),
                filterIndex = (int)workFilter,
                onFilter = SetWorkFilter,
                cases = caseNames,
                caseIndex = caseFilterId == null ? 0 : caseFilterIds.IndexOf(caseFilterId) + 1,
                onCase = SetCaseFilter,
                items = shown,
                emptyText = text.Get(ordered.Count == 0 ? UIKeys.PanelWorkEmpty : UIKeys.FullViewNoMatch),
                rowStyle = RowStyle(),
                chipStyle = new WorkPanelView.ToggleStyle
                {
                    onColor = Theme.accentSoft,
                    onTextColor = Theme.accent,
                    idleColor = Theme.panelRaised,
                    idleTextColor = Theme.text,
                },
            });
            // 다시 그려도 키보드로 보던 행에 포커스를 둔다.
            workbench.FocusRow(focusedRow);
        }

        WorkPanelView.WorkItem WorkItem(WorkEntry e, ResolvedRecord res, string selectedKey, System.Action<RecordRef> onClick)
        {
            var target = e.target;
            // `+ 비교`: 고정과 무관. 칸에 있으면 `비교 중`(다시 누르면 뺌), 칸이 차 있으면 비활성, 열 수 없는 원본이면 숨김.
            // 자동 교체는 하지 않는다.
            var compare = PinnedItemView.CompareState.Hidden;
            if (SlotOf(target.Key) >= 0)
                compare = PinnedItemView.CompareState.InSlot;
            else if (CompareBlockReason(target) == null)
                compare = compareSlots.All(s => s != null) ? PinnedItemView.CompareState.Disabled : PinnedItemView.CompareState.Add;
            return new WorkPanelView.WorkItem
            {
                key = target.Key,
                compare = compare,
                compareLabel = text.Get(compare == PinnedItemView.CompareState.InSlot ? UIKeys.ListCompareOn : UIKeys.ListCompareAdd),
                onCompare = () => ToggleCompareSlot(target),
                // 원본 누락·중복·현재 단계 접근 불가 자료는 실제 제목 대신 중립 문구만 보여준다.
                title = !res.found ? text.Get(res.duplicate ? UIKeys.PanelMissingDuplicate : UIKeys.PanelMissing)
                    : RecordAccess.Check(target, config.database, session.Stage, text) == AccessState.Unavailable
                        ? text.Get(UIKeys.PanelUnavailable) : res.title,
                meta = WorkMeta(target, res),
                badge = e.classification == Classification.Unclassified ? null : text.Get(UIKeys.ClassKey(e.classification)),
                memoMark = string.IsNullOrEmpty(e.memo) ? null : text.Get(UIKeys.ListMemoMark),
                pinned = e.pinned,
                selected = selectedKey != null && selectedKey == target.Key,
                missing = !res.found,
                onClick = () => onClick(target),
            };
        }

        /// <summary>
        /// 빠른 고정 목록 항목: 북마크라 우측 상세를 거치지 않고 원본 위치로 바로 이동한다(이후 복귀는 휴대전화 뒤로가기).
        /// 의뢰 요청은 휴대전화 원본이 없으므로 우측 패널에 열고, 원본 누락·ID 중복·현재 단계 접근 불가면 이동하지 않고
        /// 우측 패널에 이유만 보여준다(제목·내용은 가린 채). 판정은 원본 열기·검색과 같은 RecordAccess/PlanSource다.
        /// </summary>
        void OpenPinned(RecordRef target)
        {
            if (comparing)
                return;
            if (target.kind == RecordKind.Request)
            {
                var request = RecordAccess.Check(target, config.database, session.Stage, text) == AccessState.Accessible
                    ? config.database.requests.FirstOrDefault(q => q != null && q.Id == target.recordId && q.device != null && q.device.Id == target.deviceId)
                    : null;
                // 요청은 우측 패널에만 연다(휴대전화 기기·화면은 그대로).
                if (request != null)
                    OpenRequest(request);
                else
                    OpenWorkRecord(target);
                return;
            }
            if (PlanSource(target, out _, out _, out _))
                OpenSource(target);
            else
                OpenWorkRecord(target);
        }

        WorkPanelView.RowStyle RowStyle() => new WorkPanelView.RowStyle
        {
            selectedColor = Theme.accentSoft,
            idleColor = Theme.panelRaised,
            missingColor = Theme.subText,
            compareOnColor = Theme.accentSoft,
            compareOnTextColor = Theme.accent,
            compareIdleColor = Theme.panel,
            compareIdleTextColor = Theme.accent,
            disabledAlpha = Theme.compareDimAlpha,
        };
    }
}
