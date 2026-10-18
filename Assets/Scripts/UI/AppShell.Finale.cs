using System.Collections.Generic;
using System.Linq;
using Intersection.Core;
using Intersection.Data;
using UnityEngine;

namespace Intersection.UI
{
    public partial class AppShell
    {
        enum FinaleScreen
        {
            None,
            FirstContact,
            SecondContact,
            Plan,
            Warning,
            Processing,
            Ending,
        }

        FinaleScreen finaleScreen;
        float finaleProcessingUntil;
        EndingSceneView endingView;

        FinaleData Finale => config.database.finales.FirstOrDefault(f => f != null && session.Stage >= f.availableFrom);

        bool UpdateFinalePresentation()
        {
            if (finaleScreen == FinaleScreen.None)
                return false;
            if (finaleScreen == FinaleScreen.Processing && Time.unscaledTime >= finaleProcessingUntil)
                OpenEndingScene();
            endingView?.Tick();
            return true;
        }

        /// <summary>재실행·진행 변경 뒤 아직 확인하지 않은 연락, 최초 처리안, 저장된 엔딩을 복원한다.</summary>
        bool ShowPendingFinale()
        {
            var finale = Finale;
            // 타이틀·요청 장면이 떠 있는 동안에는 열지 않는다(닫은 뒤 ResumeAfterEntry가 다시 확인한다).
            if (finale == null || comparing || presenting || EntryActive)
                return false;
            if (progressStore.FinalProcessing != null)
            {
                OpenEndingScene();
                return true;
            }
            if (SummaryOpen)
                return finaleScreen != FinaleScreen.None;

            if (progression.StateOf(finale.firstJob) == JobState.Running
                && !store.IsViewed(RecordRef.ForRequest(finale.firstRequest).Key))
            {
                OpenFinaleContact(finale.firstRequest, FinaleScreen.FirstContact);
                return true;
            }
            if (progression.StateOf(finale.secondJob) == JobState.Running
                && !store.IsViewed(RecordRef.ForRequest(finale.secondRequest).Key))
            {
                OpenFinaleContact(finale.secondRequest, FinaleScreen.SecondContact);
                return true;
            }
            if (progression.StateOf(finale.secondJob) == JobState.Completed && !progressStore.FinalPlanIntroduced)
            {
                if (progressStore.MarkFinalPlanIntroduced())
                    OpenFinalPlan();
                return true;
            }
            return false;
        }

        void PrepareFinaleCenter(FinaleScreen screen)
        {
            CloseNotifications();
            CloseReviewPopover();
            if (selecting)
                SetSelecting(false);
            CloseSearch();
            if (WorkbenchOpen)
            {
                workbench.Close();
                workbench.KeyboardFocus = false;
                compareWarnKey = null;
            }
            summaryJob = null;
            finaleScreen = screen;
            summaryView.Open();
            SetPresentationInputBlocked(true);
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
        }

        void OpenFinaleContact(WorkRequestData request, FinaleScreen screen)
        {
            if (request == null || progressStore.FinalProcessing != null)
                return;
            PrepareFinaleCenter(screen);
            summaryView.Show(request.Title, request.device != null ? request.device.DisplayName : string.Empty,
                request.Body, string.Empty, new List<SyncSummaryView.Row>(), null, null,
                text.Get(UIKeys.FinaleContactConfirm), AcknowledgeFinaleContact);
            summaryView.ConfigureBody(150f, 210f);
            RenderTopBar();
        }

        void AcknowledgeFinaleContact()
        {
            var finale = Finale;
            var request = finaleScreen == FinaleScreen.FirstContact ? finale?.firstRequest
                : finaleScreen == FinaleScreen.SecondContact ? finale?.secondRequest : null;
            if (request == null)
                return;
            finaleScreen = FinaleScreen.None;
            summaryView.Close();
            SetPresentationInputBlocked(false);
            store.MarkViewed(RecordRef.ForRequest(request));
            AfterProgressChange();
        }

