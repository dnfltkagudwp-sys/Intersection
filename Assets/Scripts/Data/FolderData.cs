using UnityEngine;

namespace Intersection.Data
{
    /// <summary>
    /// 파일이 없어도 존재하는 폴더. 파일 경로에 포함된 폴더는 자동으로 계산되므로 따로 만들 필요가 없다.
    /// </summary>
    [CreateAssetMenu(menuName = "Intersection/Records/Folder", fileName = "Folder")]
    public class FolderData : ContentAsset
    {
        public CaseData device;

        public RecordSource source = RecordSource.Local;

        [Tooltip("위치 안의 폴더 경로 (예: Download, Documents/Work)")]
        public string path;

        public ProgressStage availableFrom = ProgressStage.P0;
    }
}
