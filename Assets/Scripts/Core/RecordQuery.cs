using System;
using System.Collections.Generic;
using System.Linq;
using Intersection.Data;

namespace Intersection.Core
{
    /// <summary>메시지 위치 첨부에서 계산한 지도 공유 핀.</summary>
    public class SharedPin
    {
        public ThreadEntry entry;
        public MessageData message;
    }

    /// <summary>파일 앱의 한 폴더 안 내용.</summary>
    public class FolderContents
    {
        public readonly List<string> subfolders = new List<string>();
        public readonly List<FileRecord> files = new List<FileRecord>();
    }

    /// <summary>
    /// 브라우저·지도·파일·설정 기록을 기기와 접근 단계 기준으로 계산한다. 결과를 저장하지 않는다.
    /// 접근 단계 전의 자료(예: P2 클라우드)는 어떤 화면에도 나오지 않는다.
    /// </summary>
    public static class RecordQuery
    {
        /// <summary>이 기기에서 지금 접근 가능한 기록. 오래된 것부터 시각·sortKey 순.</summary>
        public static List<T> For<T>(IEnumerable<T> records, CaseData device, ProgressStage stage) where T : DeviceRecord =>
            records
                .Where(r => r != null && r.device == device && device != null && stage >= r.availableFrom)
                .OrderBy(r => r.time.TotalMinutes)
                .ThenBy(r => r.sortKey)
                .ToList();

        /// <summary>최신이 위에 오는 목록용 순서.</summary>
        public static List<T> Newest<T>(List<T> ordered) where T : DeviceRecord
        {
            var copy = new List<T>(ordered);
            copy.Reverse();
            return copy;
        }

        /// <summary>이 기기 대화의 위치 공유 메시지. 지도 데이터를 따로 복제하지 않는다.</summary>
        public static List<SharedPin> SharedPins(ContentDatabase db, CaseData device, ProgressStage stage)
        {
            var pins = new List<SharedPin>();
            foreach (var entry in DeviceQuery.Threads(db, device, stage))
            {
                foreach (var m in DeviceQuery.Ordered(entry.thread))
                {
                    if (m.attachment == AttachmentKind.Location)
                        pins.Add(new SharedPin { entry = entry, message = m });
                }
            }
            return pins
                .OrderByDescending(p => p.message.time.TotalMinutes)
                .ThenByDescending(p => p.message.sortKey)
                .ToList();
        }

        // ───────────── 파일 ─────────────

        static IEnumerable<FolderData> Folders(ContentDatabase db, CaseData device, ProgressStage stage) =>
            db.folders.Where(f => f != null && f.device == device && device != null && stage >= f.availableFrom);

        /// <summary>파일이나 폴더가 하나라도 있는 위치(로컬/클라우드…).</summary>
        public static List<RecordSource> FileLocations(ContentDatabase db, CaseData device, ProgressStage stage) =>
            For(db.files, device, stage).Select(f => f.source)
                .Concat(Folders(db, device, stage).Select(f => f.source))
                .Distinct()
                .OrderBy(s => (int)s)
                .ToList();

        /// <summary>위치 안 한 폴더의 하위 폴더와 파일. 폴더는 선언된 폴더와 파일 경로에서 함께 계산한다.</summary>
        public static FolderContents Folder(ContentDatabase db, CaseData device, ProgressStage stage, RecordSource location, string folderPath)
        {
            folderPath = Normalize(folderPath);
            var result = new FolderContents();
            var files = For(db.files, device, stage).Where(f => f.source == location).ToList();

            var allFolders = new HashSet<string>(StringComparer.Ordinal);
            foreach (var f in Folders(db, device, stage).Where(f => f.source == location))
                AddWithParents(allFolders, Normalize(f.path));
            foreach (var f in files)
                AddWithParents(allFolders, f.FolderPath);

            foreach (var path in allFolders)
            {
                if (Parent(path) == folderPath)
                    result.subfolders.Add(path);
            }
            result.subfolders.Sort(StringComparer.CurrentCulture);
            result.files.AddRange(files
                .Where(f => f.FolderPath == folderPath)
                .OrderBy(f => f.FileName, StringComparer.CurrentCulture)
                .ThenBy(f => f.sortKey));
            return result;
        }

        /// <summary>폴더 안(하위 포함)에 있는 항목 수.</summary>
        public static int CountUnder(ContentDatabase db, CaseData device, ProgressStage stage, RecordSource location, string folderPath)
        {
            var c = Folder(db, device, stage, location, folderPath);
            return c.subfolders.Count + c.files.Count;
        }

        public static string FolderName(string path)
        {
            int i = path.LastIndexOf('/');
            return i < 0 ? path : path.Substring(i + 1);
        }

        public static string Parent(string path)
        {
            int i = path.LastIndexOf('/');
            return i < 0 ? string.Empty : path.Substring(0, i);
        }

        static string Normalize(string path) => (path ?? string.Empty).Trim('/');

        static void AddWithParents(HashSet<string> set, string path)
        {
            while (!string.IsNullOrEmpty(path))
            {
                set.Add(path);
                path = Parent(path);
            }
        }

        // ───────────── 설정 ─────────────

        /// <summary>설정 항목별 가장 마지막 변경 = 현재 설정. 마지막 변경 시각이 최근인 항목이 위.</summary>
        public static List<SettingRecord> CurrentSettings(List<SettingRecord> ordered) =>
            ordered
                .Where(s => !string.IsNullOrEmpty(s.settingKey))
                .GroupBy(s => s.settingKey)
                .Select(g => g.Last())
                .OrderByDescending(s => s.time.TotalMinutes)
                .ThenByDescending(s => s.sortKey)
                .ToList();

        /// <summary>설정 항목 표시명. {contact}는 이 기기 주소록 기준 대화 상대 표시명.</summary>
        public static string SettingItemLabel(SettingRecord s, CaseData device) =>
            s.linkedThread == null
                ? s.itemLabel
                : (s.itemLabel ?? string.Empty).Replace("{contact}", DeviceQuery.DisplayName(s.linkedThread, device));
    }
}
