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
                var ordered = group
                    .OrderBy(a => System.IO.File.GetCreationTimeUtc(AssetDatabase.GetAssetPath(a)))
                    .ToList();
                foreach (var copy in ordered.Skip(1))
                {
                    copy.RegenerateId();
                    EditorUtility.SetDirty(copy);
                    Debug.LogWarning($"[Content] 복제된 에셋에 새 ID를 발급했습니다: {AssetDatabase.GetAssetPath(copy)}", copy);
                }
            }
            AssetDatabase.SaveAssets();
        }

        static List<T> Load<T>() where T : Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/Data" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(a => a != null)
                .ToList();
    }
}
