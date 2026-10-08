using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// </summary>
    public class ProgressStore
    {
        readonly string path;
        readonly Dictionary<string, JobState> jobs = new Dictionary<string, JobState>();
        readonly HashSet<string> fired = new HashSet<string>();
        readonly List<NotificationEntry> notifications = new List<NotificationEntry>();
        long nextSeq = 1;

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

        public void Save()
        {
            var data = new ProgressSaveData
            {
                stage = Stage.ToString(),
                nextSeq = nextSeq,
                jobs = jobs.OrderBy(j => j.Key).Select(j => new JobStateEntry { jobId = j.Key, state = j.Value.ToString() }).ToList(),
                firedEffects = fired.OrderBy(f => f).ToList(),
                notifications = notifications.ToList(),
            };
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProgressStore] 진행 상태를 저장하지 못했습니다: {path}\n{ex.Message}");
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
