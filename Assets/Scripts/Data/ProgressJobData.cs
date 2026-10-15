using System;
using System.Collections.Generic;
using UnityEngine;

namespace Intersection.Data
{
    /// <summary>시작 조건 종류. 에셋에 정수로 저장되므로 값의 순서를 바꾸지 않고 새 종류는 끝에 붙인다.</summary>
    public enum ProgressConditionKind
    {
        [Tooltip("더 이상 쓰지 않음 (예전 단계형 튜토리얼 완료). 검증 도구가 오류로 잡는다.")]
        Retired,
        [Tooltip("request를 업무 패널에서 열람함 (좌측 의뢰 요청·작업 기록·검색 어디서 열었든)")]
        RequestViewed,
        [Tooltip("이 작업 의뢰의 원본 기록 중 source 출처 기록을 minCount건 이상 보존 또는 삭제 후보로 분류함 (현재 접근 가능한 기록만 셈)")]
        ClassifiedRecords,
        [Tooltip("job 작업이 완료됨")]
        JobCompleted,
        [Tooltip("record 기록의 지금 분류가 보존 (분류 횟수나 과거 이력이 아니라 현재 상태)")]
        RecordKept,
        [Tooltip("record 원본을 실제로 열람함 (사진은 한 장 보기, 대화방은 device 기기(비우면 작업 의뢰 기기)에서 대화방 열기, 그 외 기록은 상세). 검색 결과·고정·비교만으로는 인정하지 않음")]
        RecordViewed,
        [Tooltip("batch 검토 묶음의 모든 대상 원본을 이 작업 의뢰에서 실제로 열람함 (RecordViewed와 같은 열람 기준, 상단 진행률과 같은 대상 목록)")]
        ReviewBatchViewed,
        [Tooltip("group 열람 묶음의 대상 중 서로 다른 원본을 묶음의 minCount건 이상 실제로 열람함 (RecordViewed와 같은 열람 기준, 같은 원본은 한 건)")]
        RecordGroupViewed,
    }

    [Serializable]
    public class ProgressCondition
    {
        public ProgressConditionKind kind;
        public WorkRequestData request;
        public ProgressJobData job;
        [Tooltip("RecordKept: 대상 기록 (사진·기록 에셋) / RecordViewed: 대상 원본 (사진·기록·대화방 에셋, 대화방은 device 기기의 대화방으로 판정)")]
        public ContentAsset record;
        [Tooltip("RecordViewed: 대화방을 판정할 기기. 비우면 작업 의뢰 기기 (사진·기록은 원본의 기기를 쓰며, 지정하면 그 기기와 같아야 한다)")]
        public CaseData device;
        [Tooltip("ReviewBatchViewed: 검토 묶음")]
        public ReviewBatchData batch;
        [Tooltip("RecordGroupViewed: 열람 묶음 (대상 목록과 최소 열람 수는 묶음에 둔다)")]
        public ProgressRecordGroupData group;
        [Min(1)] public int minCount = 1;
        public RecordSource source = RecordSource.Local;
    }

    public enum ProgressEffectKind
    {
        [Tooltip("접근 단계를 stage까지 올린다 (내리지 않는다)")]
        RaiseStage,
        [Tooltip("job 작업을 완료한다 (그 작업의 완료 효과도 실행된다)")]
        CompleteJob,
        [Tooltip("업무 알림을 한 번 만든다. 문구는 templateKey 문자열의 {caseDisplayName}·{count} 토큰을 채워 표시한다.")]
        Notify,
        [Tooltip("이 작업이 연 출처(unlocksSources)의 자료를 묶은 연동 결과 요약을 한 번 만든다. 처음 한 번 자동으로 보이고, 상단 완료 상태에서 다시 열 수 있다. 제목은 templateKey({caseDisplayName}), 의뢰는 caseToken.")]
        SyncSummary,
    }

    [Serializable]
    public class ProgressEffect
    {
        [SerializeField, Tooltip("불변 효과 ID. 자동 발급되며 손으로 수정하지 않는다. 알림은 이 ID로 한 번만 만든다.")]
        string id;

        public ProgressEffectKind kind;
        public ProgressStage stage;
        public ProgressJobData job;

        [Tooltip("알림 문구 문자열 키")]
        public string templateKey;

        [Tooltip("{caseDisplayName}·{count} 토큰에 쓰는 의뢰")]
        public CaseData caseToken;

        [Header("요약 문구 (SyncSummary 전용, 선택 — 비우면 기본 연동 결과 요약 문구)")]
        [Tooltip("요약 머리 문구 키. 비우면 summary.heading")]
        public string summaryHeadingKey;
        [Tooltip("요약 전체 수량 문구 키({count}, 단수형은 키.one). 비우면 summary.count")]
        public string summaryCountKey;
        [Tooltip("첫 분류를 여는 버튼 문구 키. 비우면 summary.open")]
        public string summaryOpenKey;

        public string Id => id;

        public bool EnsureId()
        {
            if (!string.IsNullOrEmpty(id))
                return false;
            id = ContentAsset.NewId();
            return true;
        }

        public void RegenerateId() => id = ContentAsset.NewId();
    }

