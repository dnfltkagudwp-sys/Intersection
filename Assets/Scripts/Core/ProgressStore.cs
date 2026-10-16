using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Intersection.Data;
using UnityEngine;

namespace Intersection.Core
{
    public enum JobState
    {
        NotStarted,
        Running,
        Completed,
    }

    /// <summary>
    /// 진행 이벤트가 실제로 일어났을 때 한 번 만들어진 업무 알림.
    /// 문구는 저장하지 않고 템플릿 키와 토큰 값만 저장해, 표시할 때 현재 문자열·의뢰 표시명으로 만든다.
    /// </summary>
    [Serializable]
    public class NotificationEntry
    {
        /// <summary>알림을 만든 진행 효과의 불변 ID. 같은 효과는 다시 알림을 만들지 않는다.</summary>
        public string effectId;
        public string templateKey;
        public string caseId;
        public int count;
        public bool read;
        public long seq;
    }

    /// <summary>
    /// 작업 완료로 한 번 만들어진 연동 결과 요약. 요약 내용(수량·분류)은 저장하지 않고 표시할 때 현재 데이터에서 계산하며,
    /// 만들어졌다는 사실과 플레이어가 확인(닫음)했는지만 저장한다. 확인 전이면 다음 실행 때 다시 자동으로 보인다.
    /// </summary>
    [Serializable]
    public class SummaryEntry
    {
        /// <summary>요약을 만든 진행 효과의 불변 ID.</summary>
        public string effectId;
        /// <summary>요약 대상 작업(완료된 작업)의 불변 ID.</summary>
        public string jobId;
        public string caseId;
        public bool seen;
        public long seq;
    }

    /// <summary>에디터 검수용 실패 주입 지점 (ProgressStore.Fault).</summary>
    public static class ProgressFaultPoint
    {
        /// <summary>작업 상태를 바꾼 직후 (연쇄 완료되는 작업 포함, 인자는 작업 ID).</summary>
        public const string JobState = "job-state";
        /// <summary>접근 단계를 바꾼 직후.</summary>
        public const string Stage = "stage";
        /// <summary>CompleteJob 효과로 다른 작업을 완료한 직후 (인자는 완료된 작업 ID).</summary>
        public const string ChainComplete = "chain-complete";
        /// <summary>알림을 만들기 직전·직후 (인자는 효과 ID).</summary>
        public const string NotifyBefore = "notify-before";
        public const string NotifyAfter = "notify-after";
        /// <summary>연동 결과 요약을 만든 직후 (인자는 효과 ID).</summary>
        public const string Summary = "summary";
        /// <summary>임시 파일을 다 쓴 뒤 저장 파일과 교체하기 직전.</summary>
        public const string Replace = "replace";
    }

    [Serializable]
    class JobStateEntry
    {
        public string jobId;
        public string state;
    }

    [Serializable]
    class ProgressSaveData
    {
        public int version = 1;
        public string stage;
        public long nextSeq = 1;
        public List<JobStateEntry> jobs = new List<JobStateEntry>();
        public List<string> firedEffects = new List<string>();
        public List<NotificationEntry> notifications = new List<NotificationEntry>();
        /// <summary>예전 저장 파일에는 없다(빈 목록으로 읽음, 형식 version 그대로).</summary>
        public List<SummaryEntry> summaries = new List<SummaryEntry>();
        /// <summary>P6 최종 처리안이 최초 자동 표시됐는지. 예전 저장에는 없어 false로 읽힌다.</summary>
        public bool finalPlanIntroduced;
        /// <summary>P6 실행 전에 원자적으로 저장하는 불변 처리 결과. null이면 아직 실행 전이다.</summary>
        public FinalProcessingSnapshot finalProcessing;
        /// <summary>
        /// JsonUtility는 null인 중첩 객체도 빈 객체로 기록할 수 있으므로, 실제 처리 완료 여부를 별도 값으로 보관한다.
        /// 예전 저장에는 없어 false로 읽히며, 유효한 구형 스냅샷은 로드할 때 내용으로 판별한다.
        /// </summary>
        public bool finalProcessingCompleted;
        /// <summary>새 엔딩 연출을 한 번 시작했는지. 시작 뒤 재실행하면 마지막 장면만 복원한다.</summary>
        public bool endingSequenceIntroduced;
    }

