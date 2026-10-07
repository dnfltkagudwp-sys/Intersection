using System.Collections.Generic;
using System.Linq;
using Intersection.Data;

namespace Intersection.Core
{
    /// <summary>원본 사진 데이터에서 기기별 갤러리·앨범을 계산한다. 결과를 저장하지 않는다.</summary>
    public static class PhotoQuery
    {
        /// <summary>이 기기에서 지금 접근 가능한 모든 사진. 오래된 것부터 촬영 시각순.</summary>
        public static List<PhotoData> DevicePhotos(ContentDatabase db, CaseData device, ProgressStage stage)
        {
            if (db == null || device == null)
                return new List<PhotoData>();
            return db.photos
                .Where(p => p != null && p.device == device && stage >= p.availableFrom)
                .OrderBy(p => p.takenAt.TotalMinutes)
                .ThenBy(p => p.sortKey)
                .ToList();
        }

        public static List<AlbumData> Albums(ContentDatabase db, CaseData device)
        {
            if (db == null || device == null)
                return new List<AlbumData>();
            return db.albums.Where(a => a != null && a.device == device).OrderBy(a => a.order).ToList();
        }

        /// <summary>앨범에 속한 사진. 순서는 항상 기기 전체와 같은 촬영 시각순이다.</summary>
        public static List<PhotoData> AlbumPhotos(AlbumData album, List<PhotoData> devicePhotos)
        {
            switch (album.kind)
            {
                case AlbumKind.Screenshots:
                    return devicePhotos.Where(p => p.isScreenshot).ToList();
                case AlbumKind.Manual:
                    var members = new HashSet<PhotoData>(album.photos.Where(p => p != null));
                    return devicePhotos.Where(members.Contains).ToList();
                default:
                    return devicePhotos;
            }
        }
    }
}
