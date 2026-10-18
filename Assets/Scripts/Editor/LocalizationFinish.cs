using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Intersection.Data;

namespace Intersection.EditorTools
{
    // Reproducible localization audit and capture production, without entering play mode.
    public static class LocalizationFinish
    {
        public static void Audit()
        {
            var table = AssetDatabase.LoadAssetAtPath<ContentLocaleTable>("Assets/Data/Content_en.asset");
            var fields = AssetDatabase.FindAssets("t:ContentAsset", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<ContentAsset>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null).SelectMany(a => a.ContentFields().Select(f => new { path = AssetDatabase.GetAssetPath(a), key = f.Key, original = f.original }))
                .ToList();
            Directory.CreateDirectory("Temp/LocalizationFinish");
            File.WriteAllLines("Temp/LocalizationFinish/missing.jsonl", fields.Where(f => !table.TryGet(f.key, out var v) || string.IsNullOrEmpty(v)).Select(f => JsonUtility.ToJson(new Missing { path = f.path, key = f.key, original = f.original })));
            File.WriteAllLines("Temp/LocalizationFinish/coverage.txt", ContentValidator.Coverage());
            File.WriteAllLines("Temp/LocalizationFinish/unchanged.jsonl", fields.Where(f => table.TryGet(f.key, out var v) && v == f.original).Select(f => JsonUtility.ToJson(new Missing { path = f.path, key = f.key, original = f.original })));
            File.WriteAllLines("Temp/LocalizationFinish/validation.txt", ContentValidator.Run().Where(i => !i.info).Select(i => (i.error ? "ERROR " : "WARN ") + i.message));
            Debug.Log("[LocalizationFinish] audit complete");
        }

        public static void Generate()
        {
            int count = CaptureRenderer.RenderAllLocalized(out var errors);
            File.WriteAllLines("Temp/LocalizationFinish/generation.txt", new[] { "Generated: " + count }.Concat(errors));
            Audit();
        }

        [Serializable] class Missing { public string path; public string key; public string original; }
    }
}