    /// <summary>
    /// 진행 상태 저장소 (접근 단계·의뢰별 작업 상태·업무 알림). 플레이어 업무 상태(WorkStore)와 별도 파일에 저장한다.
    /// 작업·알림은 불변 ID로만 참조하며, 원본에서 사라진 작업 ID도 버리지 않고 보관한다.
    /// 파일이 없으면 시작 단계부터, 읽을 수 없으면 옆에 보관한 뒤 시작 단계부터 시작한다.
    /// 작업 시작·완료처럼 여러 값을 함께 바꾸는 변경은 Transact로 묶어 전부 성공했을 때만 한 번 저장하고,
    /// 도중에 실패하면 메모리를 이전 상태로 되돌린다(기존 저장 파일은 건드리지 않음).
    /// </summary>
    public class ProgressStore
    {
        readonly string path;
        readonly Dictionary<string, JobState> jobs = new Dictionary<string, JobState>();
        readonly HashSet<string> fired = new HashSet<string>();
        readonly List<NotificationEntry> notifications = new List<NotificationEntry>();
        readonly List<SummaryEntry> summaries = new List<SummaryEntry>();
        bool finalPlanIntroduced;
        FinalProcessingSnapshot finalProcessing;
        bool endingSequenceIntroduced;
        long nextSeq = 1;
        int transactionDepth;
        bool dirty;

#if UNITY_EDITOR
        /// <summary>에디터 검수용 실패 주입: (지점, 인자)를 받아 예외를 던지면 그 지점에서 실패한다. 저장되지 않는 정적 값.</summary>
        public static Action<string, string> DebugFault;
#endif

        /// <summary>실패 주입 지점. 정식 빌드에서는 호출 자체가 사라진다.</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void Fault(string point, string arg = null)
        {
#if UNITY_EDITOR
            DebugFault?.Invoke(point, arg);
#endif
        }

        public ProgressStore(string path, ProgressStage startStage)
        {
            this.path = path;
            Stage = startStage;
            Load(startStage);
        }

        public string FilePath => path;
        public ProgressStage Stage { get; private set; }

        public IEnumerable<string> JobIds => jobs.Keys;
        public IReadOnlyList<NotificationEntry> Notifications => notifications;
        public int UnreadCount => notifications.Count(n => !n.read);
        public IReadOnlyList<SummaryEntry> Summaries => summaries;
        public bool FinalPlanIntroduced => finalPlanIntroduced;
        public FinalProcessingSnapshot FinalProcessing => finalProcessing;
        public bool EndingSequenceIntroduced => endingSequenceIntroduced;

        public JobState StateOf(string jobId) => jobId != null && jobs.TryGetValue(jobId, out var s) ? s : JobState.NotStarted;

        public void SetStage(ProgressStage stage)
        {
            if (Stage == stage)
                return;
            Stage = stage;
            Save();
        }

        public void SetJob(string jobId, JobState state)
        {
            if (string.IsNullOrEmpty(jobId) || StateOf(jobId) == state)
                return;
            if (state == JobState.NotStarted)
                jobs.Remove(jobId);
            else
                jobs[jobId] = state;
            Save();
        }

        public bool HasFired(string effectId) => effectId != null && fired.Contains(effectId);

        /// <summary>진행 효과가 처음 실행될 때만 알림을 만든다. 이미 실행된 효과면 false.</summary>
        public bool AddNotification(string effectId, string templateKey, string caseId, int count)
        {
            if (string.IsNullOrEmpty(effectId) || !fired.Add(effectId))
                return false;
            notifications.Add(new NotificationEntry
            {
                effectId = effectId,
                templateKey = templateKey,
                caseId = caseId,
                count = count,
                seq = nextSeq++,
            });
            Save();
            return true;
        }

        /// <summary>진행 효과가 처음 실행될 때만 연동 결과 요약을 만든다. 이미 실행된 효과면 false.</summary>
        public bool AddSummary(string effectId, string jobId, string caseId)
        {
            if (string.IsNullOrEmpty(effectId) || !fired.Add(effectId))
                return false;
            summaries.Add(new SummaryEntry { effectId = effectId, jobId = jobId, caseId = caseId, seq = nextSeq++ });
            Save();
            return true;
        }

