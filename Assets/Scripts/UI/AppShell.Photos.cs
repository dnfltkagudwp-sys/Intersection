using System.Collections.Generic;
using System.Linq;
using Intersection.Core;
using Intersection.Data;

namespace Intersection.UI
{
    /// <summary>사진 앱: 앨범 목록 → 사진 그리드 → 한 장 보기.</summary>
    public partial class AppShell
    {
        void RenderPhotos(CaseData device, DeviceNavigation nav)
        {
            var all = PhotoQuery.DevicePhotos(config.database, device, session.Stage);
            var albums = PhotoQuery.Albums(config.database, device);
            var album = nav.photoAlbumId != null ? albums.FirstOrDefault(a => a.Id == nav.photoAlbumId) : null;

            if (album == null)
            {
                nav.photoAlbumId = null;
                nav.openPhotoId = null;
                phone.Show(PhoneView.Screen.AlbumList);
                phone.AlbumList.Show(albums, all, text, time, Theme, OpenAlbum, SavedScroll(nav, DeviceNavigation.AlbumListScrollKey));
                ShowPhotoCollectionPanel(text.Get(UIKeys.PanelTitleAlbums), device, all.Count);
                return;
            }

            var photos = PhotoQuery.AlbumPhotos(album, all);
            int index = nav.openPhotoId != null ? photos.FindIndex(p => p.Id == nav.openPhotoId) : -1;
            if (index < 0)
            {
                nav.openPhotoId = null;
                phone.Show(PhoneView.Screen.PhotoGrid);
                phone.PhotoGrid.Show(album.title, text.Get(UIKeys.PhotosBack), photos, Theme, text.Get(UIKeys.PhotosEmpty),
                    Back, OpenPhoto, SavedScroll(nav, album.Id));
                ShowPhotoCollectionPanel(album.title, device, photos.Count);
                return;
            }

            var photo = photos[index];
            store.MarkViewed(RecordRef.ForPhoto(photo));
            phone.Show(PhoneView.Screen.PhotoDetail);
            phone.PhotoDetail.Show(photo, album.title, time.DateLabel(photo.takenAt), time.Time(photo.takenAt),
                index > 0, index < photos.Count - 1, Theme, Back, () => StepPhoto(-1), () => StepPhoto(1));
            ShowPhotoPanel(device, photo);
        }

        static float? SavedScroll(DeviceNavigation nav, string key) =>
            nav.scroll.TryGetValue(key, out var value) ? value : (float?)null;

        void OpenAlbum(AlbumData album)
        {
            var nav = session.Nav(session.CurrentCase);
            SaveScroll();
            nav.photoAlbumId = album.Id;
            nav.openPhotoId = null;
            // 앨범을 새로 열면 실제 사진 앱처럼 최신 사진이 보이는 아래쪽에서 시작한다.
            nav.scroll.Remove(album.Id);
            RenderCenter();
        }

        void OpenPhoto(PhotoData photo)
        {
            if (TrySelectInstead(RecordRef.ForPhoto(photo)))
                return;
            var nav = session.Nav(session.CurrentCase);
            SaveScroll();
            nav.openPhotoId = photo.Id;
            RenderCenter();
        }

        /// <summary>한 장 보기에서 앞(-1)/뒤(+1) 사진으로 이동. 순서는 앨범의 촬영 시각순.</summary>
        void StepPhoto(int delta)
        {
            var device = session.CurrentCase;
            var nav = session.Nav(device);
            if (device == null || nav.photoAlbumId == null || nav.openPhotoId == null)
                return;
            var album = PhotoQuery.Albums(config.database, device).FirstOrDefault(a => a.Id == nav.photoAlbumId);
            if (album == null)
                return;
            var photos = PhotoQuery.AlbumPhotos(album, PhotoQuery.DevicePhotos(config.database, device, session.Stage));
            int index = photos.FindIndex(p => p.Id == nav.openPhotoId);
            int next = index + delta;
            if (index < 0 || next < 0 || next >= photos.Count)
                return;
            nav.openPhotoId = photos[next].Id;
            RenderCenter();
        }

        void BackPhotos(DeviceNavigation nav)
        {
            if (nav.openPhotoId != null)
            {
                nav.openPhotoId = null;
                RenderCenter();
            }
            else if (nav.photoAlbumId != null)
            {
                SaveScroll();
                nav.photoAlbumId = null;
                RenderCenter();
            }
        }

        void ShowPhotoCollectionPanel(string title, CaseData device, int count) => Present(() =>
        {
            workPanel.Show(title, new[]
            {
                Row(UIKeys.PanelOwner, OwnerName(device), Theme.regularFont),
                Row(UIKeys.PanelSource, text.Get(UIKeys.SourceLocal), Theme.regularFont),
                Row(UIKeys.PanelPhotoCount,
                    text.Format(UIKeys.PanelPhotoCountValue, ("count", count.ToString(time.Culture))), Theme.regularFont),
            });
        }, null);

        /// <summary>파일명·기록 코드·촬영 시각·출처는 휴대전화가 아니라 업무 패널에만 표시한다. 증거 ID는 표시하지 않는다.</summary>
        void ShowPhotoPanel(CaseData device, PhotoData photo) => Present(() =>
        {
            workPanel.Show(photo.fileName, new List<WorkPanelView.Row>
            {
                Row(UIKeys.PanelRecordId, RecordCode.Format(text, UIKeys.RecordPrefixPhoto, photo.Id, device.Id), Theme.monoFont),
                Row(UIKeys.PanelOwner, OwnerName(device), Theme.regularFont),
                Row(UIKeys.PanelSource, SourceText(photo.source, photo.serviceKey), Theme.regularFont),
                Row(UIKeys.PanelTakenAt, time.SinceDeathWithTime(photo.takenAt), Theme.regularFont),
                Row(UIKeys.PanelIntegrity, string.IsNullOrEmpty(photo.integrityKey) ? null : text.Get(photo.integrityKey), Theme.regularFont),
            });
        }, RecordRef.ForPhoto(photo));
    }
}
