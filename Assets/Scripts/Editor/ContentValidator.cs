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
            /// <summary>오류·경고가 아닌 제작 상태 안내(예: 의도적으로 둔 임시 자산). 빌드를 막지 않는다.</summary>
            public bool info;
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
                else if (i.info)
                    Debug.Log("[Content] " + i.message, i.context);
                else
                    Debug.LogWarning("[Content] " + i.message, i.context);
            }
            int errors = issues.Count(i => i.error), infos = issues.Count(i => i.info);
            Debug.Log($"[Content] 검증 완료 — 오류 {errors}개, 경고 {issues.Count - errors - infos}개" +
                      (infos > 0 ? $", 임시 제작 자산 {infos}건" : string.Empty));
        }

        public static List<Issue> Run()
        {
            // 원문(데이터 에셋)을 검사한다. 플레이 중 다른 언어를 쓰고 있었어도 검사 동안만 원문으로 바꿨다가 되돌린다.
            var shownLanguage = ContentText.Current;
            ContentText.Use(null);
            try
            {
                return RunChecks();
            }
            finally
            {
                ContentText.Use(shownLanguage);
            }
        }

        static List<Issue> RunChecks()
        {
            var issues = new List<Issue>();
            void Error(string msg, Object ctx = null) => issues.Add(new Issue { error = true, message = msg, context = ctx });
            void Warn(string msg, Object ctx = null) => issues.Add(new Issue { error = false, message = msg, context = ctx });

            var configs = Find<GameConfig>();
            if (configs.Count != 1)
                Error($"GameConfig는 정확히 1개여야 합니다 (현재 {configs.Count}개).");
            var config = configs.FirstOrDefault();
            // 경로·파일명의 {folderDate:N}을 화면과 같은 기준일·형식으로 바꾼다(중복 경로·검색 검사가 변환 결과를 쓴다).
            if (config != null)
                config.ApplyPathTokens();
            // 파일 표시 문구의 {date:N}을 화면과 같은 규칙으로 바꿔 검사한다. 설정이 없으면 숫자 그대로.
            System.Func<int, string> noteDate = config != null && config.strings != null
                ? new PhoneTime(config, new UIText(config.strings)).NoteDate
                : (System.Func<int, string>)(n => n.ToString(System.Globalization.CultureInfo.InvariantCulture));
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
            var mails = Find<MailRecord>();
            var records = browser.Cast<DeviceRecord>().Concat(maps).Concat(files).Concat(settings).Concat(mails).ToList();
            var requests = Find<WorkRequestData>();
            var tutorials = Find<TutorialData>();
            var jobs = Find<ProgressJobData>();
            var reviews = Find<ReviewBatchData>();
            var recordGroups = Find<ProgressRecordGroupData>();

            // 에셋 ID
            var allAssets = people.Cast<ContentAsset>().Concat(cases).Concat(threads).Concat(photos).Concat(albums)
                .Concat(records).Concat(folders).Concat(requests).Concat(tutorials).Concat(jobs).Concat(reviews).Concat(recordGroups).ToList();
            foreach (var a in allAssets.Where(a => string.IsNullOrEmpty(a.Id)))
                Error($"ID가 비어 있습니다: {Path(a)}", a);
            foreach (var g in allAssets.Where(a => !string.IsNullOrEmpty(a.Id)).GroupBy(a => a.Id).Where(g => g.Count() > 1))
                Error($"중복 ID {g.Key}: {string.Join(", ", g.Select(Path))}", g.First());

            // 콘텐츠 현지화: 번역 키가 실제 원문을 가리키는지, 하위 블록 ID (번역 누락은 오류가 아님)
            CheckLocalization(config, allAssets, noteDate, Error, Warn);
            CheckStringLocalization(config, Error, Warn);

            // 설정
            if (config != null)
            {
                if (!config.TryGetCaseDate(out _))
                    Error("GameConfig의 caseDate가 올바른 날짜가 아닙니다.", config);
                if (config.database == null) Error("GameConfig.database가 비어 있습니다.", config);
                if (config.strings == null) Error("GameConfig.strings가 비어 있습니다.", config);
                if (config.theme == null) Error("GameConfig.theme가 비어 있습니다.", config);
                if (config.apps == null) Error("GameConfig.apps가 비어 있습니다.", config);
                // 폴더 날짜 형식은 언어 문자열이 아닌 설정값이다. 두 언어에서 같은 경로가 나와야 하므로 경로 구분자·중괄호를 만들면 안 된다.
                if (!PathTokens.IsValidFormat(config.folderDateFormat))
                    Error($"GameConfig.folderDateFormat '{config.folderDateFormat}'이 날짜 형식이 아니거나 경로 구분자·중괄호를 만듭니다.", config);
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
                CheckKey(c.mailAccountKey, $"메일 계정 줄 ({Path(c)})", c);
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
                if (!string.IsNullOrWhiteSpace(p.visibleText))
                {
                    // 사진 표시 문구는 실제 이미지에 적힌 글자의 전사본이라 이미지가 없으면 채우지 않는다.
                    if (p.image == null)
                        Warn($"실제 이미지가 없는 사진에 표시 문구(visibleText)가 있습니다. 이미지가 정해진 뒤 그 글자를 전사하세요: {where}", p);
                    CheckShownText(p.visibleText, p.evidenceRef, p.Id, p.authorNote, "사진", where, msg => Error(msg, p));
                    // 이미지에 박힌 글자라 caseDate로 바뀌지 않는다. 날짜 토큰은 파일 표시 문구에만 쓴다.
                    if (p.visibleText.IndexOf("{" + TextTokens.DateToken, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        Error($"사진 표시 문구에는 날짜 토큰을 쓸 수 없습니다(이미지 안 글자의 전사본): {where}", p);
                }
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
                foreach (var problem in PathTokens.Problems(f.path))
                    Error($"파일 경로의 토큰 오류 — {problem}: {Path(f)}", f);
                CheckVisibleText(f, noteDate, msg => Error(msg, f), msg => Warn(msg, f));
            }
            // 같은 위치 판정은 화면·검색과 같은 변환 결과로 한다({folderDate:N}이 다른 표기로 같은 날짜를 가리킬 수 있음).
            foreach (var g in files.Where(f => f.device != null && !string.IsNullOrEmpty(f.path))
                         .GroupBy(f => (f.device, f.source, f.ExpandedPath.Trim('/'))).Where(g => g.Count() > 1))
                Error($"같은 위치에 같은 경로의 파일이 있습니다 ({g.Key.Item3}): {string.Join(", ", g.Select(Path))}", g.First());
            foreach (var f in folders)
            {
                if (f.device == null)
                    Error($"폴더의 기기가 비어 있습니다: {Path(f)}", f);
                if (string.IsNullOrWhiteSpace(f.path))
                    Error($"폴더 경로가 비어 있습니다: {Path(f)}", f);
                foreach (var problem in PathTokens.Problems(f.path))
                    Error($"폴더 경로의 토큰 오류 — {problem}: {Path(f)}", f);
            }

            // 사진·파일 계보(derivedFrom): 화면·검색·비교에 쓰지 않는 제작 데이터. 기기·의뢰·출처가 달라도 되고 접근 단계는 따지지 않는다.
            CheckLineage(photos.Cast<ContentAsset>().Concat(files).ToList(), Error);

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
            // 메일 (읽기 전용): 메일함·받는 사람·상태 키, 제목·본문, 표시 문구 노출, 블록 ID
            foreach (var m in mails)
            {
                string where = Path(m);
                if (string.IsNullOrEmpty(m.mailboxKey))
                    Error($"메일함 키가 비어 있습니다: {where}", m);
                CheckKey(m.mailboxKey, where, m);
                CheckKey(m.toKey, where, m);
                CheckKey(m.statusKey, where, m);
                // 제목 없는 초안(첨부 한 줄만)·보낸 사람을 알 수 없는 복구 메일은 정상이다. 보여줄 것이 하나도 없을 때만 오류.
                bool hasBody = m.body != null && m.body.Any(b => b != null && !b.IsEmpty);
                bool hasAttachment = m.attachments != null && m.attachments.Any(b => b != null && !b.IsEmpty);
                if (string.IsNullOrWhiteSpace(m.subject) && !hasBody && !hasAttachment)
                    Error($"메일 제목·본문·첨부가 모두 비어 있습니다: {where}", m);
                var blockIds = new Dictionary<string, string>();
                var shown = new List<string> { m.subject, m.fromName, m.toText };
                foreach (var (blocks, what) in new[] { (m.body, "본문"), (m.attachments, "첨부") })
                {
                    if (blocks == null)
                        continue;
                    for (int i = 0; i < blocks.Count; i++)
                    {
                        var b = blocks[i];
                        if (b == null || b.IsEmpty)
                        {
                            Warn($"메일 {what} {i + 1}번 블록이 비어 있습니다: {where}", m);
                            continue;
                        }
                        CheckInnerId(b.Id, $"메일 {what} 블록", where, m, blockIds, Error);
                        shown.Add(b.text);
                    }
                }
                var reported = new HashSet<string>();
                foreach (var s in shown.Where(s => !string.IsNullOrEmpty(s)))
                {
                    CheckShownText(s, m.evidenceRef, m.Id, m.authorNote, "메일", where, msg => { if (reported.Add(msg)) Error(msg, m); });
                    // 메일 문구는 받은 그대로의 글자라 caseDate 토큰을 쓰지 않는다.
                    if (s.IndexOf('{') >= 0 || s.IndexOf('}') >= 0)
                    {
                        string msg = $"메일 문구에는 중괄호(토큰)를 쓸 수 없습니다: {where}";
                        if (reported.Add(msg))
                            Error(msg, m);
                    }
                }
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

            // 초반 업무 안내: 요청·대상이 삭제되거나 지원하지 않는 기록이면 오류, 화면 문구가 비면 오류
            foreach (var t in tutorials)
            {
                string where = Path(t);
                if (t.request == null)
                    Error($"업무 안내의 요청이 비어 있습니다(삭제된 대상): {where}", t);
                CheckRecordTarget(t.target, "업무 안내의 target", t.request != null ? t.request.device : null, where, t, Error);
                if (string.IsNullOrWhiteSpace(t.targetLabel))
                    Error($"업무 안내의 대상 이름(targetLabel)이 비어 있습니다: {where}", t);
                if (string.IsNullOrWhiteSpace(t.hint))
                    Error($"업무 안내 문구(hint)가 비어 있습니다: {where}", t);
                if (t.request != null && t.hintUntil <= t.request.availableFrom)
                    Error($"업무 안내 종료 단계({t.hintUntil})가 요청 접근 단계({t.request.availableFrom})보다 늦어야 합니다: {where}", t);
            }

            // 진행 연동 (UI-07): 작업·효과·도달 가능성·출처와 접근 단계·검색 인덱스·진행 저장 파일
            if (config != null && config.database != null)
                CheckProgress(config, jobs, reviews, recordGroups, cases, threads, photos, records, folders, requests, Error, Warn, CheckKey);

            // 휴대전화 화면 캡처 제작 원본: 화면 글자 = 파일 표시 문구, 미리보기 연결, 다시 렌더링 필요 여부, 임시 자산 표시
            CheckCaptures(noteDate, Error, Warn, (msg, ctx) => issues.Add(new Issue { info = true, message = msg, context = ctx }));

            // 플레이어 저장 파일: 원본에서 찾을 수 없는 참조 (실행 중에는 누락 상태로 표시되며 다른 기록으로 대체되지 않는다)
            if (config != null && config.database != null)
                CheckSaveFile(config, Warn);

            if (config != null && config.database != null)
            {
                var db = config.database;
                if (!people.All(db.people.Contains) || !cases.All(db.cases.Contains) || !threads.All(db.threads.Contains)
                    || !photos.All(db.photos.Contains) || !albums.All(db.albums.Contains)
                    || !browser.All(db.browser.Contains) || !maps.All(db.maps.Contains) || !files.All(db.files.Contains)
                    || !folders.All(db.folders.Contains) || !settings.All(db.settings.Contains) || !mails.All(db.mails.Contains)
                    || !jobs.All(db.jobs.Contains) || !reviews.All(db.reviews.Contains) || !recordGroups.All(db.recordGroups.Contains))
                    Warn("ContentDatabase가 최신이 아닙니다. Intersection/Content/Refresh Content Database를 실행하세요.", db);
                if (db.people.Any(p => p == null) || db.cases.Any(c => c == null) || db.threads.Any(t => t == null)
                    || db.photos.Any(p => p == null) || db.albums.Any(a => a == null)
                    || db.browser.Any(x => x == null) || db.maps.Any(x => x == null) || db.files.Any(x => x == null)
                    || db.folders.Any(x => x == null) || db.settings.Any(x => x == null) || db.mails.Any(x => x == null)
                    || db.jobs.Any(x => x == null) || db.reviews.Any(x => x == null) || db.recordGroups.Any(x => x == null))
                    Error("ContentDatabase에 삭제된 에셋 참조가 남아 있습니다.", db);
            }

            return issues;
        }

        /// <summary>
        /// 파일 표시 문구(visibleText)는 플레이어가 미리보기에서 읽는 문구만 담는다.
        /// 증거 ID·불변 ID·작가 메모가 들어 있으면(자동 복사 포함) 검색으로 내부 정보가 새므로 오류로 잡는다.
        /// </summary>
        static void CheckVisibleText(FileRecord f, System.Func<int, string> date, System.Action<string> error, System.Action<string> warn)
        {
            if (f.visibleText == null)
                return;
            string where = Path(f);
            string note = f.authorNote?.Trim();
            for (int i = 0; i < f.visibleText.Count; i++)
            {
                var b = f.visibleText[i];
                if (b == null || b.IsEmpty)
                {
                    warn($"파일 표시 문구 {i + 1}번 블록이 비어 있습니다: {where}");
                    continue;
                }
                foreach (var problem in TextTokens.Problems(b.text))
                    error($"파일 표시 문구 {i + 1}번 블록의 토큰 오류 — {problem}: {where}");
                // 원문과 화면·검색에 쓰이는 변환 결과 모두 검사한다(같은 문제는 한 번만 보고).
                var reported = new HashSet<string>();
                System.Action<string> once = msg => { if (reported.Add(msg)) error(msg); };
                CheckShownText(b.text, f.evidenceRef, f.Id, note, "파일", where, once);
                string shown = b.Expanded(date).text;
                if (shown != b.text)
                    CheckShownText(shown, f.evidenceRef, f.Id, note, "파일", where, once);
                if (b.kind == FileTextKind.Table && b.Rows().Select(r => r.Length).Distinct().Count() > 1)
                    warn($"파일 표시 문구 {i + 1}번 표의 행마다 칸 수가 다릅니다: {where}");
            }
        }

        /// <summary>
        /// 플레이어에게 보이는(검색되는) 표시 문구에 증거 ID·불변 ID·작가 메모가 들어 있으면 오류.
        /// 파일 미리보기 문구와 사진 안 글자 전사본이 같은 기준을 쓴다.
        /// </summary>
        static void CheckShownText(string shown, string evidenceRef, string id, string authorNote, string what, string where,
            System.Action<string> error)
        {
            string note = authorNote?.Trim();
            if (!string.IsNullOrEmpty(evidenceRef) && shown.IndexOf(evidenceRef, System.StringComparison.OrdinalIgnoreCase) >= 0)
                error($"{what} 표시 문구에 증거 ID '{evidenceRef}'가 들어 있습니다(검색에 노출됨): {where}");
            if (!string.IsNullOrEmpty(id) && shown.IndexOf(id, System.StringComparison.OrdinalIgnoreCase) >= 0)
                error($"{what} 표시 문구에 내부 ID가 들어 있습니다(검색에 노출됨): {where}");
            if (!string.IsNullOrEmpty(note) && shown.IndexOf(note, System.StringComparison.Ordinal) >= 0)
                error($"{what} 표시 문구에 작가 메모가 그대로 들어 있습니다. 실제로 보이는 글자만 넣으세요: {where}");
        }

        /// <summary>
        /// 사진·파일 계보(derivedFrom): 원본이 존재하고(끊긴 참조 없음) 같은 ID가 하나뿐인 사진·파일이며, 자기 자신이 아니고,
        /// 직간접 순환이 없고, 원본 시각이 파생본 시각보다 늦지 않아야 한다. 에셋 참조라 순서·파일명이 바뀌어도 유지된다.
        /// 기기·의뢰·출처·접근 단계는 조건이 아니다.
        /// </summary>
        static void CheckLineage(List<ContentAsset> items, System.Action<string, Object> error)
        {
            static ContentAsset Source(ContentAsset a) => a is PhotoData p ? p.derivedFrom : a is FileRecord f ? f.derivedFrom : null;
            static int Minutes(ContentAsset a) => a is PhotoData p ? p.takenAt.TotalMinutes : ((DeviceRecord)a).time.TotalMinutes;
            var idCount = items.Where(a => !string.IsNullOrEmpty(a.Id)).GroupBy(a => a.Id).ToDictionary(g => g.Key, g => g.Count());
            foreach (var a in items)
            {
                string where = Path(a);
                var src = Source(a);
                if (src == null)
                {
                    // 지정했던 원본 에셋이 삭제되면 참조 값만 남는다.
                    var prop = new SerializedObject(a).FindProperty("derivedFrom");
                    if (prop != null && !prop.objectReferenceEntityIdValue.Equals(default(EntityId)))
                        error($"계보 원본(derivedFrom)이 삭제되었거나 찾을 수 없습니다: {where}", a);
                    continue;
                }
                if (!(src is PhotoData) && !(src is FileRecord))
                {
                    error($"계보 원본(derivedFrom)은 사진 또는 파일이어야 합니다 ({src.GetType().Name}): {where}", a);
                    continue;
                }
                if (src == a)
                {
                    error($"계보 원본(derivedFrom)이 자기 자신입니다: {where}", a);
                    continue;
                }
                if (string.IsNullOrEmpty(src.Id) || !idCount.TryGetValue(src.Id, out int n) || n != 1)
                    error($"계보 원본({Path(src)})의 ID가 비어 있거나 하나로 정해지지 않습니다: {where}", a);
                if (Minutes(src) > Minutes(a))
                    error($"계보 원본({Path(src)})의 시각이 파생본보다 늦습니다(시각 역전): {where}", a);
                // 순환: 원본을 따라가다 자기 자신으로 돌아오면 오류 (다른 고리에 이어지는 경우는 그 고리에서 보고)
                var seen = new HashSet<ContentAsset> { a };
                for (var cur = src; cur != null; cur = Source(cur))
                {
                    if (cur == a)
                    {
                        error($"계보 원본(derivedFrom)이 순환합니다: {where}", a);
                        break;
                    }
                    if (!seen.Add(cur))
                        break;
                }
            }
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

        /// <summary>안내·진행 조건이 가리키는 기록: 비어 있지 않고, 분류할 수 있는 사진·기록 에셋이며, 지정한 의뢰의 기록인지.</summary>
        static void CheckRecordTarget(ContentAsset target, string what, CaseData device, string where, Object ctx,
            System.Action<string, Object> error)
        {
            if (target == null)
            {
                error($"{what}이 비어 있습니다(삭제된 대상): {where}", ctx);
                return;
            }
            var record = RecordRef.ForAsset(target);
            if (record == null || record.kind == RecordKind.Request)
                error($"{what}은 사진·기록 에셋이어야 합니다(의뢰 요청은 분류하지 않음): {where}", ctx);
            else if (device != null && record.deviceId != device.Id)
                error($"{what}이 다른 의뢰의 기록입니다: {where}", ctx);
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
