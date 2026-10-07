using System.Collections.Generic;
using UnityEngine;

namespace Intersection.Data
{
    public enum AlbumKind
    {
        [Tooltip("이 기기의 모든 사진")]
        AllPhotos,
        [Tooltip("isScreenshot이 켜진 사진")]
        Screenshots,
        [Tooltip("photos 목록에 넣은 사진")]
        Manual,
    }

    /// <summary>기기 사진 앱의 앨범. 사진 순서는 앨범이 아니라 촬영 시각에서 계산한다.</summary>
    [CreateAssetMenu(menuName = "Intersection/Photo Album", fileName = "Album")]
    public class AlbumData : ContentAsset
    {
        public CaseData device;

        [Tooltip("휴대전화 사진 앱에 보이는 앨범 이름")]
        public string title;

        [Tooltip("앨범 목록 표시 순서")]
        public int order;

        public AlbumKind kind = AlbumKind.AllPhotos;

        [Tooltip("Manual 앨범에 들어갈 사진")]
        public List<PhotoData> photos = new List<PhotoData>();
    }
}