        /// <summary>플레이어가 요약을 확인(닫음)했다. 다음 실행부터 자동으로 보이지 않는다.</summary>
        public void MarkSummarySeen(SummaryEntry entry)
        {
            if (entry == null || entry.seen)
                return;
            entry.seen = true;
            Save();
        }

        public void MarkRead(NotificationEntry entry)
        {
            if (entry == null || entry.read)
                return;
            entry.read = true;
            Save();
        }

        public void MarkAllRead()
        {
            if (notifications.All(n => n.read))
                return;
            foreach (var n in notifications)
                n.read = true;
            Save();
        }

        /// <summary>최종 처리안의 최초 자동 표시를 기록한다. 이후에는 상단 버튼으로 다시 연다.</summary>
        public bool MarkFinalPlanIntroduced()
        {
            if (finalPlanIntroduced)
                return true;
            return Transact(() =>
            {
                finalPlanIntroduced = true;
                Save();
            });
        }

        /// <summary>최종 선택과 모든 처리 단위 결과를 한 번만 저장한다. 저장 실패 시 메모리도 이전 상태로 복원한다.</summary>
        public bool CompleteFinalProcessing(FinalProcessingSnapshot snapshot)
        {
            if (!ValidFinalProcessingSnapshot(snapshot) || finalProcessing != null)
                return false;
            return Transact(() =>
            {
                finalProcessing = Clone(snapshot);
                Save();
            });
        }

        /// <summary>엔딩 연출은 한 번만 재생하고, 이후 실행에서는 해당 엔딩의 마지막 화면만 복원한다.</summary>
        public bool MarkEndingSequenceIntroduced()
        {
            if (endingSequenceIntroduced)
                return true;
            return Transact(() =>
            {
                endingSequenceIntroduced = true;
                Save();
            });
        }

        /// <summary>에디터 검수용: 진행 상태를 모두 지운다 (파일은 다음 저장 때 다시 만든다).</summary>
        public void ResetAll(ProgressStage startStage)
        {
            Stage = startStage;
            jobs.Clear();
            fired.Clear();
            notifications.Clear();
            summaries.Clear();
            finalPlanIntroduced = false;
            finalProcessing = null;
            endingSequenceIntroduced = false;
            nextSeq = 1;
            Save();
        }

        class Snapshot
        {
            public ProgressStage stage;
            public long nextSeq;
            public Dictionary<string, JobState> jobs;
            public HashSet<string> fired;
            public List<NotificationEntry> notifications;
            public List<SummaryEntry> summaries;
            public bool finalPlanIntroduced;
            public FinalProcessingSnapshot finalProcessing;
            public bool endingSequenceIntroduced;
        }

        Snapshot Capture() => new Snapshot
        {
            stage = Stage,
            nextSeq = nextSeq,
            jobs = new Dictionary<string, JobState>(jobs),
            fired = new HashSet<string>(fired),
            notifications = notifications.Select(n => new NotificationEntry
            {
                effectId = n.effectId, templateKey = n.templateKey, caseId = n.caseId, count = n.count, read = n.read, seq = n.seq,
            }).ToList(),
            summaries = summaries.Select(s => new SummaryEntry
            {
                effectId = s.effectId, jobId = s.jobId, caseId = s.caseId, seen = s.seen, seq = s.seq,
            }).ToList(),
            finalPlanIntroduced = finalPlanIntroduced,
            finalProcessing = Clone(finalProcessing),
            endingSequenceIntroduced = endingSequenceIntroduced,
        };

        void Restore(Snapshot s)
        {
            Stage = s.stage;
            nextSeq = s.nextSeq;
            jobs.Clear();
            foreach (var j in s.jobs)
                jobs[j.Key] = j.Value;
            fired.Clear();
            fired.UnionWith(s.fired);
            notifications.Clear();
            notifications.AddRange(s.notifications);
            summaries.Clear();
            summaries.AddRange(s.summaries);
            finalPlanIntroduced = s.finalPlanIntroduced;
            finalProcessing = Clone(s.finalProcessing);
            endingSequenceIntroduced = s.endingSequenceIntroduced;
        }