        void OpenFinalPlan()
        {
            var finale = Finale;
            if (finale == null || progressStore.FinalProcessing != null
                || progression.StateOf(finale.secondJob) != JobState.Completed)
                return;
            PrepareFinaleCenter(FinaleScreen.Plan);
            var plan = FinalProcessingCalculator.Build(finale, config.database, store, session.Stage, text);
            var rows = new List<SyncSummaryView.Row>
            {
                new SyncSummaryView.Row
                {
                    label = text.Get(UIKeys.FinalePlanDelete),
                    count = plan.CurrentDeletedCount.ToString(time.Culture),
                    height = 64f,
                },
                new SyncSummaryView.Row
                {
                    label = text.Get(UIKeys.FinalePlanKeep),
                    count = plan.CurrentKeptCount.ToString(time.Culture),
                    height = 64f,
                },
            };
            summaryView.Show(text.Get(UIKeys.FinalePlanHeading),
                text.Format(UIKeys.FinalePlanTitle,
                    ("kept", plan.CurrentKeptCount.ToString(time.Culture)),
                    ("deleted", plan.CurrentDeletedCount.ToString(time.Culture))),
                text.Format(UIKeys.FinalePlanRequests,
                    ("firstCase", finale.firstRequest.device.DisplayName),
                    ("secondCase", finale.secondRequest.device.DisplayName)),
                string.Empty, rows,
                text.Get(UIKeys.FinaleProcessCurrent), BeginFinalProcessing,
                text.Get(UIKeys.FinaleReview), CloseFinalPlan);
            summaryView.ConfigureBody(105f, 165f);
            RenderTopBar();
        }

        void CloseFinalPlan()
        {
            if (finaleScreen != FinaleScreen.Plan && finaleScreen != FinaleScreen.Warning)
                return;
            finaleScreen = FinaleScreen.None;
            summaryView.Close();
            SetPresentationInputBlocked(false);
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            RenderTopBar();
        }

        void BeginFinalProcessing()
        {
            var finale = Finale;
            if (finale == null)
                return;
            var plan = FinalProcessingCalculator.Build(finale, config.database, store, session.Stage, text);
            if (!plan.HasContractOverrides)
            {
                ExecuteFinalProcessing();
                return;
            }

            finaleScreen = FinaleScreen.Warning;
            summaryView.Show(text.Get(UIKeys.FinaleWarningHeading),
                text.Format(UIKeys.FinalePlanTitle,
                    ("kept", plan.CurrentKeptCount.ToString(time.Culture)),
                    ("deleted", plan.CurrentDeletedCount.ToString(time.Culture))),
                text.Format(UIKeys.FinaleWarningBody,
                    ("override", plan.ContractOverrideCount.ToString(time.Culture))),
                string.Empty, new List<SyncSummaryView.Row>(),
                text.Get(UIKeys.FinaleWarningConfirm), ExecuteFinalProcessing,
                text.Get(UIKeys.FinaleBack), OpenFinalPlan);
            summaryView.ConfigureBody(180f, 240f);
        }

        void ExecuteFinalProcessing()
        {
            var finale = Finale;
            if (finale == null || progressStore.FinalProcessing != null)
                return;
            var plan = FinalProcessingCalculator.Build(finale, config.database, store, session.Stage, text);
            var snapshot = FinalProcessingCalculator.Snapshot(plan, FinalProcessingChoice.CurrentDesignations);
            if (!progressStore.CompleteFinalProcessing(snapshot))
                return;
            finaleScreen = FinaleScreen.Processing;
            finaleProcessingUntil = Time.unscaledTime + 0.8f;
            summaryView.ShowProcessing(text.Get(UIKeys.FinaleProcessing), string.Empty);
            RenderTopBar();
        }

