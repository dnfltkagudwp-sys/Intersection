using System.Collections.Generic;
using UnityEngine;

namespace Intersection.Data
{
    /// <summary>
    /// 런타임이 읽는 콘텐츠 목록. Assets/Data 아래 에셋을 추가·삭제하면 에디터가 자동으로 갱신한다.
    /// 손으로 편집할 필요가 없다.
    /// </summary>
    [CreateAssetMenu(menuName = "Intersection/Content Database", fileName = "ContentDatabase")]
    public class ContentDatabase : ScriptableObject
    {
        public List<PersonData> people = new List<PersonData>();
        public List<CaseData> cases = new List<CaseData>();
        public List<ThreadData> threads = new List<ThreadData>();
        public List<PhotoData> photos = new List<PhotoData>();
        public List<AlbumData> albums = new List<AlbumData>();
        public List<BrowserRecord> browser = new List<BrowserRecord>();
        public List<MapRecord> maps = new List<MapRecord>();
        public List<FileRecord> files = new List<FileRecord>();
        public List<FolderData> folders = new List<FolderData>();
        public List<SettingRecord> settings = new List<SettingRecord>();
    }
}