        /// <summary>
        /// 한 번의 작업 시작·완료로 생기는 모든 변경(작업 상태·접근 단계·연쇄 완료·알림·실행된 효과)을 하나로 처리한다.
        /// 안쪽 변경은 메모리에만 반영하고, 전부 끝나면 디스크에 한 번 저장한다.
        /// 도중에 예외가 나거나 저장에 실패하면 메모리를 시작 전 상태로 되돌리고 false를 돌려준다(같은 작업을 다시 실행할 수 있다).
        /// 이미 열린 트랜잭션 안에서 부르면 바깥 트랜잭션에 합쳐진다.
        /// </summary>
        public bool Transact(Action body)
        {
            if (transactionDepth > 0)
            {
                body();
                return true;
            }
            var before = Capture();
            transactionDepth = 1;
            dirty = false;
            try
            {
                body();
            }
            catch (Exception ex)
            {
                transactionDepth = 0;
                dirty = false;
                Restore(before);
                Debug.LogError($"[ProgressStore] 진행 변경을 적용하지 못해 이전 상태로 되돌렸습니다.\n{ex.Message}");
                return false;
            }
            transactionDepth = 0;
            if (!dirty)
                return true;
            dirty = false;
            if (Write())
                return true;
            Restore(before);
            return false;
        }

        /// <summary>현재 상태를 저장한다. 트랜잭션 안이면 끝날 때 한 번에 저장한다. 실패하면 false.</summary>
        public bool Save()
        {
            if (transactionDepth > 0)
            {
                dirty = true;
                return true;
            }
            return Write();
        }

