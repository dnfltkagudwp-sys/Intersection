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
    /// 작업 기록 목록 (UI-05/06 보완). 평소에는 우측 아래 빠른 목록(패널 높이의 일부), `전체 보기`를 누르면 우측 패널 전체를 목록으로 쓴다.
    /// 전체 보기·필터는 임시 UI 상태라 업무 저장 파일에 넣지 않으며, 재실행하면 간단한 패널과 `전체`·`모든 의뢰`로 시작한다.
    /// 전체 보기 중에도 휴대전화 이동·기기 전환은 그대로 되고, 패널은 목록(또는 연 상세)을 유지한다.
    /// </summary>
    public partial class AppShell
    {
        [Header("작업 기록 진입 (상단 툴바)")]
        [SerializeField] Button workRecordsButton;
        [SerializeField] TMP_Text workRecordsLabel;
        [SerializeField] Image workRecordsBackground;

        /// <summary>전체 보기 상태 필터. 순서는 필터 버튼 순서와 같다.</summary>
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

        bool fullView;
        WorkFilter workFilter;
        /// <summary>의뢰 필터(기기 ID). null이면 모든 의뢰.</summary>
        string caseFilterId;
        readonly List<string> caseFilterIds = new List<string>();
        /// <summary>전체 목록을 마지막으로 그린 기기. 바뀌면 현재 기기 우선 정렬이 달라지므로 맨 위로 보낸다.</summary>
        string fullViewDevice;
        /// <summary>전체 보기에서 마지막으로 연 상세. 목록으로 돌아오면 그 행을 표시하고 키보드 포커스를 둔다.</summary>
        string lastDetailKey;

        string CurrentDeviceId => session.CurrentCase != null ? session.CurrentCase.Id : null;

        /// <summary>
        /// 작업 기록 전체 보기 진입. 상단 툴바 `작업 기록 {count}`와 빠른 목록의 `전체 보기`가 함께 쓴다.
        /// 전체 보기 상세면 목록으로 돌아가고(필터·스크롤 유지), 이미 목록이면 아무것도 바꾸지 않는다. 비교 중에는 받지 않는다.
        /// </summary>
        void ShowWorkRecords()
        {
            if (comparing)
                return;
            if (FullDetailOpen)
                ReturnToFullList();
            else if (!fullView)
                OpenFullView();
        }

        void OpenFullView()
        {
            if (comparing || fullView)
                return;
            if (selecting)
                SetSelecting(false);
            fullView = true;
            focus = PanelFocus.Current;
            focusRecord = null;
            compareWarnKey = null;
            lastDetailKey = null;
            fullViewDevice = CurrentDeviceId;
            workPanel.ResetFullScroll();
            RefreshWorkPanel();
        }

        /// <summary>`< 현재 화면` 또는 Esc: 간단한 패널(현재 열람)로 돌아간다.</summary>
        void CloseFullView()
        {
            fullView = false;
            focus = PanelFocus.Current;
            focusRecord = null;
            compareWarnKey = null;
            RefreshWorkPanel();
        }

        /// <summary>상세의 `< 작업 기록` 또는 Esc: 필터·스크롤을 그대로 둔 전체 목록으로 돌아간다.</summary>
        void ReturnToFullList()
        {
            focus = PanelFocus.Current;
            focusRecord = null;
            compareWarnKey = null;
            RefreshWorkPanel();
        }

        bool FullDetailOpen => fullView && focus != PanelFocus.Current;

        /// <summary>핀·분류·메모 중 하나라도 있는 기록 (열람만 한 기록 제외). 목록과 상단 개수가 같은 기준을 쓴다.</summary>
        static bool IsWorked(WorkEntry e) =>
            e.pinned || e.classification != Classification.Unclassified || !string.IsNullOrEmpty(e.memo);

        /// <summary>상단 툴바 `작업 기록 {count}`: 필터와 무관한 전체 작업 기록 수, 전체 보기 중이면 켜진 상태.</summary>
        void RenderWorkToolbar()
        {
            workRecordsLabel.text = text.Format(UIKeys.ToolbarWorkRecords,
                ("count", store.All.Count(IsWorked).ToString(time.Culture)));
            workRecordsBackground.color = fullView ? Theme.accentSoft : Theme.panelRaised;
            workRecordsLabel.color = fullView ? Theme.accent : Theme.text;
        }

        void SetWorkFilter(int index)
        {
            if ((int)workFilter == index)
                return;
            workFilter = (WorkFilter)index;
            workPanel.ResetFullScroll();
            RefreshWorkPanel();
        }

        void SetCaseFilter(int index)
        {
            string id = index > 0 && index - 1 < caseFilterIds.Count ? caseFilterIds[index - 1] : null;
            if (id == caseFilterId)
                return;
            caseFilterId = id;
            workPanel.ResetFullScroll();
            RefreshWorkPanel();
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

        /// <summary>작업 기록 행·슬롯·의뢰 필터에 쓰는 의뢰 표시명. 원본을 찾지 못한 기록도 저장된 기기 ID로 찾는다.</summary>
        string CaseNameOf(RecordRef target, ResolvedRecord res)
        {
            var device = res.device != null ? res.device
                : config.database.cases.FirstOrDefault(c => c != null && c.Id == target.deviceId);
            return device != null ? device.DisplayName : string.Empty;
        }

        string WorkMeta(RecordRef target, ResolvedRecord res) =>
            text.Format(UIKeys.WorkItemMeta, ("owner", CaseNameOf(target, res)),
                ("code", RecordCode.Format(text, RecordResolver.PrefixFor(target.kind), target.recordId, target.deviceId)));

        /// <summary>
        /// 핀·분류·메모 중 하나라도 있는 기록. 열람만 한 기록은 넣지 않는다.
        /// 핀한 기록이 위(핀 순서), 나머지는 기기별(현재 기기 먼저)로 묶어 의뢰 요청 → 원본 시각 최신순 → 불변 키 순.
        /// 간단한 패널이면 빠른 목록, 전체 보기 목록이면 필터를 적용한 전체 목록을 그린다(상세를 연 동안에는 목록을 그대로 둔다).
        /// </summary>
        void RenderWorkList()
        {
            if (FullDetailOpen)
                return;
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

            if (!fullView)
            {
                var actionKey = ActionTarget?.Key;
                var quick = ordered.Select(x => WorkItem(x.entry, x.res, actionKey)).ToList();
                workPanel.ShowWorkList(quick, text.Format(UIKeys.PanelWorkListHeader, ("count", quick.Count.ToString(time.Culture))),
                    text.Get(UIKeys.PanelWorkEmpty), text.Get(UIKeys.PanelWorkCollapse), text.Get(UIKeys.PanelWorkExpand), RowStyle());
                return;
            }

            // 기기가 바뀌면 현재 기기 우선 정렬이 달라지므로 목록을 맨 위에서 다시 보여준다.
            if (currentDevice != fullViewDevice)
            {
                fullViewDevice = currentDevice;
                workPanel.ResetFullScroll();
            }
            // 의뢰 필터 항목: `모든 의뢰` + 데이터의 의뢰 표시명(의뢰 순서). 사라진 의뢰를 가리키던 필터는 해제한다.
            caseFilterIds.Clear();
            caseFilterIds.AddRange(Cases().Select(c => c.Id));
            if (caseFilterId != null && !caseFilterIds.Contains(caseFilterId))
                caseFilterId = null;
            var caseNames = new List<string> { text.Get(UIKeys.FilterAllCases) };
            caseNames.AddRange(Cases().Select(c => c.DisplayName));

            // 필터는 보이는 행만 바꾼다. 핀·분류·메모·비교 슬롯은 그대로다.
            var shown = ordered.Where(x => Matches(x.entry)).Select(x => WorkItem(x.entry, x.res, lastDetailKey)).ToList();
            bool filtered = workFilter != WorkFilter.All || caseFilterId != null;
            string total = ordered.Count.ToString(time.Culture);
            workPanel.ShowFullList(new WorkPanelView.FullList
            {
                title = text.Get(UIKeys.FullViewTitle),
                count = filtered
                    ? text.Format(UIKeys.FullViewCountFiltered, ("shown", shown.Count.ToString(time.Culture)), ("total", total))
                    : text.Format(UIKeys.FullViewCount, ("total", total)),
                backText = text.Get(UIKeys.PanelBackToCurrent),
                onBack = CloseFullView,
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
        }

        WorkPanelView.WorkItem WorkItem(WorkEntry e, ResolvedRecord res, string selectedKey)
        {
            var target = e.target;
            // `+ 비교`: 핀한 기록만. 칸에 있으면 `비교 중`(다시 누르면 뺌), 칸이 차 있으면 비활성. 자동 교체는 하지 않는다.
            var compare = PinnedItemView.CompareState.Hidden;
            if (e.pinned)
            {
                if (SlotOf(target.Key) >= 0)
                    compare = PinnedItemView.CompareState.InSlot;
                else if (CompareBlockReason(target) == null)
                    compare = compareSlots.All(s => s != null) ? PinnedItemView.CompareState.Disabled : PinnedItemView.CompareState.Add;
            }
            return new WorkPanelView.WorkItem
            {
                key = target.Key,
                compare = compare,
                compareLabel = text.Get(compare == PinnedItemView.CompareState.InSlot ? UIKeys.ListCompareOn : UIKeys.ListCompareAdd),
                onCompare = () => ToggleCompareSlot(target),
                title = !res.found ? text.Get(res.duplicate ? UIKeys.PanelMissingDuplicate : UIKeys.PanelMissing)
                    : RecordAccess.Check(target, config.database, session.Stage, text) == AccessState.Unavailable
                        ? text.Get(UIKeys.PanelUnavailable) : res.title,
                meta = WorkMeta(target, res),
                badge = e.classification == Classification.Unclassified ? null : text.Get(UIKeys.ClassKey(e.classification)),
                memoMark = string.IsNullOrEmpty(e.memo) ? null : text.Get(UIKeys.ListMemoMark),
                pinned = e.pinned,
                selected = selectedKey != null && selectedKey == target.Key,
                missing = !res.found,
                onClick = () => OpenWorkRecord(target),
            };
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
