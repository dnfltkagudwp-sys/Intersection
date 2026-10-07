using System.Collections.Generic;
using System.Linq;
using Intersection.Data;
using UnityEditor;
using UnityEngine;

namespace Intersection.EditorTools
{
    /// <summary>
    /// Assets/Data 아래 콘텐츠 에셋이 추가·삭제·이동되면 ContentDatabase 목록을 자동 갱신한다.
    /// 새 대화방·인물·의뢰를 만들 때 코드나 목록을 손으로 고칠 필요가 없다.
    /// </summary>
    public class ContentDatabaseSync : AssetPostprocessor
    {
        const string DataRoot = "Assets/Data/";
        static bool scheduled;

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool touched = imported.Concat(deleted).Concat(moved).Concat(movedFrom)
                .Any(p => p.StartsWith(DataRoot) && p.EndsWith(".asset"));
            if (!touched || scheduled)
                return;
            scheduled = true;
            EditorApplication.delayCall += () =>
            {
                scheduled = false;
                Sync();
            };
        }

        [MenuItem("Intersection/Content/Refresh Content Database")]
        public static void Sync()
        {
            var people = Load<PersonData>();
            var cases = Load<CaseData>();
            var threads = Load<ThreadData>();
            FixDuplicateAssetIds(people.Cast<ContentAsset>().Concat(cases).Concat(threads));
            FixDuplicateInnerIds(threads);

            foreach (var db in Load<ContentDatabase>())
            {
                if (db.people.SequenceEqual(people) && db.cases.SequenceEqual(cases) && db.threads.SequenceEqual(threads))
                    continue;
                db.people = people;
                db.cases = cases;
                db.threads = threads;
                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssetIfDirty(db);
            }
        }

        /// <summary>에셋을 복제하면 불변 ID도 복사되므로, 나중에 만들어진 복제본에만 새 ID를 준다.</summary>
        static void FixDuplicateAssetIds(IEnumerable<ContentAsset> assets)
        {
            foreach (var a in assets)
            {
                if (a.EnsureIds())
                    EditorUtility.SetDirty(a);
            }
            foreach (var group in assets.GroupBy(a => a.Id).Where(g => g.Count() > 1))
            {
                foreach (var copy in ByCreation(group).Skip(1))
                {
                    // 대화 에셋은 내부 구간·메시지 ID까지 함께 새로 발급된다.
                    copy.RegenerateIds();
                    EditorUtility.SetDirty(copy);
                    Debug.LogWarning($"[Content] 복제된 에셋에 새 ID를 발급했습니다: {AssetDatabase.GetAssetPath(copy)}", copy);
                }
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 구간·메시지를 다른 대화에서 복사해 붙여 넣으면 내부 ID가 겹친다.
        /// 먼저 만들어진 대화의 ID는 유지하고, 나중 대화의 겹친 항목에만 새 ID를 준다.
        /// </summary>
        static void FixDuplicateInnerIds(IEnumerable<ThreadData> threads)
        {
            var taken = new HashSet<string>();
            bool any = false;
            foreach (var thread in ByCreation(threads))
            {
                if (thread.RegenerateTakenInnerIds(taken))
                {
                    EditorUtility.SetDirty(thread);
                    any = true;
                    Debug.LogWarning($"[Content] 다른 대화와 겹친 구간·메시지 ID를 새로 발급했습니다: {AssetDatabase.GetAssetPath(thread)}", thread);
                }
                taken.UnionWith(thread.InnerIds);
            }
            if (any)
                AssetDatabase.SaveAssets();
        }

        static IEnumerable<T> ByCreation<T>(IEnumerable<T> assets) where T : Object =>
            assets
                .OrderBy(a => System.IO.File.GetCreationTimeUtc(AssetDatabase.GetAssetPath(a)))
                .ThenBy(AssetDatabase.GetAssetPath)
                .ToList();

        static List<T> Load<T>() where T : Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/Data" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(a => a != null)
                .ToList();
    }
}
