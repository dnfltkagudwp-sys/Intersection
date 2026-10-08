using System.Collections.Generic;
using System.IO;
using System.Linq;
using Intersection.Core;
using Intersection.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>
    /// 핀·분류·작업메모·기록 고르기·업무 튜토리얼 (UI-05).
    /// 우측 패널은 기본적으로 휴대전화에 열린 기록(현재 열람)을 보여주며 바로 핀·분류·메모할 수 있다.
    /// 기록 고르기 모드는 개별 메시지처럼 화면 안의 더 작은 단위를 고를 때만 쓴다.
    /// 휴대전화 화면에는 고르기 모드에서만 얇은 테두리·체크·핀 표시가 보인다.
    /// </summary>
    public partial class AppShell
    {
        [Header("기록 고르기·작업메모")]
        [SerializeField] Button selectToggle;
        [SerializeField] TMP_Text selectToggleLabel;
        [SerializeField] Image selectToggleBackground;
        [SerializeField] TMP_Text selectStatus;
        [SerializeField] RectTransform requestListRoot;

        /// <summary>우측 패널이 무엇을 보여주는지.</summary>
        enum PanelFocus
        {
            /// <summary>휴대전화에 열린 화면 (현재 열람).</summary>
            Current,
            /// <summary>기록 고르기 모드에서 고른 기록 (선택한 기록).</summary>
            Picked,
            /// <summary>작업 기록 목록에서 다시 연 기록. 휴대전화 화면과 기기는 그대로다.</summary>
            WorkRecord,
            /// <summary>좌측에서 연 의뢰 요청.</summary>
            Request,
        }

        WorkStore store;
        bool selecting;
        PanelFocus focus = PanelFocus.Current;
        RecordRef focusRecord;
        System.Action currentDraw;
        RecordRef currentRecord;
        /// <summary>`원본 열기`로 연 기록. 대화방 안의 개별 메시지처럼 화면보다 작은 단위면 패널은 이 기록을 현재 열람으로 보여준다.</summary>
        RecordRef sourceFocus;
        RecordRef pendingSource;
        bool memoWasFocused;
        /// <summary>모든 단계가 끝난 튜토리얼(시작 시 이미 끝난 것 포함). 완료 안내는 이번 실행에서 새로 끝났을 때만 한 번 띄운다.</summary>
        readonly HashSet<string> finishedTutorials = new HashSet<string>();
        string tutorialDoneLine;
        readonly List<SidebarItemView> requestItems = new List<SidebarItemView>();

        /// <summary>검수·테스트용 업무 상태 저장소.</summary>
        public WorkStore Store => store;

        /// <summary>핀·분류·메모 조작이 적용되는 기록 (패널에 보이는 기록). 전체 보기 목록에는 없다(P·1·2·0 무시).</summary>
        RecordRef ActionTarget => focus != PanelFocus.Current ? focusRecord : fullView ? null : sourceFocus ?? currentRecord;

        void InitWork()
        {
            store = new WorkStore(Path.Combine(Application.persistentDataPath, config.workSaveFileName));
            foreach (var tutorial in config.database.tutorials.Where(t => t != null))
            {
                var progress = TutorialProgress.Evaluate(tutorial, store);
                if (progress.Count > 0 && progress.All(p => p.status == StepStatus.Done))
                    finishedTutorials.Add(tutorial.Id);
            }
            SelectionBus.Reset();
            SelectionBus.IsPinned = store.IsPinned;
            SelectionBus.SelectRequested = Pick;
            selectToggle.onClick.AddListener(() => SetSelecting(!selecting));
            workPanel.BindFullView(ShowWorkRecords);
            workRecordsButton.onClick.AddListener(ShowWorkRecords);
            SyncSelection();
        }

        void OnApplicationQuit() => store?.Save();

        /// <summary>휴대전화 화면을 그리면서 그 화면의 패널 내용과 열린 기록을 등록한다. 실제 표시는 RefreshWorkPanel.</summary>
        void Present(System.Action draw, RecordRef record)
        {
            currentDraw = draw;
            currentRecord = record;
        }

        /// <summary>고르기 모드면 이동 대신 고른다. 골랐으면 true.</summary>
        bool TrySelectInstead(RecordRef record)
        {
            if (!selecting || record == null)
                return false;
            Pick(record);
            return true;
        }

        void SetSelecting(bool on)
        {
            if (selecting == on)
                return;
            selecting = on;
            // 기록 고르기는 휴대전화 화면 안을 고르는 모드라 전체 보기를 닫고 간단한 패널로 돌아간다.
            if (on)
                fullView = false;
            // 고르기를 시작하거나 끝내면 패널은 현재 열람으로 돌아간다.
            SaveScroll();
            RenderCenter();
        }

        /// <summary>앱·기기를 바꿀 때 고르기 모드를 끝낸다 (화면은 호출한 쪽이 다시 그린다).</summary>
        void EndSelectingForNavigation() => selecting = false;

        void Pick(RecordRef record)
        {
            if (!selecting || record == null)
                return;
            focus = PanelFocus.Picked;
            focusRecord = record;
            RefreshWorkPanel();
        }

        /// <summary>작업 기록에서 연 기록을 패널에만 띄운다. 휴대전화 화면·현재 기기는 바꾸지 않는다.</summary>
        void OpenWorkRecord(RecordRef target)
        {
            if (selecting)
                SetSelecting(false);
            // 휴대전화에 열린 기록과 같아도 작업 기록으로 띄운다(원본 열기·비교에 추가 같은 보조 행동은 여기서만 제공).
            focus = PanelFocus.WorkRecord;
            focusRecord = target;
            lastDetailKey = target.Key;
            compareWarnKey = null;
            RefreshWorkPanel();
        }

        /// <summary>`< 현재 화면` 또는 Esc: 패널만 현재 열람으로 되돌린다.</summary>
        void ReturnPanelToCurrent()
        {
            focus = PanelFocus.Current;
            focusRecord = null;
            RefreshWorkPanel();
        }

        bool PanelShowsOther => focus == PanelFocus.WorkRecord || focus == PanelFocus.Request;

        void SyncSelection()
        {
            // 비교 화면에서는 고르기 표시를 끈다(비교 기록을 선택 표시로 남기지 않음).
            SelectionBus.Set(selecting && !comparing, focus == PanelFocus.Picked ? focusRecord?.Key : null);
            selectToggleLabel.text = text.Get(selecting ? UIKeys.ToolbarSelectEnd : UIKeys.ToolbarSelectStart);
            selectToggleBackground.color = selecting ? Theme.accentSoft : Theme.panelRaised;
            selectStatus.gameObject.SetActive(selecting);
            selectStatus.text = !selecting ? string.Empty
                : focus == PanelFocus.Picked && focusRecord != null
                    ? text.Format(UIKeys.ToolbarPicked, ("kind", text.Get(UIKeys.KindKey(focusRecord.kind))))
                    : text.Get(UIKeys.ToolbarPickHint);
        }

        /// <summary>업무 상태가 바뀐 뒤: 화면 표시(체크·핀), 패널, 작업 기록, 튜토리얼을 다시 그린다.</summary>
        void AfterWorkChange()
        {
            SelectionBus.Notify();
            RefreshWorkPanel();
        }

        /// <summary>
        /// RenderCenter 뒤에 호출된다. 패널이 보여줄 대상에 따라 내용·머리말·조작을 그린다.
        /// 전체 보기면 focus가 Current일 때 목록, 다른 기록이면 그 상세(`< 작업 기록`)를 보여준다.
        /// 다시 그린 뒤 패널에 있던 키보드 포커스를 같은 기록·조작으로 되살린다.
        /// </summary>
        void RefreshWorkPanel()
        {
            var keyboardFocus = workPanel.CaptureFocus();
            if (fullView && selecting)
                fullView = false;
            if (focus == PanelFocus.Picked && !selecting)
                focus = PanelFocus.Current;
            if (focus != PanelFocus.Current && focusRecord == null)
                focus = PanelFocus.Current;
            // 비교 슬롯 정리는 보조 행동(비교에 추가/빼기)을 그리기 전에 한다.
            PruneCompareSlots();

            bool fullList = fullView && focus == PanelFocus.Current;
            workPanel.SetMode(!fullView ? WorkPanelView.Mode.Simple
                : fullList ? WorkPanelView.Mode.FullList : WorkPanelView.Mode.FullDetail);
            if (fullList)
            {
                // 목록은 RenderWorkList가 그린다.
            }
            else if (focus == PanelFocus.Current)
            {
                if (sourceFocus != null && (currentRecord == null || sourceFocus.Key != currentRecord.Key))
                    DrawRecord(sourceFocus);
                else
                    currentDraw?.Invoke();
                workPanel.SetHeading(text.Get(UIKeys.PanelHeadingCurrent), null);
            }
            else
            {
                DrawRecord(focusRecord);
                string heading = text.Get(focus == PanelFocus.Picked ? UIKeys.PanelHeadingPicked
                    : focus == PanelFocus.Request ? UIKeys.PanelHeadingRequest : UIKeys.PanelHeadingWork);
                // 전체 보기 상세는 돌아가기가 `< 작업 기록`이므로 머리말에는 기록 종류(사진·의뢰 요청 등)를 보여준다.
                if (fullView)
                    workPanel.SetHeading(text.Get(UIKeys.KindKey(focusRecord.kind)), ReturnToFullList, text.Get(UIKeys.PanelBackToList));
                else
                    workPanel.SetHeading(heading, PanelShowsOther ? ReturnPanelToCurrent : (System.Action)null,
                        text.Get(UIKeys.PanelBackToCurrent));
            }
            RenderRecordTools();

            var target = ActionTarget;
            workPanel.SetActions(target != null ? ActionsFor(target) : null);
            RenderWorkList();
            RenderCompareSlots();
            RenderTutorialLine();
            RenderWorkToolbar();
            SyncSelection();
            workPanel.ApplyNavigation();
            workPanel.RestoreFocus(keyboardFocus, fullList ? lastDetailKey : null);
        }

        WorkPanelView.Actions ActionsFor(RecordRef target) => new WorkPanelView.Actions
        {
            pinned = store.IsPinned(target.Key),
            classification = store.ClassOf(target.Key),
            memo = store.MemoOf(target.Key),
            pinText = text.Get(UIKeys.ActionPin),
            unpinText = text.Get(UIKeys.ActionUnpin),
            primaryColor = Theme.accentSoft,
            primaryTextColor = Theme.accent,
            onColor = Theme.accentSoft,
            onTextColor = Theme.accent,
            idleColor = Theme.panelRaised,
            idleTextColor = Theme.text,
            togglePin = () => TogglePin(target),
            toggleClass = c => ToggleClass(target, c),
            memoChanged = memo =>
            {
                store.SetMemo(target, memo);
                RenderWorkList();
            },
        };

        void TogglePin(RecordRef target)
        {
            store.SetPinned(target, !store.IsPinned(target.Key));
            AfterWorkChange();
        }

        /// <summary>보존·삭제는 토글이다. 켜진 쪽을 다시 누르면 미분류로 돌아가고, 둘은 동시에 켜지지 않는다.</summary>
        void ToggleClass(RecordRef target, Classification c)
        {
            SetClass(target, store.ClassOf(target.Key) == c ? Classification.Unclassified : c);
        }

        void SetClass(RecordRef target, Classification c)
        {
            store.SetClassification(target, c);
            // 분류 튜토리얼 단계는 진행 중일 때 한 분류 행동으로만 완료된다.
            foreach (var tutorial in config.database.tutorials.Where(t => t != null))
                TutorialProgress.OnClassified(tutorial, store, target, c);
            AfterWorkChange();
        }

        // ───────────── 원본 열기 ─────────────

        /// <summary>
        /// 작업 기록(또는 의뢰 요청)을 패널에 띄웠을 때의 보조 행동: `원본 열기`와 `비교에 추가`/`비교에서 빼기`.
        /// 의뢰 요청은 휴대전화 자료가 아니라 원본 열기를 숨긴다. 열거나 비교할 수 없으면 이유를 한 번만 보여준다.
        /// </summary>
        void RenderRecordTools()
        {
            if ((focus != PanelFocus.WorkRecord && focus != PanelFocus.Request) || focusRecord == null)
            {
                workPanel.SetRecordTools(null, null, null, null, null, null);
                return;
            }
            var target = focusRecord;
            string sourceText = null, sourceNote = null;
            System.Action onSource = null;
            if (focus == PanelFocus.WorkRecord && target.kind != RecordKind.Request)
            {
                if (PlanSource(target, out _, out _, out var reasonKey))
                {
                    sourceText = text.Get(target.kind == RecordKind.LocationShare ? UIKeys.ActionOpenSourceMessage : UIKeys.ActionOpenSource);
                    onSource = () => OpenSource(target);
                }
                else if (reasonKey != null)
                    sourceNote = text.Get(reasonKey);
            }
            CompareTool(target, out var compareText, out var onCompare, out var compareNote);
            // 같은 이유(원본 없음 등)로 둘 다 막혔으면 "열거나 비교할 수 없음" 한 줄만 보여준다.
            if (sourceNote != null && onCompare == null && compareNote != null)
                sourceNote = null;
            workPanel.SetRecordTools(sourceText, onSource, sourceNote, compareText, onCompare, compareNote);
        }

        /// <summary>대화방 전체는 날짜 범위, 위치 기록은 시각 범위, 나머지는 한 시점.</summary>
        WorkPanelView.Row TimeRow(RecordRef target, ResolvedRecord res) =>
            !res.hasEnd
                ? Row(UIKeys.PanelRecordTime, time.SinceDeathWithTime(res.time), Theme.regularFont)
                : Row(UIKeys.PanelPeriod, target.kind == RecordKind.Thread
                    ? time.SinceDeathRange(res.time, res.endTime)
                    : time.SinceDeathTimeRange(res.time, res.endTime), Theme.regularFont);

        /// <summary>
        /// 저장된 불변 ID를 해석해 원본이 있는 기기·앱·화면 상태를 계산한다. 열 수 없으면 false와 이유 키.
        /// 기록 정체성(RecordRef)은 바꾸지 않고 탐색 상태만 만든다.
        /// </summary>
        bool PlanSource(RecordRef target, out CaseData device, out System.Action<DeviceNavigation> apply, out string reasonKey)
        {
            apply = null;
            reasonKey = null;
            var db = config.database;
            var stage = session.Stage;
            var res = RecordResolver.Resolve(target, db, text);
            device = res.device;
            if (!res.found)
            {
                reasonKey = res.duplicate ? UIKeys.SourceDuplicate : UIKeys.SourceMissing;
                return false;
            }
            if (!device.IsAvailable(stage))
            {
                reasonKey = UIKeys.SourceUnavailable;
                return false;
            }
            string id = target.recordId;
            switch (target.kind)
            {
                case RecordKind.Thread:
                case RecordKind.Message:
                case RecordKind.Attachment:
                case RecordKind.LocationShare:
                {
                    // 개별 메시지·첨부·위치 공유는 대화방을 열고 그 메시지 위치로 스크롤한다.
                    string threadId = target.kind == RecordKind.Thread ? id : target.threadId;
                    string focusId = target.kind == RecordKind.Thread ? null : id;
                    if (!DeviceQuery.Threads(db, device, stage).Any(e => e.thread.Id == threadId))
                        break;
                    apply = nav =>
                    {
                        nav.app = AppKind.Messages;
                        nav.openThreadId = threadId;
                        nav.focusMessageId = focusId;
                        nav.scroll.Remove(threadId);
                    };
                    return true;
                }
                case RecordKind.Photo:
                {
                    var all = PhotoQuery.DevicePhotos(db, device, stage);
                    var photo = all.FirstOrDefault(p => p.Id == id);
                    // 사진이 들어 있는 첫 앨범(앨범 순서 기준)에서 한 장 보기로 연다.
                    var album = photo == null ? null
                        : PhotoQuery.Albums(db, device).FirstOrDefault(a => PhotoQuery.AlbumPhotos(a, all).Contains(photo));
                    if (album == null)
                        break;
                    apply = nav =>
                    {
                        nav.app = AppKind.Photos;
                        nav.photoAlbumId = album.Id;
                        nav.openPhotoId = photo.Id;
                    };
                    return true;
                }
                case RecordKind.Browser:
                    if (RecordQuery.For(db.browser, device, stage).Any(r => r.Id == id))
                        return OpenRecordPlan(AppKind.Browser, id, out apply);
                    break;
                case RecordKind.Map:
                    if (RecordQuery.For(db.maps, device, stage).Any(r => r.Id == id))
                        return OpenRecordPlan(AppKind.Maps, id, out apply);
                    break;
                case RecordKind.Setting:
                    if (RecordQuery.For(db.settings, device, stage).Any(r => r.Id == id))
                        return OpenRecordPlan(AppKind.Settings, id, out apply);
                    break;
                case RecordKind.File:
                {
                    // 파일의 출처(위치)와 폴더로 이동해 상세를 연다.
                    var file = RecordQuery.For(db.files, device, stage).FirstOrDefault(f => f.Id == id);
                    if (file == null)
                        break;
                    apply = nav =>
                    {
                        nav.app = AppKind.Files;
                        nav.filesLocation = file.source;
                        nav.filesPath = file.FolderPath;
                        nav.SetOpenRecord(AppKind.Files, id);
                    };
                    return true;
                }
                default:
                    return false;
            }
            reasonKey = UIKeys.SourceUnavailable;
            return false;
        }

        static bool OpenRecordPlan(AppKind app, string id, out System.Action<DeviceNavigation> apply)
        {
            apply = nav =>
            {
                nav.app = app;
                nav.SetOpenRecord(app, id);
            };
            return true;
        }

        /// <summary>
        /// 사용자가 `원본 열기`를 눌렀을 때만 기기·앱을 바꿔 원본을 연다. 바꾸기 전 화면 상태는 기기별로 그대로 보존된다.
        /// 연 뒤 패널은 현재 열람으로 돌아가고, 고르기 모드는 켜지지 않으며, 열람 외 업무 상태는 바꾸지 않는다.
        /// </summary>
        void OpenSource(RecordRef target)
        {
            if (!PlanSource(target, out var device, out var apply, out _))
                return;
            SaveScroll();
            // 원본은 중앙 휴대전화에 열리므로 전체 보기를 끝낸다.
            fullView = false;
            EndSelectingForNavigation();
            session.CurrentCase = device;
            apply(session.Nav(device));
            if (target.kind == RecordKind.Message || target.kind == RecordKind.Attachment || target.kind == RecordKind.LocationShare)
            {
                // 메시지를 열면 그 대화방도 연 것이므로 목록에서 대화방을 열 때와 같이 열람으로 기록한다.
                var thread = config.database.threads.FirstOrDefault(t => t != null && t.Id == target.threadId);
                if (thread != null)
                    store.MarkViewed(RecordRef.ForThread(device, thread));
            }
            store.MarkViewed(target);
            pendingSource = target;
            Refresh();
        }

        // ───────────── 패널에 띄운 기록 (고른 기록·작업 기록·의뢰 요청) ─────────────

        void DrawRecord(RecordRef target)
        {
            var res = RecordResolver.Resolve(target, config.database, text);
            var entry = store.Get(target.Key);
            string code = RecordCode.Format(text, RecordResolver.PrefixFor(target.kind), target.recordId, target.deviceId);
            if (!res.found)
            {
                // 원본이 없어도 다른 기록으로 대체하지 않고, 저장된 ID 그대로 누락 상태로 보여준다.
                workPanel.Show(text.Get(res.duplicate ? UIKeys.PanelMissingDuplicate : UIKeys.PanelMissing),
                    text.Get(UIKeys.PanelMissingBody), new[] { Row(UIKeys.PanelRecordId, code, Theme.monoFont) });
                return;
            }

            var rows = new List<WorkPanelView.Row>
            {
                Row(UIKeys.PanelRecordId, code, Theme.monoFont),
                Row(UIKeys.PanelOwner, OwnerName(res.device), Theme.regularFont),
            };
            if (target.kind != RecordKind.Request)
                rows.Add(Row(UIKeys.PanelSource, SourceText(res.source, res.serviceKey), Theme.regularFont));
            if (res.hasTime)
                rows.Add(TimeRow(target, res));
            rows.Add(Row(UIKeys.PanelIntegrity, string.IsNullOrEmpty(res.integrityKey) ? null : text.Get(res.integrityKey), Theme.regularFont));
            rows.Add(Row(UIKeys.PanelViewed, text.Get(entry != null && entry.viewed ? UIKeys.ViewedYes : UIKeys.ViewedNo), Theme.regularFont));
            if (target.kind == RecordKind.Request)
                rows.AddRange(TutorialRows(target));
            workPanel.Show(res.title, res.body, rows);
        }

        // ───────────── 의뢰 요청·튜토리얼 ─────────────

        IEnumerable<WorkRequestData> Requests(CaseData device) =>
            config.database.requests
                .Where(q => q != null && q.device == device && session.Stage >= q.availableFrom)
                .OrderBy(q => q.order);

        void RenderRequests()
        {
            foreach (var item in requestItems)
                UIPool.Discard(item.gameObject);
            requestItems.Clear();
            var device = session.CurrentCase;
            if (device == null)
                return;
            foreach (var request in Requests(device))
            {
                var q = request;
                var item = Instantiate(appItemPrefab, requestListRoot);
                item.Bind(null, q.title, null, false, true, Theme.accentSoft, Color.clear, () => OpenRequest(q));
                requestItems.Add(item);
            }
        }

        /// <summary>의뢰 요청은 업무 프로그램 기록이라 휴대전화가 아니라 우측 패널에 열리고, 바로 핀할 수 있다.</summary>
        void OpenRequest(WorkRequestData request)
        {
            if (selecting)
                SetSelecting(false);
            var target = RecordRef.ForRequest(request);
            store.MarkViewed(target);
            focus = PanelFocus.Request;
            focusRecord = target;
            lastDetailKey = target.Key;
            RefreshWorkPanel();
        }

        IEnumerable<WorkPanelView.Row> TutorialRows(RecordRef request)
        {
            foreach (var tutorial in config.database.tutorials.Where(t => t != null && t.request != null
                         && RecordRef.ForRequest(t.request).Key == request.Key))
            {
                foreach (var (step, status) in TutorialProgress.Evaluate(tutorial, store))
                    yield return new WorkPanelView.Row { label = step.label, value = StatusText(status), font = Theme.regularFont };
            }
        }

        string StatusText(StepStatus s) =>
            text.Get(s == StepStatus.Done ? UIKeys.TutorialDone : s == StepStatus.Active ? UIKeys.TutorialActive : UIKeys.TutorialWaiting);

        /// <summary>
        /// 진행 중인 업무 튜토리얼 한 줄: 과제 · 단계 번호/전체 · 지금 할 행동.
        /// 이번 실행 중에 모든 단계가 끝나면 `{과제} 업무 완료`를 한 번 보여주고, 휴대전화 화면을 이동하면 숨긴다.
        /// </summary>
        void RenderTutorialLine()
        {
            string line = null;
            foreach (var tutorial in config.database.tutorials.Where(t => t != null && t.request != null
                         && session.Stage >= t.request.availableFrom))
            {
                var progress = TutorialProgress.Evaluate(tutorial, store);
                int index = progress.FindIndex(p => p.status == StepStatus.Active);
                if (index < 0)
                {
                    if (progress.Count > 0 && finishedTutorials.Add(tutorial.Id))
                        tutorialDoneLine = text.Format(UIKeys.TutorialDoneLine, ("task", tutorial.title));
                    continue;
                }
                line = text.Format(UIKeys.TutorialLine, ("task", tutorial.title),
                    ("index", (index + 1).ToString(time.Culture)), ("total", progress.Count.ToString(time.Culture)),
                    ("step", progress[index].step.label));
                break;
            }
            workPanel.ShowTutorial(line ?? tutorialDoneLine);
        }

        // ───────────── 키보드 ─────────────

        /// <summary>
        /// Esc 우선순위: 비교 화면이면 비교 종료 → 메모 입력 중이면 입력값을 저장하고 포커스만 해제
        /// → 전체 보기 상세면 전체 목록으로 → 전체 목록이면 간단한 패널로 → 패널에 다른 기록을 띄웠으면 현재 열람으로
        /// → 고르기 모드면 종료 → 휴대전화 뒤로. 한 번의 Esc는 한 단계만 처리하고 아래 단계로 넘기지 않는다.
        /// </summary>
        void HandleEscape()
        {
            if (comparing)
                ExitCompare();
            else if (workPanel.IsEditingMemo || memoWasFocused)
                workPanel.EndMemoEdit();
            else if (FullDetailOpen)
                ReturnToFullList();
            else if (fullView)
                CloseFullView();
            else if (PanelShowsOther)
                ReturnPanelToCurrent();
            else if (selecting)
                SetSelecting(false);
            else
                Back();
        }

        /// <summary>패널에 보이는 기록에 적용: P 핀, 1 보존 토글, 2 삭제 토글, 0 미분류. 입력 중에는 무시한다.</summary>
        void HandleWorkKeys(UnityEngine.InputSystem.Keyboard keyboard)
        {
            var target = ActionTarget;
            if (target == null || IsTyping())
                return;
            if (keyboard.pKey.wasPressedThisFrame)
                TogglePin(target);
            else if (keyboard.digit1Key.wasPressedThisFrame)
                ToggleClass(target, Classification.Keep);
            else if (keyboard.digit2Key.wasPressedThisFrame)
                ToggleClass(target, Classification.Delete);
            else if (keyboard.digit0Key.wasPressedThisFrame)
                SetClass(target, Classification.Unclassified);
        }

        bool IsTyping()
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            var current = events != null ? events.currentSelectedGameObject : null;
            var field = current != null ? current.GetComponent<TMP_InputField>() : null;
            return field != null && field.isFocused;
        }
    }
}
