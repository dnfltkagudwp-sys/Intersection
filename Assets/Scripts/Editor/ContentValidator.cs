using System.Collections.Generic;
using System.Linq;
using Intersection.Core;
using Intersection.Data;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Intersection.EditorTools
{
    /// <summary>
    /// 콘텐츠 참조 검증. 메뉴로 수동 실행하고, 빌드 전에도 자동으로 실행되어 오류가 있으면 빌드를 막는다.
    /// </summary>
    public static partial class ContentValidator
    {
        public class Issue
        {
            public bool error;
            public string message;
            public Object context;
        }

        [MenuItem("Intersection/Content/Validate")]
        static void ValidateMenu()
        {
            var issues = Run();
            foreach (var i in issues)
            {
                if (i.error)
                    Debug.LogError("[Content] " + i.message, i.context);
                else
                    Debug.LogWarning("[Content] " + i.message, i.context);
            }
            int errors = issues.Count(i => i.error);
            Debug.Log($"[Content] 검증 완료 — 오류 {errors}개, 경고 {issues.Count - errors}개");
        }

        public static List<Issue> Run()
        {
            var issues = new List<Issue>();
            void Error(string msg, Object ctx = null) => issues.Add(new Issue { error = true, message = msg, context = ctx });
            void Warn(string msg, Object ctx = null) => issues.Add(new Issue { error = false, message = msg, context = ctx });

            var configs = Find<GameConfig>();
            if (configs.Count != 1)
                Error($"GameConfig는 정확히 1개여야 합니다 (현재 {configs.Count}개).");
            var config = configs.FirstOrDefault();
            var people = Find<PersonData>();
            var cases = Find<CaseData>();
            var threads = Find<ThreadData>();
            var photos = Find<PhotoData>();
            var albums = Find<AlbumData>();
            var browser = Find<BrowserRecord>();
            var maps = Find<MapRecord>();
            var files = Find<FileRecord>();
            var folders = Find<FolderData>();
            var settings = Find<SettingRecord>();
            var records = browser.Cast<DeviceRecord>().Concat(maps).Concat(files).Concat(settings).ToList();
            var requests = Find<WorkRequestData>();
            var tutorials = Find<TutorialData>();
            var jobs = Find<ProgressJobData>();

            // 에셋 ID
            var allAssets = people.Cast<ContentAsset>().Concat(cases).Concat(threads).Concat(photos).Concat(albums)
                .Concat(records).Concat(folders).Concat(requests).Concat(tutorials).Concat(jobs).ToList();
            foreach (var a in allAssets.Where(a => string.IsNullOrEmpty(a.Id)))
                Error($"ID가 비어 있습니다: {Path(a)}", a);
            foreach (var g in allAssets.Where(a => !string.IsNullOrEmpty(a.Id)).GroupBy(a => a.Id).Where(g => g.Count() > 1))
                Error($"중복 ID {g.Key}: {string.Join(", ", g.Select(Path))}", g.First());

            // 설정
            if (config != null)
            {
                if (!config.TryGetCaseDate(out _))
                    Error("GameConfig의 caseDate가 올바른 날짜가 아닙니다.", config);
                if (config.database == null) Error("GameConfig.database가 비어 있습니다.", config);
                if (config.strings == null) Error("GameConfig.strings가 비어 있습니다.", config);
                if (config.theme == null) Error("GameConfig.theme가 비어 있습니다.", config);
                if (config.apps == null) Error("GameConfig.apps가 비어 있습니다.", config);
            }
            var strings = config != null ? config.strings : null;

            void CheckKey(string key, string where, Object ctx)
            {
                if (string.IsNullOrEmpty(key) || strings == null)
                    return;
                if (!strings.Contains(key))
                    Error($"{where}: 문자열 키 '{key}'가 문자열 테이블에 없습니다.", ctx);
            }

            // 문자열 테이블
            if (strings != null)
            {
                foreach (var key in UIKeys.All)
                    CheckKey(key, "코드 사용 키", strings);
                foreach (var g in strings.entries.Where(e => e != null && !string.IsNullOrEmpty(e.key)).GroupBy(e => e.key).Where(g => g.Count() > 1))
                    Error($"문자열 키 중복: {g.Key}", strings);
            }

            // 앱 등록
            if (config != null && config.apps != null)
            {
                foreach (var app in config.apps.apps)
                    CheckKey(app.nameKey, $"앱 {app.kind}", config.apps);
                foreach (var g in config.apps.apps.GroupBy(a => a.kind).Where(g => g.Count() > 1))
                    Error($"앱 종류 중복: {g.Key}", config.apps);
            }

            // UI 프리팹·씬의 고정 라벨 키
            if (strings != null)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    foreach (var label in prefab.GetComponentsInChildren<Intersection.UI.LocalizedText>(true))
                        CheckKey(label.Key, $"프리팹 {prefab.name}/{label.name}", prefab);
                }
                foreach (var label in Object.FindObjectsByType<Intersection.UI.LocalizedText>(FindObjectsInactive.Include))
                    CheckKey(label.Key, $"씬 {label.name}", label);
            }

            // 인물
            foreach (var p in people)
            {
                if (string.IsNullOrEmpty(p.fullName))
                    Warn($"인물 이름이 비어 있습니다: {Path(p)}", p);
            }

            // 의뢰(기기)
            foreach (var c in cases)
            {
                if (c.owner == null)
                    Error($"의뢰의 기기 소유자가 비어 있습니다: {Path(c)}", c);
                if (string.IsNullOrEmpty(c.unsavedContactLabel))
                    Warn($"저장되지 않은 상대 표시 문구가 비어 있어 전화번호가 대신 표시됩니다: {Path(c)}", c);
                for (int i = 0; i < c.contacts.Count; i++)
                {
                    var contact = c.contacts[i];
                    if (contact == null || contact.person == null)
                        Error($"주소록 {i}번 항목의 인물이 비어 있습니다: {Path(c)}", c);
                    else if (string.IsNullOrEmpty(contact.savedName))
                        Warn($"주소록 저장명이 비어 있습니다 ({contact.person.name}): {Path(c)}", c);
                }
                foreach (var g in c.contacts.Where(x => x?.person != null).GroupBy(x => x.person).Where(g => g.Count() > 1))
                    Error($"주소록에 같은 인물이 중복돼 있습니다 ({g.Key.name}): {Path(c)}", c);
            }

            // 대화
            var innerIds = new Dictionary<string, string>();
            foreach (var t in threads)
            {
                string where = Path(t);
                if (t.participants.Count < 2 || t.participants.Any(p => p == null))
                    Error($"참여자가 2명 미만이거나 비어 있는 항목이 있습니다: {where}", t);
                if (!t.IsGroup && t.participants.Count != 2)
                    Error($"1:1 대화는 참여자가 2명이어야 합니다. 단체방이면 groupTitle을 입력하세요: {where}", t);
                if (t.devices.Count == 0)
                    Warn($"이 대화가 표시될 기기가 없습니다: {where}", t);
                foreach (var d in t.devices)
                {
                    if (d == null || d.device == null)
                    {
                        Error($"기기 상태의 기기가 비어 있습니다: {where}", t);
                        continue;
                    }
                    if (!t.participants.Contains(d.device.owner))
                        Error($"기기 소유자({d.device.name})가 대화 참여자에 없습니다: {where}", t);
                    CheckKey(d.serviceKey, where, t);
                    CheckKey(d.integrityKey, where, t);
                    if (d.unreadCount < 0)
                        Error($"읽지 않음 수가 음수입니다: {where}", t);
                }
                foreach (var g in t.devices.Where(d => d?.device != null).GroupBy(d => d.device).Where(g => g.Count() > 1))
                    Error($"같은 기기 상태가 중복돼 있습니다 ({g.Key.name}): {where}", t);

                if (!t.AllMessages.Any())
                    Warn($"메시지가 없는 대화는 목록에 나타나지 않습니다: {where}", t);

                foreach (var s in t.segments)
                {
                    if (s == null)
                        continue;
                    CheckInnerId(s.Id, "구간", where, t, innerIds, Error);
                    foreach (var r in s.evidenceRefs)
                    {
                        if (r == null || r.device == null || string.IsNullOrEmpty(r.evidenceRef))
                            Error($"구간 증거 참조의 기기 또는 ID가 비어 있습니다: {where}", t);
                        else if (t.StateFor(r.device) == null)
                            Error($"구간 증거 참조 기기({r.device.name})에 이 대화가 없습니다: {where}", t);
                    }
                    foreach (var m in s.messages)
                    {
                        if (m == null)
                            continue;
                        CheckInnerId(m.Id, "메시지", where, t, innerIds, Error);
                        string mw = $"{where} [{m.time}]";
                        if (m.sender == null)
                            Error($"발신자가 비어 있습니다: {mw}", t);
                        else if (!t.participants.Contains(m.sender))
                            Error($"발신자({m.sender.name})가 참여자에 없습니다: {mw}", t);
                        if (!m.time.IsValid)
                            Error($"시각이 올바르지 않습니다: {mw}", t);
                        if (m.attachment == AttachmentKind.None && string.IsNullOrWhiteSpace(m.body))
                            Error($"본문과 첨부가 모두 비어 있습니다: {mw}", t);
                    }
                }

                // 같은 시각·같은 정렬값이면 표시 순서가 정해지지 않는다.
                foreach (var g in t.AllMessages.GroupBy(m => (m.time.TotalMinutes, m.sortKey)).Where(g => g.Count() > 1))
                    Error($"같은 시각·같은 sortKey 메시지가 {g.Count()}개 있어 순서가 정해지지 않습니다: {where} [{g.First().time}]", t);

                // 구간은 시간순으로 배치한다 (작성 순서와 정렬 결과가 어긋나면 시각 입력 실수일 가능성이 크다).
                RelativeTime? prevEnd = null;
                foreach (var s in t.segments.Where(s => s != null && s.messages.Count > 0))
                {
                    var start = s.messages.Min(m => m.time);
                    if (prevEnd.HasValue && start.CompareTo(prevEnd.Value) < 0)
                        Error($"구간 시각이 앞 구간보다 이릅니다(시각 역전): {where} [{start}]", t);
                    prevEnd = s.messages.Max(m => m.time);
                }
            }

            // 사진
            foreach (var p in photos)
            {
                string where = Path(p);
                if (p.device == null)
                    Error($"사진의 기기가 비어 있습니다: {where}", p);
                if (string.IsNullOrWhiteSpace(p.fileName))
                    Error($"사진 파일명이 비어 있습니다: {where}", p);
                if (!p.takenAt.IsValid)
                    Error($"촬영 시각이 올바르지 않습니다: {where}", p);
                CheckKey(p.serviceKey, where, p);
                CheckKey(p.integrityKey, where, p);
            }
            foreach (var g in photos.Where(p => p.device != null).GroupBy(p => (p.device, p.takenAt.TotalMinutes, p.sortKey)).Where(g => g.Count() > 1))
                Error($"같은 기기·같은 촬영 시각·같은 sortKey 사진이 {g.Count()}장 있어 순서가 정해지지 않습니다: {string.Join(", ", g.Select(Path))}", g.First());
            foreach (var g in photos.Where(p => p.device != null && !string.IsNullOrEmpty(p.fileName)).GroupBy(p => (p.device, p.fileName)).Where(g => g.Count() > 1))
                Error($"같은 기기에 같은 파일명이 있습니다 ({g.Key.fileName}): {string.Join(", ", g.Select(Path))}", g.First());

            // 메시지 첨부가 가리키는 이미지 자산이 사진 데이터에 있는지 (미정 자산은 비워 두면 검사하지 않는다)
            var mediaIds = new HashSet<string>(photos.Where(p => !string.IsNullOrEmpty(p.mediaAssetId)).Select(p => p.mediaAssetId));
            foreach (var t in threads)
            {
                foreach (var m in t.AllMessages.Where(m => m.attachment == AttachmentKind.Image && !string.IsNullOrEmpty(m.mediaAssetId)))
                {
                    if (!mediaIds.Contains(m.mediaAssetId))
                        Warn($"첨부 자산 '{m.mediaAssetId}'를 가진 사진 데이터가 없습니다: {Path(t)} [{m.time}]", t);
                }
            }

            // 앨범
            foreach (var a in albums)
            {
                string where = Path(a);
                if (a.device == null)
                    Error($"앨범의 기기가 비어 있습니다: {where}", a);
                if (string.IsNullOrWhiteSpace(a.title))
                    Error($"앨범 이름이 비어 있습니다: {where}", a);
                if (a.kind == AlbumKind.Manual)
                {
                    foreach (var p in a.photos)
                    {
                        if (p == null)
                            Error($"앨범에 비어 있는 사진 참조가 있습니다: {where}", a);
                        else if (p.device != a.device)
                            Error($"다른 기기의 사진이 앨범에 들어 있습니다 ({p.name}): {where}", a);
                    }
                }
                else if (a.photos.Count > 0)
                    Warn($"{a.kind} 앨범은 photos 목록을 쓰지 않습니다: {where}", a);
            }
            foreach (var c in cases)
            {
                if (photos.Any(p => p.device == c) && !albums.Any(a => a.device == c))
                    Warn($"사진은 있지만 앨범이 없어 사진 앱에서 볼 수 없습니다: {Path(c)}", c);
            }

            // 브라우저·지도·파일·설정 기록 공통
            foreach (var r in records)
            {
                string where = Path(r);
                if (r.device == null)
                    Error($"기록의 기기가 비어 있습니다: {where}", r);
                if (!r.time.IsValid)
                    Error($"기록 시각이 올바르지 않습니다: {where}", r);
                CheckKey(r.integrityKey, where, r);
            }
            foreach (var g in records.Where(r => r.device != null)
                         .GroupBy(r => (r.GetType(), r.device, r.time.TotalMinutes, r.sortKey)).Where(g => g.Count() > 1))
                Error($"같은 기기·같은 종류·같은 시각·같은 sortKey 기록이 {g.Count()}개 있어 순서가 정해지지 않습니다: {string.Join(", ", g.Select(Path))}", g.First());

            foreach (var b in browser.Where(b => string.IsNullOrWhiteSpace(b.title)))
                Error($"브라우저 기록 제목(검색어)이 비어 있습니다: {Path(b)}", b);

            foreach (var m in maps)
            {
                string where = Path(m);
                if (m.kind == MapRecordKind.Route && (string.IsNullOrWhiteSpace(m.routeFrom) || string.IsNullOrWhiteSpace(m.routeTo)))
                    Warn($"경로 조회의 출발지·도착지가 비어 있어 중립 표기로 보입니다: {where}", m);
                if (m.kind == MapRecordKind.LocationHistory)
                {
                    if (!m.endTime.IsValid)
                        Error($"위치 기록 끝 시각이 올바르지 않습니다: {where}", m);
                    else if (m.endTime.CompareTo(m.time) < 0)
                        Error($"위치 기록의 끝 시각이 시작보다 이릅니다(시각 역전): {where}", m);
                }
            }

            foreach (var f in files)
            {
                if (string.IsNullOrWhiteSpace(f.path) || f.path.EndsWith("/"))
                    Error($"파일 경로가 비어 있거나 폴더로 끝납니다: {Path(f)}", f);
            }
            foreach (var g in files.Where(f => f.device != null && !string.IsNullOrEmpty(f.path))
                         .GroupBy(f => (f.device, f.source, f.path.Trim('/'))).Where(g => g.Count() > 1))
                Error($"같은 위치에 같은 경로의 파일이 있습니다 ({g.Key.Item3}): {string.Join(", ", g.Select(Path))}", g.First());
            foreach (var f in folders)
            {
                if (f.device == null)
                    Error($"폴더의 기기가 비어 있습니다: {Path(f)}", f);
                if (string.IsNullOrWhiteSpace(f.path))
                    Error($"폴더 경로가 비어 있습니다: {Path(f)}", f);
            }

            foreach (var s in settings)
            {
                string where = Path(s);
                if (string.IsNullOrWhiteSpace(s.settingKey))
                    Error($"설정 항목 키가 비어 있습니다: {where}", s);
                if (s.linkedThread != null && s.device != null && s.linkedThread.StateFor(s.device) == null)
                    Error($"설정이 가리키는 대화가 이 기기에 없습니다(다른 기기 자료 혼입): {where}", s);
                if (s.muteChange != MuteChange.None && s.linkedThread == null)
                    Error($"알림 변경 기록에 대상 대화가 없습니다: {where}", s);
                if (s.linkedThread == null && (s.itemLabel ?? string.Empty).Contains("{contact}"))
                    Error($"{{contact}}를 쓰려면 대상 대화가 필요합니다: {where}", s);
            }
            // 대화 알림 끔 상태와 마지막 알림 변경 기록이 맞는지
            foreach (var t in threads)
            {
                foreach (var state in t.devices.Where(d => d?.device != null))
                {
                    var last = settings
                        .Where(s => s.device == state.device && s.linkedThread == t && s.muteChange != MuteChange.None)
                        .OrderBy(s => s.time.TotalMinutes).ThenBy(s => s.sortKey).LastOrDefault();
                    if (last != null && (last.muteChange == MuteChange.Mute) != state.muted)
                        Error($"대화 알림 상태({(state.muted ? "끔" : "켬")})가 마지막 설정 변경 기록과 다릅니다: {Path(t)} / {Path(last)}", t);
                    if (last == null && state.muted)
                        Warn($"알림 끔 상태인데 설정 변경 기록이 없습니다 ({state.device.name}): {Path(t)}", t);
                }
            }

            // 의뢰 요청
            foreach (var q in requests)
            {
                if (q.device == null)
                    Error($"요청의 의뢰가 비어 있습니다: {Path(q)}", q);
                if (string.IsNullOrWhiteSpace(q.title) || string.IsNullOrWhiteSpace(q.body))
                    Error($"요청 제목 또는 본문이 비어 있습니다: {Path(q)}", q);
            }

            // 업무 튜토리얼: 대상이 삭제되거나 지원하지 않는 기록이면 오류
            var stepIds = new Dictionary<string, string>();
            foreach (var t in tutorials)
            {
                string where = Path(t);
                if (t.request == null)
                    Error($"튜토리얼의 시작 요청이 비어 있습니다(삭제된 대상): {where}", t);
                foreach (var s in t.steps)
                {
                    if (s == null)
                        continue;
                    CheckInnerId(s.Id, "튜토리얼 단계", where, t, stepIds, Error);
                    CheckTutorialTarget(s.target, "target", s, t, where, Error);
                    if (s.condition == TutorialCondition.Compared)
                        CheckTutorialTarget(s.target2, "target2", s, t, where, Error);
                }
            }

            // 진행 연동 (UI-07): 작업·효과·도달 가능성·출처와 접근 단계·검색 인덱스·진행 저장 파일
            if (config != null && config.database != null)
                CheckProgress(config, jobs, cases, threads, photos, records, folders, requests, Error, Warn, CheckKey);

            // 플레이어 저장 파일: 원본에서 찾을 수 없는 참조 (실행 중에는 누락 상태로 표시되며 다른 기록으로 대체되지 않는다)
            if (config != null && config.database != null)
                CheckSaveFile(config, Warn);

            if (config != null && config.database != null)
            {
                var db = config.database;
                if (!people.All(db.people.Contains) || !cases.All(db.cases.Contains) || !threads.All(db.threads.Contains)
                    || !photos.All(db.photos.Contains) || !albums.All(db.albums.Contains)
                    || !browser.All(db.browser.Contains) || !maps.All(db.maps.Contains) || !files.All(db.files.Contains)
                    || !folders.All(db.folders.Contains) || !settings.All(db.settings.Contains))
                    Warn("ContentDatabase가 최신이 아닙니다. Intersection/Content/Refresh Content Database를 실행하세요.", db);
                if (db.people.Any(p => p == null) || db.cases.Any(c => c == null) || db.threads.Any(t => t == null)
                    || db.photos.Any(p => p == null) || db.albums.Any(a => a == null)
                    || db.browser.Any(x => x == null) || db.maps.Any(x => x == null) || db.files.Any(x => x == null)
                    || db.folders.Any(x => x == null) || db.settings.Any(x => x == null))
                    Error("ContentDatabase에 삭제된 에셋 참조가 남아 있습니다.", db);
            }

            return issues;
        }

        static void CheckInnerId(string id, string kind, string where, Object ctx, Dictionary<string, string> seen,
            System.Action<string, Object> error)
        {
            if (string.IsNullOrEmpty(id))
            {
                error($"{kind} ID가 비어 있습니다: {where}", ctx);
                return;
            }
            if (seen.TryGetValue(id, out var other))
                error($"{kind} ID {id}가 중복됩니다: {where} / {other}", ctx);
            else
                seen[id] = where;
        }

        static void CheckTutorialTarget(ContentAsset target, string field, TutorialStep step, TutorialData t, string where,
            System.Action<string, Object> error)
        {
            if (target == null)
            {
                error($"튜토리얼 단계 '{step.label}'의 {field}가 비어 있습니다(삭제된 대상): {where}", t);
                return;
            }
            var record = RecordRef.ForAsset(target);
            if (record == null)
                error($"튜토리얼 단계 '{step.label}'의 {field}는 사진·기록·요청 에셋이어야 합니다: {where}", t);
            else if (t.request != null && t.request.device != null && record.deviceId != t.request.device.Id)
                error($"튜토리얼 단계 '{step.label}'의 {field}가 요청과 다른 의뢰의 기록입니다: {where}", t);
        }

        /// <summary>에디터에서 만든 플레이어 저장 파일의 참조를 원본 데이터와 대조한다.</summary>
        static void CheckSaveFile(GameConfig config, System.Action<string, Object> warn)
        {
            string path = System.IO.Path.Combine(Application.persistentDataPath, config.workSaveFileName);
            if (!System.IO.File.Exists(path))
                return;
            var store = new WorkStore(path);
            var text = new UIText(config.strings);
            foreach (var e in store.All)
            {
                var res = RecordResolver.Resolve(e.target, config.database, text);
                if (!res.found)
                    warn($"저장 파일의 {e.target.kind} 참조를 원본에서 찾을 수 없습니다{(res.duplicate ? "(ID 중복)" : "")}: {e.target.Key} — 실행 시 누락 상태로 표시됩니다. ({path})", null);
            }

            // 비교 이력: 끊긴 참조, 키와 참조 불일치, 자기 자신과의 비교, 같은 쌍(순서 무관) 중복
            var seen = new HashSet<string>();
            foreach (var c in store.Comparisons)
            {
                foreach (var (key, r) in new[] { (c.keyA, c.a), (c.keyB, c.b) })
                {
                    if (r == null || string.IsNullOrEmpty(r.recordId))
                    {
                        warn($"비교 이력에 원본 참조가 없습니다: {key} ({path})", null);
                        continue;
                    }
                    if (r.Key != key)
                        warn($"비교 이력의 키와 참조가 다릅니다: {key} ≠ {r.Key} ({path})", null);
                    var res = RecordResolver.Resolve(r, config.database, text);
                    if (!res.found)
                        warn($"비교 이력의 {r.kind} 참조를 원본에서 찾을 수 없습니다{(res.duplicate ? "(ID 중복)" : "")}: {r.Key} ({path})", null);
                }
                if (c.keyA == c.keyB)
                    warn($"비교 이력에 같은 기록끼리의 비교가 있습니다: {c.keyA} ({path})", null);
                string pair = string.CompareOrdinal(c.keyA, c.keyB) <= 0 ? c.keyA + "|" + c.keyB : c.keyB + "|" + c.keyA;
                if (!seen.Add(pair))
                    warn($"비교 이력에 같은 쌍이 중복되어 있습니다: {c.keyA} / {c.keyB} ({path})", null);
            }
        }

        static string Path(Object o) => AssetDatabase.GetAssetPath(o);

        static List<T> Find<T>() where T : Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null)
                .ToList();
    }

    class ContentBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            ContentDatabaseSync.Sync();
            var errors = ContentValidator.Run().Where(i => i.error).ToList();
            foreach (var e in errors)
                Debug.LogError("[Content] " + e.message, e.context);
            if (errors.Count > 0)
                throw new BuildFailedException($"콘텐츠 검증 오류 {errors.Count}개 — Intersection/Content/Validate 결과를 확인하세요.");
        }
    }
}
