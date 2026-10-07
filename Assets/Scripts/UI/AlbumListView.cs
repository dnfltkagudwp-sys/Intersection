using System;
using System.Collections.Generic;
using Intersection.Core;
using Intersection.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Intersection.UI
{
    /// <summary>사진 앱 최상위 화면: 앨범 목록. 앨범과 사진 수는 데이터에서 계산한다.</summary>
    public class AlbumListView : MonoBehaviour
    {
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform content;
        [SerializeField] AlbumRowView rowPrefab;

        readonly List<GameObject> items = new List<GameObject>();

        public float ScrollPosition => scroll.verticalNormalizedPosition;

        public void Show(List<AlbumData> albums, List<PhotoData> devicePhotos, UIText text, PhoneTime time, UITheme theme,
            Action<AlbumData> onOpen, float? scrollPosition)
        {
            foreach (var go in items)
                UIPool.Discard(go);
            items.Clear();

            foreach (var album in albums)
            {
                var photos = PhotoQuery.AlbumPhotos(album, devicePhotos);
                var a = album;
                var row = Instantiate(rowPrefab, content);
                row.Bind(album.title,
                    text.Format(UIKeys.PhotosAlbumCount, ("count", photos.Count.ToString(time.Culture))),
                    photos.Count > 0 ? photos[photos.Count - 1] : null,
                    theme.chevronGlyph, theme, () => onOpen(a));
                items.Add(row.gameObject);
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.verticalNormalizedPosition = scrollPosition ?? 1f;
        }
    }
}
