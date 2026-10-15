using System;
using System.Collections.Generic;
using System.Linq;
using Intersection.Core;
using Intersection.Data;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Intersection.EditorTools
{
    /// <summary>
    /// 콘텐츠 현지화 검증: 콘텐츠 번역 테이블의 키가 실제 콘텐츠 원문을 가리키는지(사라진 콘텐츠 ID·하위 블록 ID·알 수 없는 필드),
    /// 중복 키, 하위 블록 ID 누락·중복. 번역 누락은 원문으로 대체되므로 오류가 아니며 메뉴 `Localization Coverage`로 현황을 본다.
    /// </summary>
    public static partial class ContentValidator
    {
        /// <summary>모든 콘텐츠 에셋의 현지화 대상 원문 (키 → 필드). 같은 키가 둘이면 duplicates에 담는다.</summary>
        static Dictionary<string, (ContentField field, ContentAsset owner)> ContentFieldMap(IEnumerable<ContentAsset> assets, List<(string key, ContentAsset owner)> duplicates)
        {
            var map = new Dictionary<string, (ContentField, ContentAsset)>();
            foreach (var a in assets.Where(a => a != null && !string.IsNullOrEmpty(a.Id)))
            {
                foreach (var f in a.ContentFields())
                {
                    if (map.ContainsKey(f.Key))
                        duplicates?.Add((f.Key, a));
                    else
                        map[f.Key] = (f, a);
                }
            }
            return map;
        }

        /// <summary>중괄호가 하나라도 있는지 (닫힌 토큰·닫히지 않은 토큰·단독 중괄호 모두).</summary>
        static bool HasBrace(string s) => s.IndexOf('{') >= 0 || s.IndexOf('}') >= 0;

        /// <summary>
        /// 비어 있지 않은 번역값이 원문과 같은 규칙을 지키는지. 원문 검사와 같은 기준을 번역값에도 적용한다.
        /// · 표시 문구(번역 결과·토큰 변환 결과)에 그 기록의 증거 ID·불변 ID·작가 메모가 없어야 한다.
        /// · 토큰은 파일 미리보기의 {date:N}(문법·범위)과 설정 항목의 {contact}(원문과 같은 개수)만. 사진 전사본은 토큰 금지.
        ///   그 밖의 중괄호는 닫히지 않은 토큰·단독 중괄호까지 모두 오류.
        /// · 파일명은 원문 확장자 유지, 경로 구분자(/ \) 금지. 표 블록은 원문과 같은 행 수·칸 수.
        /// </summary>
        static void CheckTranslatedValue(ContentAsset owner, ContentField field, string value, Func<int, string> noteDate, Action<string> error)
        {
            string ownerEvidence = null, ownerNote = null;
            var extraEvidence = new List<string>();
            var extraNotes = new List<string>();
            switch (owner)
            {
                case DeviceRecord r: ownerEvidence = r.evidenceRef; ownerNote = r.authorNote; break;
                case PhotoData p: ownerEvidence = p.evidenceRef; ownerNote = p.authorNote; break;
                case WorkRequestData q: ownerEvidence = q.evidenceRef; break;
                case ThreadData t:
                    extraEvidence.AddRange(t.devices.Where(d => d != null).Select(d => d.evidenceRef));
                    foreach (var s in t.segments.Where(s => s != null))
                    {
                        extraEvidence.AddRange(s.evidenceRefs.Where(d => d != null).Select(d => d.evidenceRef));
                        extraNotes.Add(s.authorNote);
                    }
                    break;
            }
            var reported = new HashSet<string>();
            Action<string> once = msg => { if (reported.Add(msg)) error(msg); };
            void Shown(string shown)
            {
                CheckShownText(shown, ownerEvidence, owner.Id, ownerNote, "번역", "번역값", once);
                foreach (var ev in extraEvidence.Where(x => !string.IsNullOrEmpty(x)).Distinct())
                    CheckShownText(shown, ev, null, null, "번역", "번역값", once);
                foreach (var note in extraNotes.Where(x => !string.IsNullOrWhiteSpace(x)))
                    CheckShownText(shown, null, null, note, "번역", "번역값", once);
                // 메시지 번역은 메시지 ID도 노출 검사
                int m = field.field.IndexOf("message:", StringComparison.Ordinal);
                if (m >= 0)
                {
                    string messageId = field.field.Substring(m + "message:".Length).Split(':')[0];
                    CheckShownText(shown, null, messageId, null, "번역", "번역값", once);
                }
            }

            string f = field.field;
            if (owner is FileRecord file && f.StartsWith(ContentText.Field.VisibleText + ":", StringComparison.Ordinal))
            {
                // 파일 미리보기: {date:N} 문법과 변환 결과
                foreach (var problem in TextTokens.Problems(value))
                    error($"파일 미리보기 번역의 토큰 오류 — {problem}");
                Shown(value);
                string expanded = TextTokens.Expand(value, noteDate);
                if (expanded != value)
                    Shown(expanded);
                string blockId = f.Substring(ContentText.Field.VisibleText.Length + 1);
                var block = file.visibleText?.FirstOrDefault(b => b != null && b.Id == blockId);
                if (block != null && block.kind == FileTextKind.Table)
                {
                    var translated = new FileTextBlock { kind = FileTextKind.Table, text = value };
                    var rows = translated.Rows().Select(r => r.Length).ToList();
                    var originalRows = block.Rows().Select(r => r.Length).ToList();
                    if (rows.Distinct().Count() > 1)
                        error($"표 블록 번역의 행마다 칸 수가 다릅니다({string.Join("/", rows)})");
                    else if (rows.Count != originalRows.Count || !rows.SequenceEqual(originalRows))
                        error($"표 블록 번역의 행·칸 수({rows.Count}행 {rows.FirstOrDefault()}칸)가 원문({originalRows.Count}행 {originalRows.FirstOrDefault()}칸)과 다릅니다");
                }
                return;
            }

            Shown(value);
            if (owner is SettingRecord && f == ContentText.Field.ItemLabel)
            {
                const string contact = "{contact}";
                int Count(string s) => (s.Length - s.Replace(contact, string.Empty).Length) / contact.Length;
                if (Count(value) != Count(field.original))
                    error($"설정 항목 번역의 {contact} 토큰 수({Count(value)})가 원문({Count(field.original)})과 다릅니다");
                if (HasBrace(value.Replace(contact, string.Empty)))
                    error($"설정 항목 번역에는 {contact} 외의 토큰·중괄호를 쓸 수 없습니다(닫히지 않은 토큰 포함)");
                return;
            }
            if (owner is PhotoData && f == ContentText.Field.VisibleText)
            {
                if (HasBrace(value))
                    error("사진 표시 문구 번역에는 날짜 등 토큰·중괄호를 쓸 수 없습니다(이미지 안 글자의 전사본, 닫히지 않은 토큰 포함)");
                return;
            }
            if (owner is FileRecord && f == ContentText.Field.FileName)
            {
                string ext = System.IO.Path.GetExtension(field.original);
                if (!string.Equals(System.IO.Path.GetExtension(value), ext, StringComparison.OrdinalIgnoreCase))
                    error($"파일명 번역은 원래 확장자({(string.IsNullOrEmpty(ext) ? "없음" : ext)})를 유지해야 합니다");
                if (value.IndexOf('/') >= 0 || value.IndexOf('\\') >= 0)
                    error("파일명 번역에 경로 구분자(/ 또는 \\)를 쓸 수 없습니다(폴더 경로는 번역하지 않음)");
                // 파일명은 경로 토큰 {folderDate:N}만 쓸 수 있다(두 언어에서 같은 날짜 문자열로 바뀜).
                foreach (var problem in PathTokens.Problems(value))
                    error($"파일명 번역의 토큰 오류 — {problem}");
                string expanded = PathTokens.Expand(value);
                if (expanded != value)
                    Shown(expanded);
                return;
            }
            if (HasBrace(value))
                error("이 필드의 번역에는 토큰·중괄호를 쓸 수 없습니다(파일 미리보기 {date:N}·파일명 {folderDate:N}·설정 항목 {contact}만 지원, 닫히지 않은 토큰 포함)");
        }

        static void CheckLocalization(GameConfig config, List<ContentAsset> assets, Func<int, string> noteDate,
            Action<string, Object> error, Action<string, Object> warn)
        {
            // 하위 블록 ID: 번역 키가 블록을 위치가 아니라 ID로 찾는다.
            foreach (var f in assets.OfType<FileRecord>())
            {
                if (f.visibleText == null)
                    continue;
                var seen = new HashSet<string>();
                foreach (var b in f.visibleText.Where(b => b != null))
                {
                    if (string.IsNullOrEmpty(b.Id))
                        error($"파일 미리보기 블록의 불변 ID가 비어 있습니다(에셋을 다시 저장하면 발급됩니다): {Path(f)}", f);
                    else if (!seen.Add(b.Id))
                        error($"파일 미리보기 블록 ID {b.Id}가 한 파일 안에서 중복됩니다: {Path(f)}", f);
                }
            }

            var duplicates = new List<(string key, ContentAsset owner)>();
            var fields = ContentFieldMap(assets, duplicates);
            foreach (var (key, owner) in duplicates)
                error($"같은 현지화 키를 두 원문이 씁니다: {key} ({Path(owner)})", owner);
            var ownerIds = new HashSet<string>(assets.Where(a => a != null && !string.IsNullOrEmpty(a.Id)).Select(a => a.Id));

            var tables = Find<ContentLocaleTable>();
            var languages = new HashSet<string>();
            foreach (var table in tables)
            {
                string where = Path(table);
                if (string.IsNullOrWhiteSpace(table.language))
                    error($"콘텐츠 번역 테이블의 언어 코드가 비어 있습니다: {where}", table);
                else if (config != null && table.language == config.contentSourceLanguage)
                    error($"원문 언어({table.language})의 콘텐츠 번역 테이블은 만들지 않습니다(원문은 데이터 에셋): {where}", table);
                else if (!languages.Add(table.language))
                    error($"같은 언어({table.language})의 콘텐츠 번역 테이블이 둘 이상 있습니다: {where}", table);
                if (config != null && !config.contentTables.Contains(table))
                    warn($"GameConfig.contentTables에 등록되지 않은 콘텐츠 번역 테이블입니다(실행 중 쓰이지 않음): {where}", table);

                var keys = new HashSet<string>();
                foreach (var e in table.entries)
                {
                    if (e == null || string.IsNullOrWhiteSpace(e.key))
                    {
                        error($"콘텐츠 번역 테이블에 키가 빈 항목이 있습니다: {where}", table);
                        continue;
                    }
                    if (!keys.Add(e.key))
                        error($"콘텐츠 번역 키가 중복됩니다: {e.key} ({where})", table);
                    if (fields.TryGetValue(e.key, out var known))
                    {
                        if (!string.IsNullOrEmpty(e.value))
                            CheckTranslatedValue(known.owner, known.field, e.value, noteDate, msg => error($"{msg}: {e.key} ({where})", table));
                        continue;
                    }
                    int colon = e.key.IndexOf(':');
                    string ownerId = colon < 0 ? e.key : e.key.Substring(0, colon);
                    string field = colon < 0 ? string.Empty : e.key.Substring(colon + 1);
                    if (!ownerIds.Contains(ownerId))
                        error($"콘텐츠 번역 키가 사라진 콘텐츠 ID를 가리킵니다: {e.key} ({where})", table);
                    else if (field.StartsWith(ContentText.Field.VisibleText + ":"))
                        error($"콘텐츠 번역 키가 사라진 파일 미리보기 블록 ID를 가리킵니다: {e.key} ({where})", table);
                    else if (field.StartsWith("message:"))
                        error($"콘텐츠 번역 키가 사라진 메시지 ID를 가리킵니다: {e.key} ({where})", table);
                    else if (field.StartsWith(MailRecord.BodyField(string.Empty)) || field.StartsWith(MailRecord.AttachmentField(string.Empty)))
                        error($"콘텐츠 번역 키가 사라진 메일 본문·첨부 블록 ID를 가리킵니다: {e.key} ({where})", table);
                    else
                        error($"콘텐츠 번역 키가 원문이 없는 필드를 가리킵니다(알 수 없는 필드이거나 원문이 비어 있음): {e.key} ({where})", table);
                }
            }

            if (config != null)
            {
                if (config.contentTables.Any(t => t == null))
                    error("GameConfig.contentTables에 비어 있는 항목이 있습니다.", config);
                if (!string.IsNullOrEmpty(config.contentLanguage) && config.contentLanguage != config.contentSourceLanguage
                    && config.ActiveContentTable == null)
                    warn($"GameConfig.contentLanguage({config.contentLanguage})의 콘텐츠 번역 테이블이 없어 모든 콘텐츠가 원문으로 보입니다.", config);
            }
        }

        /// <summary>UI 번역 테이블은 콘텐츠와 달리 키 누락 시 원문 단위 대체가 없으므로 기본 테이블과 키·토큰을 정확히 맞춘다.</summary>
        static void CheckStringLocalization(GameConfig config, Action<string, Object> error, Action<string, Object> warn)
        {
            if (config == null || config.strings == null)
                return;

            var source = config.strings.entries
                .Where(e => e != null && !string.IsNullOrEmpty(e.key))
                .GroupBy(e => e.key)
                .ToDictionary(g => g.Key, g => g.First().value ?? string.Empty);
            var tables = config.localizedStrings ?? new List<StringTable>();
            if (tables.Any(t => t == null))
                error("GameConfig.localizedStrings에 비어 있는 항목이 있습니다.", config);

            var languages = new HashSet<string>();
            foreach (var table in tables.Where(t => t != null))
            {
                string where = Path(table);
                if (string.IsNullOrWhiteSpace(table.language))
                    error($"UI 번역 테이블의 언어 코드가 비어 있습니다: {where}", table);
                else if (table.language == config.contentSourceLanguage)
                    error($"원문 언어({table.language})의 UI 테이블은 GameConfig.strings에 둡니다: {where}", table);
                else if (!languages.Add(table.language))
                    error($"같은 언어({table.language})의 UI 번역 테이블이 둘 이상 등록되어 있습니다: {where}", table);
                if (string.IsNullOrWhiteSpace(table.cultureName))
                    error($"UI 번역 테이블의 문화권 이름이 비어 있습니다: {where}", table);

                var entries = table.entries.Where(e => e != null && !string.IsNullOrEmpty(e.key)).ToList();
                foreach (var duplicate in entries.GroupBy(e => e.key).Where(g => g.Count() > 1))
                    error($"UI 번역 키가 중복됩니다: {duplicate.Key} ({where})", table);
                var translated = entries.GroupBy(e => e.key).ToDictionary(g => g.Key, g => g.First().value ?? string.Empty);

                foreach (var pair in source)
                {
                    if (!translated.TryGetValue(pair.Key, out var value) || string.IsNullOrEmpty(value))
                    {
                        error($"UI 번역 키가 없거나 값이 비어 있습니다: {pair.Key} ({where})", table);
                        continue;
                    }
                    if (!Tokens(pair.Value).SequenceEqual(Tokens(value)))
                        error($"UI 번역의 토큰이 원문과 다릅니다: {pair.Key} ({where})", table);
                }
                foreach (var extra in translated.Keys.Where(k => !source.ContainsKey(k)))
                {
                    // 단수 변형(키.one)은 원문에 없어도 되며, 기본 키가 있고 토큰이 같아야 한다.
                    string baseKey = extra.EndsWith(UIText.OneSuffix) ? extra.Substring(0, extra.Length - UIText.OneSuffix.Length) : null;
                    if (baseKey == null || !source.TryGetValue(baseKey, out var baseValue))
                    {
                        error($"원문에 없는 UI 번역 키입니다: {extra} ({where})", table);
                        continue;
                    }
                    if (string.IsNullOrEmpty(translated[extra]))
                        error($"UI 단수 변형 값이 비어 있습니다: {extra} ({where})", table);
                    else if (!Tokens(baseValue).SequenceEqual(Tokens(translated[extra])))
                        error($"UI 단수 변형의 토큰이 원문과 다릅니다: {extra} ({where})", table);
                }
            }

            if (!string.IsNullOrEmpty(config.contentLanguage) && config.contentLanguage != config.contentSourceLanguage
                && !tables.Any(t => t != null && t.language == config.contentLanguage))
                warn($"GameConfig.contentLanguage({config.contentLanguage})의 UI 번역 테이블이 없어 UI가 원문으로 보입니다.", config);
        }

        /// <summary>문구 안의 {토큰} 목록 (순서 무관 비교용으로 정렬).</summary>
        static IEnumerable<string> Tokens(string value) =>
            System.Text.RegularExpressions.Regex.Matches(value ?? string.Empty, @"\{[^{}]+\}")
                .Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).OrderBy(x => x);

        /// <summary>언어별 번역 현황 (번역 누락은 원문으로 대체되므로 오류가 아니다).</summary>
        [MenuItem("Intersection/Content/Localization Coverage")]
        static void CoverageMenu()
        {
            foreach (var line in Coverage())
                Debug.Log("[Content] " + line);
        }

        public static List<string> Coverage()
        {
            var lines = new List<string>();
            var assets = AllContentAssets();
            var fields = ContentFieldMap(assets, null);
            var byType = fields.Values.GroupBy(v => v.owner.GetType().Name).ToDictionary(g => g.Key, g => g.Select(v => v.field.Key).ToList());
            var tables = Find<ContentLocaleTable>();
            lines.Add($"콘텐츠 원문 {fields.Count}개 키 (에셋 {assets.Count}개). 번역 테이블 {tables.Count}개.");
            foreach (var table in tables)
            {
                var translated = new HashSet<string>(table.entries.Where(e => e != null && !string.IsNullOrEmpty(e.key) && !string.IsNullOrEmpty(e.value)).Select(e => e.key));
                int done = fields.Keys.Count(translated.Contains);
                lines.Add($"{table.language}: 번역 {done}/{fields.Count}, 누락 {fields.Count - done} — " +
                          string.Join(", ", byType.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value.Count(translated.Contains)}/{kv.Value.Count}")));
            }
            return lines;
        }

        static List<ContentAsset> AllContentAssets() =>
            AssetDatabase.FindAssets("t:" + nameof(ContentAsset), new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<ContentAsset>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null)
                .ToList();
    }
}
