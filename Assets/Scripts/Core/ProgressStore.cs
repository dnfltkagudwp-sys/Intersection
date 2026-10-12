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

        /// <summary>에디터 검수용: 진행 상태를 모두 지운다 (파일은 다음 저장 때 다시 만든다).</summary>
        public void ResetAll(ProgressStage startStage)
        {
            Stage = startStage;
            jobs.Clear();
            fired.Clear();
            notifications.Clear();
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
                nextSeq = Math.Max(nextSeq, notifications.Count == 0 ? 1 : notifications.Max(n => n.seq) + 1);
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
                nextSeq = 1;
                Debug.LogError($"[ProgressStore] 진행 저장 파일을 읽지 못해 시작 단계부터 시작합니다. 원본은 {backup}에 보관했습니다.\n{ex.Message}");
            }
        }
    }
}
