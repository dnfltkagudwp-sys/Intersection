using UnityEngine;

namespace Intersection.Data
{
    /// <summary>
    /// 파일 앱의 파일 한 개. 폴더 구조는 path에서 계산한다 (예: "Download/compare.xlsx").
    /// time은 수정 시각이다.
    /// </summary>
    [CreateAssetMenu(menuName = "Intersection/Records/File", fileName = "File")]
    public class FileRecord : DeviceRecord
    {
        [Tooltip("위치(로컬/클라우드) 안의 경로. 폴더는 '/'로 구분한다.")]
        public string path;

        [Tooltip("같은 이미지 자산을 공유하는 기록끼리 같은 값")]
        public string mediaAssetId;

        [Tooltip("미리보기 이미지. 비어 있으면 중립 자리 표시로 보인다.")]
        public Texture2D preview;

        public string FileName
        {
            get
            {
                if (string.IsNullOrEmpty(path))
                    return string.Empty;
                int i = path.LastIndexOf('/');
                return i < 0 ? path : path.Substring(i + 1);
            }
        }

        /// <summary>파일이 들어 있는 폴더 경로 (위치 최상위면 빈 문자열).</summary>
        public string FolderPath
        {
            get
            {
                if (string.IsNullOrEmpty(path))
                    return string.Empty;
                int i = path.LastIndexOf('/');
                return i < 0 ? string.Empty : path.Substring(0, i);
            }
        }
    }
}