        void OpenEndingScene()
        {
            var finale = Finale;
            var snapshot = progressStore.FinalProcessing;
            if (finale == null || snapshot == null)
                return;
            if (endingView != null)
                return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                return;
            canvas = canvas.rootCanvas;
            bool playSequence = !progressStore.EndingSequenceIntroduced;
            if (playSequence && !progressStore.MarkEndingSequenceIntroduced())
                return;
            finaleScreen = FinaleScreen.Ending;
            summaryView.Close();
            CloseNotifications();
            CloseReviewPopover();
            SetPresentationInputBlocked(true);
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            bool preserved = snapshot.exceptionKeptCount > 0;
            var content = new EndingSceneContent
            {
                title = text.Get(UIKeys.EndingTitle),
                resultHeading = text.Get(UIKeys.EndingResultHeading),
                requestLabel = text.Format(UIKeys.EndingFamilyLabel, ("case", finale.secondRequest.device.DisplayName)),
                request = text.Get(UIKeys.EndingRequestA),
                counts = text.Format(UIKeys.EndingCounts,
                    ("kept", snapshot.keptCount.ToString(time.Culture)),
                    ("deleted", snapshot.deletedCount.ToString(time.Culture))),
                deletedResult = text.Get(UIKeys.EndingDeletedResult),
                deletedUnavailable = text.Get(UIKeys.EndingDeletedUnavailable),
                deletedFinal = text.Get(UIKeys.EndingDeletedFinal),
                preservedResult = text.Get(UIKeys.EndingPreservedResult),
                later = text.Get(UIKeys.EndingLater),
                auditHeading = text.Get(UIKeys.EndingAuditHeading),
                auditBody = text.Get(UIKeys.EndingAuditBody),
                preservedFinal = text.Get(UIKeys.EndingPreservedFinal),
                complete = text.Get(UIKeys.EndingComplete),
            };
            endingView = EndingSceneView.Create(canvas, Theme, preserved, playSequence, content);
            RenderTopBar();
        }

        /// <summary>기존 요약 닫기 경로가 P6 화면을 일반 연동 요약으로 처리하지 않게 먼저 소비한다.</summary>
        bool CloseFinaleScreen()
        {
            switch (finaleScreen)
            {
                case FinaleScreen.FirstContact:
                case FinaleScreen.SecondContact:
                    AcknowledgeFinaleContact();
                    return true;
                case FinaleScreen.Plan:
                case FinaleScreen.Warning:
                    CloseFinalPlan();
                    return true;
                case FinaleScreen.Processing:
                case FinaleScreen.Ending:
                    return true;
                default:
                    return false;
            }
        }

        bool TryFinaleTopStatus(out TopBarView.Status status)
        {
            status = default;
            var finale = Finale;
            if (finale == null)
                return false;
            if (progressStore.FinalProcessing != null)
            {
                status = new TopBarView.Status { text = text.Get(UIKeys.FinaleTopComplete), style = TopBarView.StatusStyle.Plain, progress = 1f };
                return true;
            }
            if (progression.StateOf(finale.firstJob) == JobState.Running)
            {
                statusAction = () => OpenFinaleContact(finale.firstRequest, FinaleScreen.FirstContact);
                status = Link(text.Get(UIKeys.FinaleTopFirst), 0f);
                return true;
            }
            if (progression.StateOf(finale.secondJob) == JobState.Running)
            {
                statusAction = () => OpenFinaleContact(finale.secondRequest, FinaleScreen.SecondContact);
                status = Link(text.Get(UIKeys.FinaleTopSecond), 0f);
                return true;
            }
            if (progression.StateOf(finale.secondJob) == JobState.Completed)
            {
                statusAction = OpenFinalPlan;
                status = Link(text.Get(UIKeys.FinaleTopPlan), 1f);
                return true;
            }
            return false;
        }

        string FinaleShortStatus(CaseData device)
        {
            var finale = Finale;
            if (finale == null || device == null)
                return null;
            return text.Get(progressStore.FinalProcessing != null ? UIKeys.FinaleShortComplete : UIKeys.FinaleShortPending);
        }
    }
}
