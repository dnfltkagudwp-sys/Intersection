using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Intersection.Core
{
    public enum Classification
    {
        Unclassified,
        Keep,
        // Reserved for old save compatibility. Loaded delete candidates now follow the contract default.
        Delete,
    }

    /// <summary>
    /// 한 기록에 대한 플레이어 업무 상태. 열람·핀·분류·메모는 서로 독립이며 하나의 selected 값으로 합치지 않는다.
    /// 보존 지정은 기본 처리에 대한 별도 지정이다. 과거 삭제 후보는 불러올 때 별도 지정 없음으로 읽는다.
    /// </summary>
    [Serializable]
    public class WorkEntry : ISerializationCallbackReceiver
    {
        public RecordRef target;
        public bool viewed;
        public bool pinned;
        public long pinOrder;
        [NonSerialized] public Classification classification;
        [SerializeField] string classificationName = nameof(Classification.Unclassified);
        public string memo = string.Empty;

        public void OnBeforeSerialize() => classificationName = classification.ToString();

        public void OnAfterDeserialize()
        {
            if (!Enum.TryParse(classificationName, out classification) || classification == Classification.Delete)
                classification = Classification.Unclassified;
        }

        public bool IsEmpty =>
            !viewed && !pinned && classification == Classification.Unclassified && string.IsNullOrEmpty(memo);
    }

    /// <summary>
    /// 비교 화면에 두 기록을 실제로 함께 표시했다는 사실. 일치·관련 여부 같은 판정은 담지 않는다.
    /// keyA/keyB는 불변 RecordRef.Key, a/b는 같은 참조 전체(검증·해석용).
    /// </summary>
    [Serializable]
    public class ComparisonEntry
    {
        public string keyA;
        public string keyB;
        public RecordRef a;
        public RecordRef b;

        public bool IsPair(string x, string y) => (keyA == x && keyB == y) || (keyA == y && keyB == x);
    }

    [Serializable]
    class WorkSaveData
    {
        public int version = 1;
        public long nextPinOrder = 1;
        public List<WorkEntry> entries = new List<WorkEntry>();
        public List<string> completedTutorialSteps = new List<string>();
        public List<ComparisonEntry> comparisons = new List<ComparisonEntry>();
    }

    /// <summary>
    /// 업무 상태 저장소. 변경할 때마다 JSON 파일로 저장하고, 시작할 때 복원한다.
    /// 저장 키는 RecordRef.Key(불변 ID 조합)이며, 원본이 사라진 항목도 지우지 않고 그대로 보관한다.
    /// </summary>
    public class WorkStore
    {
        readonly string path;
        readonly Dictionary<string, WorkEntry> entries = new Dictionary<string, WorkEntry>();
        /// <summary>예전 단계형 튜토리얼의 완료 단계 ID. 판정에는 쓰지 않고, 기존 저장 파일 내용을 잃지 않도록 그대로 읽고 다시 쓴다.</summary>
        readonly HashSet<string> completedSteps = new HashSet<string>();
        readonly List<ComparisonEntry> comparisons = new List<ComparisonEntry>();
        long nextPinOrder = 1;

        public event Action Changed;

        public string FilePath => path;

        public WorkStore(string path)
        {
            this.path = path;
            Load();
        }

        public WorkEntry Get(string key) => key != null && entries.TryGetValue(key, out var e) ? e : null;

        public bool IsPinned(string key) => Get(key)?.pinned ?? false;
        public bool IsViewed(string key) => Get(key)?.viewed ?? false;
        public Classification ClassOf(string key) => Get(key)?.classification ?? Classification.Unclassified;
        public string MemoOf(string key) => Get(key)?.memo ?? string.Empty;

        public IEnumerable<WorkEntry> All => entries.Values;

        public List<WorkEntry> Pinned() => entries.Values.Where(e => e.pinned).OrderBy(e => e.pinOrder).ToList();

        /// <summary>최근에 고정한 자료부터 (빠른 고정 목록). 순번은 고정할 때마다 새로 받으므로 해제 후 다시 고정하면 맨 위로 온다.</summary>
        public List<WorkEntry> RecentlyPinned() => entries.Values.Where(e => e.pinned).OrderByDescending(e => e.pinOrder).ToList();

        WorkEntry Ensure(RecordRef target)
        {
            if (!entries.TryGetValue(target.Key, out var e))
            {
                e = new WorkEntry { target = target };
                entries[target.Key] = e;
            }
            return e;
        }

        public void MarkViewed(RecordRef target)
        {
            if (target == null || IsViewed(target.Key))
                return;
            Ensure(target).viewed = true;
            Commit();
        }

        public void SetPinned(RecordRef target, bool pinned)
        {
            var e = Ensure(target);
            if (e.pinned == pinned)
                return;
            e.pinned = pinned;
            if (pinned)
                e.pinOrder = nextPinOrder++;
            Commit();
        }

        public void SetClassification(RecordRef target, Classification classification)
        {
            var e = Ensure(target);
            if (e.classification == classification)
                return;
            e.classification = classification;
            Commit();
        }

        public void SetMemo(RecordRef target, string memo)
        {
            memo = memo ?? string.Empty;
            if (MemoOf(target.Key) == memo)
                return;
            Ensure(target).memo = memo;
            Commit();
        }

        // ───────────── 비교 ─────────────

        public bool WasCompared(string keyA, string keyB) => comparisons.Any(c => c.IsPair(keyA, keyB));

        public IReadOnlyList<ComparisonEntry> Comparisons => comparisons;

        /// <summary>비교 화면을 실제로 연 쌍을 기록한다. 같은 쌍(순서 무관)은 다시 만들지 않는다. 새로 기록했으면 true.</summary>
        public bool RecordComparison(RecordRef a, RecordRef b)
        {
            if (a == null || b == null || a.Key == b.Key || WasCompared(a.Key, b.Key))
                return false;
            comparisons.Add(new ComparisonEntry { keyA = a.Key, keyB = b.Key, a = a, b = b });
            Commit();
            return true;
        }

        // ───────────── 초기화 ─────────────

        /// <summary>
        /// 새로 시작하기: 열람·고정·분류·메모·비교 이력·예전 튜토리얼 단계를 모두 지우고 저장한다.
        /// 저장에 실패하면 메모리를 이전 상태로 되돌리고 false(기존 저장 파일은 그대로). 성공하면 true와 함께,
        /// 함께 초기화하는 다른 저장이 실패했을 때 이전 상태를 다시 저장하는 되돌리기를 준다.
        /// </summary>
        public bool ResetAll(out Func<bool> undo)
        {
            var oldEntries = entries.Values.ToList();
            var oldSteps = completedSteps.ToList();
            var oldComparisons = comparisons.ToList();
            long oldNext = nextPinOrder;
            void Apply(IEnumerable<WorkEntry> e, IEnumerable<string> s, IEnumerable<ComparisonEntry> c, long next)
            {
                entries.Clear();
                foreach (var x in e)
                    entries[x.target.Key] = x;
                completedSteps.Clear();
                completedSteps.UnionWith(s);
                comparisons.Clear();
                comparisons.AddRange(c);
                nextPinOrder = next;
            }

            Apply(Enumerable.Empty<WorkEntry>(), Enumerable.Empty<string>(), Enumerable.Empty<ComparisonEntry>(), 1);
            if (!Save())
            {
                Apply(oldEntries, oldSteps, oldComparisons, oldNext);
                undo = null;
                return false;
            }
            undo = () =>
            {
                Apply(oldEntries, oldSteps, oldComparisons, oldNext);
                return Save();
            };
            Changed?.Invoke();
            return true;
        }

        // ───────────── 저장 ─────────────

        void Commit()
        {
            Save();
            Changed?.Invoke();
        }

        /// <summary>현재 상태를 저장한다. 실패하면 오류를 남기고 false.</summary>
        public bool Save()
        {
            var data = new WorkSaveData
            {
                nextPinOrder = nextPinOrder,
                entries = entries.Values.Where(e => !e.IsEmpty).OrderBy(e => e.target.Key).ToList(),
                completedTutorialSteps = completedSteps.OrderBy(s => s).ToList(),
                comparisons = comparisons.ToList(),
            };
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tmp, path);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WorkStore] 업무 상태를 저장하지 못했습니다: {path}\n{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 고정 순번 정리. 순번이 없거나(0 이하) 겹치는 고정 항목이 있으면, 순번이 있는 항목은 그 순서를,
        /// 없는 항목은 그보다 앞(오래된 고정)으로 불변 키 순서를 써서 1부터 다시 매긴다. 고정·분류·메모 값은 바꾸지 않는다.
        /// 다음 순번은 늘 기존 최대값보다 크게 둔다(새로 고정한 자료가 맨 위).
        /// </summary>
        void MigratePinOrder()
        {
            var pinned = entries.Values.Where(e => e.pinned).ToList();
            bool needs = pinned.Any(e => e.pinOrder <= 0) || pinned.GroupBy(e => e.pinOrder).Any(g => g.Count() > 1);
            if (needs)
            {
                long order = 1;
                foreach (var e in pinned.OrderBy(e => e.pinOrder > 0 ? 1 : 0).ThenBy(e => e.pinOrder)
                             .ThenBy(e => e.target.Key, StringComparer.Ordinal))
                    e.pinOrder = order++;
            }
            long max = pinned.Count == 0 ? 0 : pinned.Max(e => e.pinOrder);
            nextPinOrder = Math.Max(nextPinOrder, max + 1);
        }

        void Load()
        {
            if (!File.Exists(path))
                return;
            try
            {
                var data = JsonUtility.FromJson<WorkSaveData>(File.ReadAllText(path));
                if (data == null)
                    return;
                nextPinOrder = Math.Max(1, data.nextPinOrder);
                foreach (var e in data.entries ?? new List<WorkEntry>())
                {
                    if (e?.target == null || string.IsNullOrEmpty(e.target.recordId))
                        continue;
                    // 해석할 수 없는 종류도 버리지 않고 보관한다(누락 상태로 표시되고 다시 저장된다).
                    if (!e.target.IsKnownKind)
                        Debug.LogWarning($"[WorkStore] 알 수 없는 기록 종류의 저장 항목을 누락 상태로 보관합니다: {e.target.Key}");
                    // 의뢰 요청은 분류하지 않는다. 구버전 저장의 요청 분류만 미분류로 정리하고 핀·메모·열람은 유지한다.
                    else if (e.target.kind == RecordKind.Request)
                        e.classification = Classification.Unclassified;
                    entries[e.target.Key] = e;
                }
                foreach (var s in data.completedTutorialSteps ?? new List<string>())
                    completedSteps.Add(s);
                comparisons.AddRange(data.comparisons ?? new List<ComparisonEntry>());
                MigratePinOrder();
            }
            catch (Exception ex)
            {
                // 손상된 저장 파일은 덮어쓰지 않고 옆에 보관한 뒤 빈 상태로 시작한다.
                string backup = path + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                try { File.Copy(path, backup, true); } catch { /* 보관 실패는 무시 */ }
                Debug.LogError($"[WorkStore] 저장 파일을 읽지 못해 빈 상태로 시작합니다. 원본은 {backup}에 보관했습니다.\n{ex.Message}");
            }
        }
    }
}