    /// <summary>
    /// 의뢰별 진행 작업 (클라우드·연동 인벤토리 생성, 삭제 데이터 복구 등). 플레이어가 상단 업무 상태 컨트롤에서 직접 시작한다.
    /// 시작 조건·시작/완료 효과·상태 문구를 모두 데이터로 둬, 진행 기준이 바뀌어도 화면 코드나 콘텐츠를 다시 만들지 않는다.
    /// 코드는 특정 의뢰를 알지 못한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Intersection/Progress Job", fileName = "Job")]
    public class ProgressJobData : ContentAsset
    {
        [Tooltip("작업이 속한 의뢰")]
        public CaseData device;

        [Tooltip("같은 의뢰 안 작업 순서")]
        public int order;

        [Tooltip("이 작업의 의뢰 요청 (요청 미열람이면 상단이 `유족 요청 확인 필요`로 이 요청을 연다)")]
        public WorkRequestData request;

        [Tooltip("이 작업이 끝나야 접근할 수 있게 되는 출처 (검증 도구가 기록의 접근 단계와 대조한다)")]
        public List<RecordSource> unlocksSources = new List<RecordSource>();

        [Header("문구 (문자열 키)")]
        [Tooltip("상단 시작 동작 문구 (조건을 모두 만족했을 때만 보인다)")]
        public string actionKey;
        [Tooltip("조건이 아직 안 됐을 때의 업무 상태 (다음에 할 일을 지시하지 않는 문구)")]
        public string notReadyKey;
        [Tooltip("진행 중일 때 상단·패널 상태")]
        public string runningKey;
        [Tooltip("완료 뒤 상단·패널 상태")]
        public string doneKey;
        [Tooltip("진행 중일 때 좌측 의뢰 행의 짧은 상태")]
        public string shortRunningKey;
        [Tooltip("완료 뒤 좌측 의뢰 행의 짧은 상태")]
        public string shortDoneKey;

        [Header("진행")]
        public List<ProgressCondition> startConditions = new List<ProgressCondition>();

        [Tooltip("시작 조건을 처음 모두 만족했을 때(아직 시작하지 않은 작업) 한 번 실행하는 효과. 업무 알림(Notify)만 쓸 수 있다 — " +
                 "작업 시작·단계 변경은 플레이어가 상단에서 직접 한다. 효과 ID로 한 번만 실행되며, 이미 시작·완료한 작업에는 실행하지 않는다.")]
        public List<ProgressEffect> onReady = new List<ProgressEffect>();

        public List<ProgressEffect> onStart = new List<ProgressEffect>();
        public List<ProgressEffect> onComplete = new List<ProgressEffect>();

        [Header("완료")]
        [Tooltip("진행 중인 이 작업이 완료될 수 있는 조건 (모두 만족). 시작 조건과 별개이며 열람 기준은 시작 조건과 같다. 비어 있으면 조건으로 완료되지 않는다.")]
        public List<ProgressCondition> completionConditions = new List<ProgressCondition>();

        [Tooltip("완료 조건을 모두 만족하면 자동으로 완료한다 (완료 효과 한 번 실행). 끄면 조건 충족 여부는 에디터 진행 검수 창에서만 보이고 완료되지 않는다. 다음 단계의 정식 자료가 없으면 검증 도구가 오류로 막는다.")]
        public bool autoCompleteWhenReady;

        [Tooltip("다른 작업의 효과가 아니라 이후 정식 콘텐츠(또는 에디터 검수)가 완료시키는 작업. 검증 도구의 도달 가능성 검사에서 완료 경로로 인정한다.")]
        public bool completedExternally;

        [Tooltip("켜면 시작 조건을 모두 만족했을 때 이 작업의 시작 버튼을 다른 의뢰 화면 상단에도 보여준다(같은 작업·같은 상태를 공유, 복제하지 않음). " +
                 "조건 충족 전·시작 뒤에는 다른 의뢰 화면에 아무것도 더하지 않는다. 끄면(기본) 작업의 의뢰 화면에만 보인다.")]
        public bool showOnAllCases;

        [Header("시작 연출")]
        [Tooltip("선택. 플레이어가 상단에서 이 작업을 시작했을 때 잠깐 보여줄 처리 문구 키. 비우면 연출 없음(기존 동작). 진행 데이터(작업 상태·단계·알림·요약)는 연출과 무관하게 누르는 즉시 저장된다.")]
        public string startPresentationKey;

        [Tooltip("처리 연출 길이(초). 클릭·Enter로 바로 넘길 수 있고, 연출 중 종료하면 다음 실행에서 반복하지 않는다.")]
        [Min(0f)] public float startPresentationSeconds = 1f;

        [TextArea(1, 4), Tooltip("작가용 메모. 화면에 표시하지 않는다.")]
        public string authorNote;

        public IEnumerable<ProgressEffect> AllEffects
        {
            get
            {
                foreach (var e in onReady)
                    yield return e;
                foreach (var e in onStart)
                    yield return e;
                foreach (var e in onComplete)
                    yield return e;
            }
        }

        public override bool EnsureIds()
        {
            bool changed = base.EnsureIds();
            foreach (var e in AllEffects)
                changed |= e != null && e.EnsureId();
            return changed;
        }

        public override void RegenerateIds()
        {
            base.RegenerateIds();
            foreach (var e in AllEffects)
                e?.RegenerateId();
        }
    }
}