        /// <summary>
        /// 임시 파일에 다 쓰고 디스크에 내린 뒤 기존 파일과 원자적으로 교체한다(File.Replace).
        /// 기존 파일을 먼저 지우지 않으므로 어느 단계에서 실패해도 기존의 유효한 저장 파일이 남는다.
        /// </summary>
        bool Write()
        {
            var data = new ProgressSaveData
            {
                stage = Stage.ToString(),
                nextSeq = nextSeq,
                jobs = jobs.OrderBy(j => j.Key).Select(j => new JobStateEntry { jobId = j.Key, state = j.Value.ToString() }).ToList(),
                firedEffects = fired.OrderBy(f => f).ToList(),
                notifications = notifications.ToList(),
                summaries = summaries.ToList(),
                finalPlanIntroduced = finalPlanIntroduced,
                finalProcessing = Clone(finalProcessing),
                finalProcessingCompleted = finalProcessing != null,
                endingSequenceIntroduced = endingSequenceIntroduced,
            };
            string tmp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(data, true));
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush(true);
                }
                Fault(ProgressFaultPoint.Replace);
                if (File.Exists(path))
                    File.Replace(tmp, path, null, true);
                else
                    File.Move(tmp, path);
                return true;
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* 남은 임시 파일은 다음 저장 때 덮어쓴다 */ }
                Debug.LogError($"[ProgressStore] 진행 상태를 저장하지 못했습니다. 기존 저장 파일은 그대로 둡니다: {path}\n{ex.Message}");
                return false;
            }
        }

        void Load(ProgressStage startStage)
        {
            if (!File.Exists(path))
                return;
            try
            {
                var data = JsonUtility.FromJson<ProgressSaveData>(File.ReadAllText(path));
                if (data == null)
                    throw new InvalidDataException("비어 있는 진행 저장 파일");
                if (!Enum.TryParse(data.stage, out ProgressStage stage) || !Enum.IsDefined(typeof(ProgressStage), stage))
                    throw new InvalidDataException($"알 수 없는 접근 단계 '{data.stage}'");
                Stage = stage;
                nextSeq = Math.Max(1, data.nextSeq);
                foreach (var j in data.jobs ?? new List<JobStateEntry>())
                {
                    if (string.IsNullOrEmpty(j?.jobId) || !Enum.TryParse(j.state, out JobState s) || s == JobState.NotStarted)
                        continue;
                    jobs[j.jobId] = s;
                }
                foreach (var f in data.firedEffects ?? new List<string>())
                {
                    if (!string.IsNullOrEmpty(f))
                        fired.Add(f);
                }
                foreach (var n in data.notifications ?? new List<NotificationEntry>())
                {
                    if (n == null || string.IsNullOrEmpty(n.effectId))
                        continue;
                    fired.Add(n.effectId);
                    notifications.Add(n);
                }
                foreach (var s in data.summaries ?? new List<SummaryEntry>())
                {
                    if (s == null || string.IsNullOrEmpty(s.effectId) || summaries.Any(x => x.effectId == s.effectId))
                        continue;
                    fired.Add(s.effectId);
                    summaries.Add(s);
                }
                finalPlanIntroduced = data.finalPlanIntroduced;
                // JsonUtility가 null을 빈 객체로 직렬화한 예전 저장은 처리 완료로 보지 않는다.
                // 완료 플래그가 도입되기 전에 실제로 만든 유효한 스냅샷은 내용 검증으로 계속 복원한다.
                bool validFinalProcessing = ValidFinalProcessingSnapshot(data.finalProcessing);
                finalProcessing = (data.finalProcessingCompleted || validFinalProcessing) && validFinalProcessing
                    ? Clone(data.finalProcessing) : null;
                endingSequenceIntroduced = finalProcessing != null && data.endingSequenceIntroduced;
                nextSeq = Math.Max(nextSeq, notifications.Count == 0 ? 1 : notifications.Max(n => n.seq) + 1);
                nextSeq = Math.Max(nextSeq, summaries.Count == 0 ? 1 : summaries.Max(s => s.seq) + 1);
            }
            catch (Exception ex)
            {
                // 손상된 진행 파일은 덮어쓰지 않고 옆에 보관한 뒤 시작 단계부터 시작한다.
                string backup = path + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                try { File.Copy(path, backup, true); } catch { /* 보관 실패는 무시 */ }
                Stage = startStage;
                jobs.Clear();
                fired.Clear();
                notifications.Clear();
                summaries.Clear();
                finalPlanIntroduced = false;
                finalProcessing = null;
                endingSequenceIntroduced = false;
                nextSeq = 1;
                Debug.LogError($"[ProgressStore] 진행 저장 파일을 읽지 못해 시작 단계부터 시작합니다. 원본은 {backup}에 보관했습니다.\n{ex.Message}");
            }
        }

        static FinalProcessingSnapshot Clone(FinalProcessingSnapshot source)
        {
            if (source == null)
                return null;
            return new FinalProcessingSnapshot
            {
                choice = source.choice,
                keptCount = source.keptCount,
                deletedCount = source.deletedCount,
                contractKeptCount = source.contractKeptCount,
                exceptionKeptCount = source.exceptionKeptCount,
                units = (source.units ?? new List<FinalProcessingUnitSnapshot>()).Select(u => new FinalProcessingUnitSnapshot
                {
                    recordKey = u.recordKey,
                    groupId = u.groupId,
                    itemCount = u.itemCount,
                    kept = u.kept,
                    exception = u.exception,
                }).ToList(),
            };
        }

        static bool ValidFinalProcessingSnapshot(FinalProcessingSnapshot source)
        {
            if (source == null || !Enum.TryParse(source.choice, out FinalProcessingChoice choice)
                || !Enum.IsDefined(typeof(FinalProcessingChoice), choice)
                || source.units == null || source.units.Count == 0
                || source.units.Any(u => u == null || string.IsNullOrEmpty(u.recordKey)
                                          || string.IsNullOrEmpty(u.groupId) || u.itemCount <= 0
                                          || (u.exception && !u.kept))
                || source.units.GroupBy(u => u.recordKey).Any(g => g.Count() > 1))
                return false;

            long kept = source.units.Where(u => u.kept).Sum(u => (long)u.itemCount);
            long deleted = source.units.Where(u => !u.kept).Sum(u => (long)u.itemCount);
            long exceptions = source.units.Where(u => u.exception).Sum(u => (long)u.itemCount);
            long total = kept + deleted;
            return total > 0 && kept == source.keptCount && deleted == source.deletedCount
                   && source.contractKeptCount >= 0 && source.contractKeptCount <= total
                   && exceptions == source.exceptionKeptCount
                   && (choice != FinalProcessingChoice.Contract || source.exceptionKeptCount == 0);
        }
    }
}
