using System.Collections.Generic;
using System.Linq;
using Intersection.Core;
using Intersection.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Intersection.UI
{
    /// <summary>
    /// 시작 흐름: 타이틀 → (새 게임) 유족 요청 도입 장면 → 업무 화면. 둘 다 엔딩처럼 업무 화면을 완전히 덮는 전체 화면이다.
    /// 타이틀 표시는 저장을 바꾸지 않는다. 도입 장면 대상은 요청 데이터(introScene)로 정하며, '확인하고 시작'을 눌러야
    /// 기존 요청 열람으로 기록된다. 확인 전에 종료하면 다음에 그 의뢰를 검토할 때 다시 나온다.
    /// </summary>
    public partial class AppShell
    {
        enum EntryScreen
        {
            None,
            Title,
            /// <summary>새 게임의 업무 배정 안내(UI 문구, 요청 레코드 아님). 확인하면 첫 요청 장면으로 간다.</summary>
            Briefing,
            Request,
        }

        EntryScreen entryScreen;
        TitleScreenView titleView;
        RequestSceneView requestView;
        WorkRequestData introRequest;
        int entryOpenedFrame;

        /// <summary>
        /// 이번 실행을 시작할 때 이미 그 의뢰의 업무를 진행한 기존 저장(요청은 미열람)이라 도입 장면을 소급하지 않는 요청 ID.
        /// </summary>
        readonly HashSet<string> introSkipped = new HashSet<string>();

        /// <summary>새로 시작하기로 저장을 초기화하고 씬을 다시 연 직후면 타이틀을 건너뛰고 첫 요청 장면으로 간다(이번 실행 안에서만 쓰는 값).</summary>
        static bool newGameAfterReload;

        bool EntryActive => entryScreen != EntryScreen.None;

        /// <summary>검수용: 지금 보이는 시작 화면 이름(None·Title·Briefing·Request).</summary>
        public string DebugEntryScreen => entryScreen.ToString();
        public TitleScreenView DebugTitleView => titleView;
        public RequestSceneView DebugRequestView => requestView;

        Canvas RootCanvas
        {
            get
            {
                var canvas = GetComponentInParent<Canvas>();
                return canvas != null ? canvas.rootCanvas : null;
            }
        }

        void BeginEntry()
        {
            MarkLegacyIntros();
            if (newGameAfterReload)
            {
                newGameAfterReload = false;
                OpenBriefing();
                return;
            }
            OpenTitle();
        }

        /// <summary>시작 화면을 닫은 뒤 원래 시작 처리: 확인하지 않은 연락·처리안·연동 결과 요약을 이어서 보여준다.</summary>
        void ResumeAfterEntry()
        {
            if (!ShowPendingFinale())
                ShowPendingSummary();
        }

        /// <summary>요청은 아직 읽지 않았지만 그 의뢰의 업무가 이미 진행된 저장이면 도입 장면을 강제하지 않는다.</summary>
        void MarkLegacyIntros()
        {
            introSkipped.Clear();
            foreach (var request in config.database.requests.Where(q => q != null && q.introScene && q.device != null))
            {
                if (store.IsViewed(RecordRef.ForRequest(request).Key))
                    continue;
                bool jobsStarted = progression.JobsForRequest(request).Any(j => progression.StateOf(j) != JobState.NotStarted);
                bool recordsViewed = store.All.Any(e => e.viewed && e.target != null && e.target.deviceId == request.device.Id
                                                        && e.target.kind != RecordKind.Request);
                if (jobsStarted || recordsViewed)
                    introSkipped.Add(request.Id);
            }
        }

        // ───────────── 타이틀 ─────────────

        enum SaveState
        {
            None,
            InProgress,
            Completed,
        }

        SaveState CurrentSaveState()
        {
            if (progressStore.FinalProcessing != null)
                return SaveState.Completed;
            // 새로 시작하기 직후처럼 시작 단계 그대로이고 아무 진행도 없는 진행 파일은 저장 없음으로 본다(시작하기 → 업무 배정 안내부터).
            bool progressSaved = System.IO.File.Exists(progressStore.FilePath)
                                 && (progressStore.Stage != config.EffectiveStartStage || progressStore.JobIds.Any()
                                     || progressStore.Notifications.Count > 0 || progressStore.Summaries.Count > 0
                                     || progressStore.FinalPlanIntroduced);
            bool workSaved = store.All.Any(e => !e.IsEmpty) || store.Comparisons.Count > 0;
            return progressSaved || workSaved ? SaveState.InProgress : SaveState.None;
        }

        void OpenTitle()
        {
            var canvas = RootCanvas;
            if (canvas == null)
            {
                ResumeAfterEntry();
                return;
            }
            var menu = new List<TitleScreenView.MenuItem>();
            switch (CurrentSaveState())
            {
                case SaveState.None:
                    menu.Add(new TitleScreenView.MenuItem { label = text.Get(UIKeys.TitleStart), primary = true, onClick = StartNewFromTitle });
                    break;
                case SaveState.InProgress:
                    menu.Add(new TitleScreenView.MenuItem { label = text.Get(UIKeys.TitleContinue), primary = true, onClick = StartFromTitle });
                    menu.Add(new TitleScreenView.MenuItem { label = text.Get(UIKeys.TitleNewGame), confirmReset = true, onClick = StartNewGame });
                    break;
                case SaveState.Completed:
                    menu.Add(new TitleScreenView.MenuItem { label = text.Get(UIKeys.TitleResult), primary = true, onClick = ShowResultFromTitle });
                    menu.Add(new TitleScreenView.MenuItem { label = text.Get(UIKeys.TitleNewGame), confirmReset = true, onClick = StartNewGame });
                    break;
            }
            menu.Add(new TitleScreenView.MenuItem { label = text.Get(UIKeys.TitleQuit), onClick = Quit });

            var content = new TitleScreenContent
            {
                title = text.Get(UIKeys.TitleName),
                subtitle = text.Get(UIKeys.TitleSubtitle),
                resetHeading = text.Get(UIKeys.TitleResetHeading),
                resetBody = text.Get(UIKeys.TitleResetBody),
                resetConfirm = text.Get(UIKeys.TitleResetConfirm),
                resetCancel = text.Get(UIKeys.TitleResetCancel),
                resetFailed = text.Get(UIKeys.TitleResetFailed),
            };
            PrepareEntryCover();
            entryScreen = EntryScreen.Title;
            titleView = TitleScreenView.Create(canvas, Theme, content, menu);
        }

        void CloseTitle()
        {
            if (titleView != null)
                Destroy(titleView.gameObject);
            titleView = null;
            entryScreen = EntryScreen.None;
        }

        /// <summary>시작하기·계속하기: 기존 진행을 그대로 쓰고, 지금 의뢰의 요청을 아직 확인하지 않았으면 도입 장면부터 보여준다.</summary>
        void StartFromTitle()
        {
            if (entryScreen != EntryScreen.Title || !EntryInputReady())
                return;
            CloseTitle();
            if (ShowPendingIntro())
                return;
            EndEntryCover();
            ResumeAfterEntry();
        }

        /// <summary>시작하기(저장 없음): 업무 배정 안내부터 보여준다. 진행 중인 저장의 계속하기에는 소급하지 않는다.</summary>
        void StartNewFromTitle()
        {
            if (entryScreen != EntryScreen.Title || !EntryInputReady())
                return;
            CloseTitle();
            OpenBriefing();
        }

        /// <summary>처리 결과 보기: 저장된 처리 스냅샷의 엔딩을 복원한다(최초 연출을 이미 봤으면 마지막 화면만).</summary>
        void ShowResultFromTitle()
        {
            if (entryScreen != EntryScreen.Title || !EntryInputReady() || progressStore.FinalProcessing == null)
                return;
            CloseTitle();
            EndEntryCover();
            OpenEndingScene();
        }

        /// <summary>
        /// 새로 시작하기(확인 뒤): 업무 저장과 진행 저장을 함께 초기화한다. 하나라도 실패하면 둘 다 이전 상태로 두고 타이틀에 남는다.
        /// 성공하면 같은 씬을 다시 열어 화면·탐색 상태를 처음부터 만들고 첫 요청 장면으로 간다. 언어·표시 설정은 저장과 별개라 그대로다.
        /// </summary>
        void StartNewGame()
        {
            if (entryScreen != EntryScreen.Title || !EntryInputReady())
                return;
            if (!store.ResetAll(out var undoWork))
            {
                titleView?.ShowResetFailed();
                return;
            }
            if (!progressStore.ResetAll(config.EffectiveStartStage))
            {
                if (!undoWork())
                    Debug.LogError("[AppShell] 진행 저장 초기화 실패 뒤 업무 저장을 되돌리지 못했습니다.");
                titleView?.ShowResetFailed();
                return;
            }
            newGameAfterReload = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void Quit()
        {
            if (entryScreen != EntryScreen.Title || !EntryInputReady())
                return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ───────────── 업무 배정 안내 ─────────────

        /// <summary>
        /// 새 게임에서 첫 요청 장면 전에 보여주는 업무 배정 안내. 요청 장면과 같은 화면 구성·문장 페이드·버튼을 쓰며 문구는 UI 문자열이다.
        /// 확인해도 요청 열람·분류·작업 시작을 기록하지 않는다.
        /// </summary>
        void OpenBriefing()
        {
            var canvas = RootCanvas;
            if (canvas == null)
            {
                if (!ShowPendingIntro())
                    ResumeAfterEntry();
                return;
            }
            PrepareEntryCover();
            entryScreen = EntryScreen.Briefing;
            requestView = RequestSceneView.Create(canvas, Theme, text.Get(UIKeys.BriefingHeading), text.Get(UIKeys.BriefingBody), 0,
                text.Get(UIKeys.BriefingConfirm), ConfirmBriefing);
        }

        void ConfirmBriefing()
        {
            if (entryScreen != EntryScreen.Briefing || !EntryInputReady())
                return;
            if (requestView != null)
                Destroy(requestView.gameObject);
            requestView = null;
            entryScreen = EntryScreen.None;
            if (ShowPendingIntro())
                return;
            EndEntryCover();
            ResumeAfterEntry();
        }

        // ───────────── 유족 요청 도입 장면 ─────────────

        /// <summary>지금 의뢰에서 처음 확인할 요청(데이터의 introScene)이 있으면 도입 장면을 연다.</summary>
        bool ShowPendingIntro()
        {
            var request = PendingIntro(session.CurrentCase);
            if (request == null)
                return false;
            OpenRequestScene(request);
            return true;
        }

        WorkRequestData PendingIntro(CaseData device) =>
            device == null || !device.IsAvailable(session.Stage) ? null
            : Requests(device).FirstOrDefault(q => q.introScene && !introSkipped.Contains(q.Id)
                                                    && !store.IsViewed(RecordRef.ForRequest(q).Key));

        void OpenRequestScene(WorkRequestData request)
        {
            var canvas = RootCanvas;
            if (canvas == null || request == null)
                return;
            PrepareEntryCover();
            entryScreen = EntryScreen.Request;
            introRequest = request;
            string heading = text.Format(UIKeys.RequestIntroHeading,
                ("case", request.device != null ? request.device.DisplayName : string.Empty));
            requestView = RequestSceneView.Create(canvas, Theme, heading, request.Body, request.introDetailFromLine,
                text.Get(UIKeys.RequestIntroConfirm), ConfirmRequestScene);
        }

        /// <summary>확인하고 시작: 기존 요청 열람을 기록하고 업무 화면으로 돌아간다. 분류·작업 시작은 하지 않는다.</summary>
        void ConfirmRequestScene()
        {
            if (entryScreen != EntryScreen.Request || introRequest == null || !EntryInputReady())
                return;
            var request = introRequest;
            if (requestView != null)
                Destroy(requestView.gameObject);
            requestView = null;
            introRequest = null;
            entryScreen = EntryScreen.None;
            EndEntryCover();
            store.MarkViewed(RecordRef.ForRequest(request));
            RefreshWorkPanel();
            ResumeAfterEntry();
        }

        // ───────────── 입력 ─────────────

        /// <summary>
        /// 시작 화면이 떠 있으면 그 화면만 갱신하고 true(업무 단축키·Esc·휴대전화 조작 없음).
        /// 업무 중에는 다른 전체 화면이 없을 때 지금 의뢰에 처음 확인할 요청이 생겼는지(B 의뢰 첫 검토) 확인한다.
        /// </summary>
        bool UpdateEntry()
        {
            if (entryScreen == EntryScreen.Title)
            {
                titleView?.Tick();
                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && titleView != null && titleView.ConfirmOpen)
                    titleView.CloseConfirm();
                return true;
            }
            if (entryScreen == EntryScreen.Request || entryScreen == EntryScreen.Briefing)
            {
                requestView?.Tick();
                return true;
            }
            if (comparing || presenting || finaleScreen != FinaleScreen.None || SummaryOpen)
                return false;
            return ShowPendingIntro();
        }

        /// <summary>시작 화면을 연 프레임의 클릭·Enter가 그 화면의 버튼으로 이어지지 않게 한 프레임 뒤부터 받는다.</summary>
        bool EntryInputReady() => Time.frameCount > entryOpenedFrame;

        /// <summary>업무 화면을 덮기 전: 열린 팝업·고르기·검색·작업함을 닫고 좌우·상단 입력과 선택을 막는다.</summary>
        void PrepareEntryCover()
        {
            entryOpenedFrame = Time.frameCount;
            CloseNotifications();
            CloseReviewPopover();
            if (selecting)
                SetSelecting(false);
            CloseCenterScreens();
            SetPresentationInputBlocked(true);
            EventSystem.current?.SetSelectedGameObject(null);
        }

        void EndEntryCover()
        {
            SetPresentationInputBlocked(false);
            EventSystem.current?.SetSelectedGameObject(null);
            RenderTopBar();
        }
    }
}
