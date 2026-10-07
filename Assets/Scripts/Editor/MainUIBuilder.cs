using System;
using System.IO;
using System.Linq;
using Intersection.Data;
using Intersection.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Intersection.EditorTools
{
    /// <summary>
    /// 메인 화면 골격(씬 계층·프리팹·생성 스프라이트)을 만든다. UI 레이아웃의 유일한 원본이다.
    /// 콘텐츠·문구는 넣지 않고 문자열 키와 데이터 참조만 연결한다.
    /// 다시 실행하면 UI 루트와 Assets/Prefabs/UI의 프리팹을 새로 만든다(데이터 에셋은 건드리지 않는다).
    /// </summary>
    public static class MainUIBuilder
    {
        const string PrefabDir = "Assets/Prefabs/UI";
        const string SpriteDir = "Assets/Art/UI/Generated";
        const string RootName = "IntersectionUI";
        const float SpriteRadius = 24f;

        static UITheme theme;
        static (AlbumRowView albumRow, PhotoTileView photoTile) photoPrefabs;

        [MenuItem("Intersection/UI/Build Main Scene UI")]
        public static void Build()
        {
            var config = AssetDatabase.FindAssets("t:GameConfig", new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault();
            if (config == null || config.theme == null)
            {
                Debug.LogError("[UI] Assets/Data에 GameConfig와 Theme가 필요합니다.");
                return;
            }
            theme = config.theme;
            EnsureThemeAssets();

            Directory.CreateDirectory(PrefabDir);
            var threadRow = BuildThreadRow();
            var bubbleRow = BuildBubbleRow();
            var separator = BuildSeparator();
            var caseItem = BuildSidebarItem("CaseItem", true);
            var appItem = BuildSidebarItem("AppItem", false);
            var infoRow = BuildInfoRow();
            var section = BuildListSection();
            photoPrefabs = (BuildAlbumRow(), BuildPhotoTile());

            BuildScene(config, threadRow, bubbleRow, separator, section, caseItem, appItem, infoRow);
            Debug.Log("[UI] 화면 골격을 만들었습니다.");
        }

        // ───────────── 테마 자산 ─────────────

        static void EnsureThemeAssets()
        {
            Directory.CreateDirectory(SpriteDir);
            if (theme.regularFont == null) theme.regularFont = Font("Assets/Fonts/Pretendard/Pretendard-Regular SDF.asset");
            if (theme.mediumFont == null) theme.mediumFont = Font("Assets/Fonts/Pretendard/Pretendard-Medium SDF.asset");
            if (theme.boldFont == null) theme.boldFont = Font("Assets/Fonts/Pretendard/Pretendard-Bold SDF.asset");
            if (theme.monoFont == null) theme.monoFont = Font("Assets/Fonts/D2Coding/D2Coding-Regular SDF.asset");

            if (theme.roundedSprite == null)
                theme.roundedSprite = MakeSprite("rounded.png", 64, (x, y) => RoundedAlpha(x, y, 64, SpriteRadius), Color.white, (int)SpriteRadius);
            if (theme.circleSprite == null)
                theme.circleSprite = MakeSprite("circle.png", 128, (x, y) => RoundedAlpha(x, y, 128, 64f), Color.white, 0);
            if (theme.avatarSprite == null)
                theme.avatarSprite = MakeAvatar("avatar_person.png", 128);
            if (theme.mutedIcon == null)
                theme.mutedIcon = MakeSprite("muted.png", 64, MutedAlpha, Color.white, 0);
            if (theme.photoGlyph == null)
                theme.photoGlyph = MakeSprite("photo_placeholder.png", 64, PhotoGlyphAlpha, Color.white, 0);
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssetIfDirty(theme);
        }

        static TMP_FontAsset Font(string path) => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

        static float RoundedAlpha(int px, int py, int size, float radius)
        {
            float x = px + 0.5f, y = py + 0.5f;
            float cx = Mathf.Clamp(x, radius, size - radius);
            float cy = Mathf.Clamp(y, radius, size - radius);
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
            return Mathf.Clamp01(radius - d + 0.5f);
        }

        static Sprite MakeSprite(string file, int size, Func<int, int, float> alpha, Color color, int border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha(x, y)));
            return SaveSprite(tex, file, border);
        }

        /// <summary>알림 끔 아이콘: 종 모양에 사선. 실제 앱 아이콘을 복제하지 않은 일반 기호.</summary>
        static float MutedAlpha(int px, int py)
        {
            var p = new Vector2(px + 0.5f, py + 0.5f);
            float Disc(Vector2 c, float r) => Mathf.Clamp01(r - Vector2.Distance(p, c) + 0.5f);
            float Box(float x0, float y0, float x1, float y1) =>
                Mathf.Clamp01(Mathf.Min(Mathf.Min(p.x - x0, x1 - p.x), Mathf.Min(p.y - y0, y1 - p.y)) + 0.5f);
            float bell = Mathf.Max(Disc(new Vector2(32, 38), 15f), Box(17, 20, 47, 38));
            bell = Mathf.Max(bell, Box(11, 15, 53, 21));
            bell = Mathf.Max(bell, Disc(new Vector2(32, 9), 5f));
            bell = Mathf.Max(bell, Disc(new Vector2(32, 54), 3f));
            // 사선 (좌상 → 우하), 둘레를 비워 종과 구분한다.
            var a = new Vector2(10, 56);
            var b = new Vector2(54, 8);
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            float d = Vector2.Distance(p, a + ab * t);
            float slash = Mathf.Clamp01(3f - d + 0.5f);
            float gap = Mathf.Clamp01(7f - d + 0.5f);
            return Mathf.Max(bell * (1f - gap), slash);
        }

        /// <summary>이미지 없는 사진 자리 표시용 일반 그림 기호: 테두리 사각형 + 산 + 해. 어떤 장면도 암시하지 않는다.</summary>
        static float PhotoGlyphAlpha(int px, int py)
        {
            var p = new Vector2(px + 0.5f, py + 0.5f);
            float Box(float x0, float y0, float x1, float y1) =>
                Mathf.Clamp01(Mathf.Min(Mathf.Min(p.x - x0, x1 - p.x), Mathf.Min(p.y - y0, y1 - p.y)) + 0.5f);
            float frame = Mathf.Clamp01(Box(6, 12, 58, 52) - Box(10, 16, 54, 48));
            // 산: 두 삼각형 (아래 변 y=16)
            float Tri(float cx, float top, float half)
            {
                float h = top - 16f;
                float t = (p.y - 16f) / h;
                if (t < 0f || t > 1f) return 0f;
                float w = half * (1f - t);
                return Mathf.Clamp01(w - Mathf.Abs(p.x - cx) + 0.5f);
            }
            float mountains = Mathf.Max(Tri(24, 38, 16), Tri(40, 32, 13));
            mountains *= Box(10, 16, 54, 48);
            float sun = Mathf.Clamp01(4.5f - Vector2.Distance(p, new Vector2(44, 40)) + 0.5f);
            return Mathf.Max(frame, Mathf.Max(mountains, sun));
        }

        static Sprite MakeAvatar(string file, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var bg = new Color(0.35f, 0.35f, 0.37f);
            var fg = new Color(0.62f, 0.62f, 0.66f);
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float circle = Mathf.Clamp01(r - Vector2.Distance(p, new Vector2(r, r)) + 0.5f);
                float head = Mathf.Clamp01(size * 0.17f - Vector2.Distance(p, new Vector2(r, size * 0.62f)) + 0.5f);
                float body = Mathf.Clamp01(size * 0.33f - Vector2.Distance(p, new Vector2(r, size * 0.08f)) + 0.5f);
                var c = Color.Lerp(bg, fg, Mathf.Max(head, body));
                c.a = circle;
                tex.SetPixel(x, y, c);
            }
            return SaveSprite(tex, file, 0);
        }

        static Sprite SaveSprite(Texture2D tex, string file, int border)
        {
            string path = SpriteDir + "/" + file;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.spritePixelsPerUnit = 100;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ───────────── 공통 헬퍼 ─────────────

        static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>부모를 채우되 각 변에서 띄운다.</summary>
        static RectTransform Fill(RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>위쪽을 기준으로 가로를 채우는 띠.</summary>
        static RectTransform TopBand(RectTransform rt, float top, float height, float left = 0, float right = 0)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(left, -top - height);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        static RectTransform BottomBand(RectTransform rt, float bottom, float height, float left = 0, float right = 0)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, bottom + height);
            return rt;
        }

        static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static Image Img(RectTransform rt, Color color, Sprite sprite = null, float radius = 0f)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = radius > 0f ? Image.Type.Sliced : Image.Type.Simple;
                if (radius > 0f)
                    img.pixelsPerUnitMultiplier = SpriteRadius / radius;
            }
            img.raycastTarget = false;
            return img;
        }

        static Image Rounded(RectTransform rt, Color color, float radius) => Img(rt, color, theme.roundedSprite, radius);

        static TextMeshProUGUI Text(RectTransform rt, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, string key = null, bool wrap = false)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            t.text = string.Empty;
            if (key != null)
                rt.gameObject.AddComponent<LocalizedText>().Key = key;
            return t;
        }

        static Button MakeButton(RectTransform rt, Image target, Color normal, Color highlight)
        {
            target.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = target;
            var colors = b.colors;
            colors.normalColor = normal;
            colors.highlightedColor = highlight;
            colors.selectedColor = highlight;
            colors.pressedColor = highlight * 1.15f;
            colors.disabledColor = normal;
            colors.colorMultiplier = 1f;
            b.colors = colors;
            return b;
        }

        static void Wire(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null)
                throw new Exception($"{target.GetType().Name}.{field} 필드를 찾을 수 없습니다.");
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Image Line(RectTransform parent, string name, bool horizontal, float offset, bool fromStart = true, float inset = 0f)
        {
            var rt = Node(name, parent);
            if (horizontal)
            {
                if (fromStart) TopBand(rt, offset, 1f, inset); else BottomBand(rt, offset, 1f, inset);
            }
            else
            {
                rt.anchorMin = new Vector2(fromStart ? 0 : 1, 0);
                rt.anchorMax = new Vector2(fromStart ? 0 : 1, 1);
                rt.pivot = new Vector2(fromStart ? 0 : 1, 0.5f);
                rt.offsetMin = new Vector2(fromStart ? offset : -offset - 1, 0);
                rt.offsetMax = new Vector2(fromStart ? offset + 1 : -offset, 0);
            }
            return Img(rt, theme.border);
        }

        static ScrollRect MakeScroll(RectTransform area, out RectTransform content, float spacing, RectOffset padding)
        {
            var scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            var viewport = Fill(Node("Viewport", area));
            viewport.gameObject.AddComponent<RectMask2D>();
            var vpImage = Img(viewport, new Color(0, 0, 0, 0));
            vpImage.raycastTarget = true;
            content = Node("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = spacing;
            layout.padding = padding;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return scroll;
        }

        static VerticalLayoutGroup Stack(RectTransform rt, float spacing)
        {
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = spacing;
            return layout;
        }

        static T SavePrefab<T>(RectTransform root, string name) where T : Component
        {
            string path = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            Object.DestroyImmediate(root.gameObject);
            return prefab.GetComponent<T>();
        }

        // ───────────── 프리팹 ─────────────

        static ThreadRowView BuildThreadRow()
        {
            var root = Node("ThreadRow", null);
            root.sizeDelta = new Vector2(400, 78);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 78;
            var bg = Img(root, Color.white);
            var button = MakeButton(root, bg, new Color(1, 1, 1, 0), new Color(1, 1, 1, 0.08f));
            var view = root.gameObject.AddComponent<ThreadRowView>();

            var unread = Img(Place(Node("UnreadDot", root), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(3, 0), new Vector2(9, 9)),
                theme.unreadDot, theme.circleSprite);
            Img(Place(Node("Avatar", root), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(46, 46)),
                Color.white, theme.avatarSprite);
            // 이름 바로 뒤에 알림 끔 아이콘이 붙는다. 이름이 길면 이름만 말줄임된다.
            var titleRow = TopBand(Node("TitleRow", root), 13, 24, 74, 120);
            var titleLayout = titleRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            titleLayout.childAlignment = TextAnchor.MiddleLeft;
            titleLayout.childControlWidth = true;
            titleLayout.childControlHeight = false;
            titleLayout.childForceExpandWidth = false;
            titleLayout.childForceExpandHeight = false;
            titleLayout.spacing = 6;
            var titleRt = Node("Title", titleRow);
            titleRt.sizeDelta = new Vector2(200, 24);
            var title = Text(titleRt, theme.boldFont, theme.phoneRowTitleSize, theme.phoneText);
            var mutedRt = Node("MutedIcon", titleRow);
            mutedRt.sizeDelta = new Vector2(15, 15);
            var mutedLayout = mutedRt.gameObject.AddComponent<LayoutElement>();
            mutedLayout.minWidth = mutedLayout.preferredWidth = 15;
            var muted = Img(mutedRt, theme.phoneSubText, theme.mutedIcon);
            var date = Text(Place(Node("Date", root), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-32, -15), new Vector2(100, 20)),
                theme.regularFont, theme.phoneMetaSize, theme.phoneSubText, TextAlignmentOptions.MidlineRight);
            var chevron = Text(Place(Node("Chevron", root), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -12), new Vector2(14, 24)),
                theme.regularFont, 22, theme.phoneSubText, TextAlignmentOptions.MidlineRight);
            var preview = Text(TopBand(Node("Preview", root), 40, 22, 74, 18), theme.regularFont, theme.phoneRowPreviewSize, theme.phoneSubText);
            var divider = Node("Divider", root);
            BottomBand(divider, 0, 1, 74);
            Img(divider, theme.phoneSeparator);

            Wire(view, "button", button);
            Wire(view, "title", title);
            Wire(view, "preview", preview);
            Wire(view, "date", date);
            Wire(view, "chevron", chevron);
            Wire(view, "unreadDot", unread.gameObject);
            Wire(view, "mutedIcon", muted);
            return SavePrefab<ThreadRowView>(root, "ThreadRow");
        }

        static BubbleRowView BuildBubbleRow()
        {
            var root = Node("BubbleRow", null);
            root.sizeDelta = new Vector2(400, 44);
            var layout = root.gameObject.AddComponent<LayoutElement>();
            var view = root.gameObject.AddComponent<BubbleRowView>();
            var sender = Text(Node("Sender", root), theme.regularFont, 12, theme.phoneSubText);
            var bubble = Node("Bubble", root);
            var bubbleImage = Rounded(bubble, theme.bubbleIncoming, 18f);
            var body = Text(Fill(Node("Body", bubble)), theme.regularFont, theme.bubbleTextSize, theme.bubbleIncomingText,
                TextAlignmentOptions.TopLeft, null, true);
            var time = Text(Node("Time", root), theme.regularFont, theme.bubbleTimeSize, theme.phoneSubText);

            Wire(view, "layout", layout);
            Wire(view, "bubble", bubble);
            Wire(view, "bubbleImage", bubbleImage);
            Wire(view, "body", body);
            Wire(view, "time", time);
            Wire(view, "sender", sender);
            return SavePrefab<BubbleRowView>(root, "BubbleRow");
        }

        static DateSeparatorView BuildSeparator()
        {
            var root = Node("DateSeparator", null);
            root.sizeDelta = new Vector2(400, 42);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 42;
            var view = root.gameObject.AddComponent<DateSeparatorView>();
            var label = Text(Fill(Node("Label", root), 0, 12, 0, 6), theme.mediumFont, 13, theme.phoneSubText, TextAlignmentOptions.Center);
            Wire(view, "label", label);
            return SavePrefab<DateSeparatorView>(root, "DateSeparator");
        }

        static AlbumRowView BuildAlbumRow()
        {
            var root = Node("AlbumRow", null);
            root.sizeDelta = new Vector2(400, 84);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 84;
            var bg = Img(root, Color.white);
            var button = MakeButton(root, bg, new Color(1, 1, 1, 0), new Color(1, 1, 1, 0.08f));
            var view = root.gameObject.AddComponent<AlbumRowView>();

            var coverRt = Place(Node("Cover", root), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(64, 64));
            coverRt.gameObject.AddComponent<RectMask2D>();
            var cover = coverRt.gameObject.AddComponent<RawImage>();
            cover.raycastTarget = false;
            var glyph = Img(Place(Node("Glyph", coverRt), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30)),
                theme.photoPlaceholderGlyph, theme.photoGlyph);
            var title = Text(TopBand(Node("Title", root), 20, 24, 94, 40), theme.mediumFont, theme.phoneRowTitleSize, theme.phoneText);
            var count = Text(TopBand(Node("Count", root), 44, 20, 94, 40), theme.regularFont, theme.phoneRowPreviewSize, theme.phoneSubText);
            var chevron = Text(Place(Node("Chevron", root), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(14, 24)),
                theme.regularFont, 22, theme.phoneSubText, TextAlignmentOptions.MidlineRight);
            var divider = Node("Divider", root);
            BottomBand(divider, 0, 1, 94);
            Img(divider, theme.phoneSeparator);

            Wire(view, "button", button);
            Wire(view, "cover", cover);
            Wire(view, "coverGlyph", glyph);
            Wire(view, "title", title);
            Wire(view, "count", count);
            Wire(view, "chevron", chevron);
            return SavePrefab<AlbumRowView>(root, "AlbumRow");
        }

        static PhotoTileView BuildPhotoTile()
        {
            var root = Node("PhotoTile", null);
            root.sizeDelta = new Vector2(100, 100);
            var image = root.gameObject.AddComponent<RawImage>();
            image.color = theme.photoPlaceholder;
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            button.colors = colors;
            var view = root.gameObject.AddComponent<PhotoTileView>();
            var glyph = Img(Place(Node("Glyph", root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30)),
                theme.photoPlaceholderGlyph, theme.photoGlyph);

            Wire(view, "button", button);
            Wire(view, "image", image);
            Wire(view, "glyph", glyph);
            return SavePrefab<PhotoTileView>(root, "PhotoTile");
        }

        static ListSectionView BuildListSection()
        {
            var root = Node("ListSection", null);
            root.sizeDelta = new Vector2(400, 40);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
            var view = root.gameObject.AddComponent<ListSectionView>();
            var label = Text(Fill(Node("Label", root), 18, 12, 18, 4), theme.boldFont, 17, theme.phoneText,
                TextAlignmentOptions.BottomLeft);
            Wire(view, "label", label);
            return SavePrefab<ListSectionView>(root, "ListSection");
        }

        static SidebarItemView BuildSidebarItem(string name, bool isCase)
        {
            float height = isCase ? 60 : 46;
            var root = Node(name, null);
            root.sizeDelta = new Vector2(240, height);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var bg = Rounded(root, Color.clear, 8f);
            var button = MakeButton(root, bg, Color.white, new Color(1.25f, 1.25f, 1.25f, 1f));
            var view = root.gameObject.AddComponent<SidebarItemView>();

            TMP_Text badge = null;
            float textLeft = 14;
            if (isCase)
            {
                var badgeRt = Place(Node("Badge", root), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(34, 34));
                Img(badgeRt, theme.panelRaised, theme.circleSprite);
                badge = Text(Fill(Node("Initial", badgeRt)), theme.boldFont, 15, theme.accent, TextAlignmentOptions.Center);
                textLeft = 56;
            }
            var title = isCase
                ? Text(TopBand(Node("Title", root), 10, 22, textLeft, 10), theme.boldFont, 17, theme.text)
                : Text(Fill(Node("Title", root), textLeft, 0, 60, 0), theme.mediumFont, 16, theme.text);
            var detail = isCase
                ? Text(TopBand(Node("Detail", root), 33, 18, textLeft, 10), theme.regularFont, 13, theme.subText)
                : Text(Fill(Node("Detail", root), 0, 0, 14, 0), theme.monoFont, 13, theme.subText, TextAlignmentOptions.MidlineRight);

            Wire(view, "button", button);
            Wire(view, "background", bg);
            Wire(view, "group", group);
            Wire(view, "badge", badge);
            Wire(view, "title", title);
            Wire(view, "detail", detail);
            return SavePrefab<SidebarItemView>(root, name);
        }

        static InfoRowView BuildInfoRow()
        {
            var root = Node("InfoRow", null);
            root.sizeDelta = new Vector2(320, 30);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            var view = root.gameObject.AddComponent<InfoRowView>();
            var label = Text(Fill(Node("Label", root), 0, 0, 200, 0), theme.regularFont, 13, theme.subText);
            var value = Text(Fill(Node("Value", root), 110, 0, 0, 0), theme.regularFont, 14, theme.text, TextAlignmentOptions.MidlineRight);
            Wire(view, "label", label);
            Wire(view, "value", value);
            return SavePrefab<InfoRowView>(root, "InfoRow");
        }

        // ───────────── 씬 ─────────────

        static void BuildScene(GameConfig config, ThreadRowView threadRow, BubbleRowView bubbleRow, DateSeparatorView separator,
            ListSectionView section, SidebarItemView caseItem, SidebarItemView appItem, InfoRowView infoRow)
        {
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == RootName))
                Object.DestroyImmediate(old);

            var camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = theme.background;
            }

            var canvasGo = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = camera != null ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;

            if (Object.FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            Img(Fill(Node("Background", root)), theme.background);

            const float topH = 64, leftW = 264, rightW = 360;
            var top = BuildTopBar(root, topH);

            var body = Fill(Node("Body", root), 0, topH, 0, 0);
            var left = Node("Sidebar", body);
            left.anchorMin = new Vector2(0, 0);
            left.anchorMax = new Vector2(0, 1);
            left.pivot = new Vector2(0, 0.5f);
            left.offsetMin = Vector2.zero;
            left.offsetMax = new Vector2(leftW, 0);
            var right = Node("WorkPanel", body);
            right.anchorMin = new Vector2(1, 0);
            right.anchorMax = new Vector2(1, 1);
            right.pivot = new Vector2(1, 0.5f);
            right.offsetMin = new Vector2(-rightW, 0);
            right.offsetMax = Vector2.zero;
            var center = Fill(Node("Center", body), leftW, 0, rightW, 0);

            BuildSidebar(left, out var caseRoot, out var appRoot);
            var panel = BuildWorkPanel(right, infoRow);
            var phone = BuildCenter(center, threadRow, bubbleRow, separator, section, out var deviceLabel);

            var shell = canvasGo.AddComponent<AppShell>();
            Wire(shell, "config", config);
            Wire(shell, "topBar", top);
            Wire(shell, "caseListRoot", caseRoot);
            Wire(shell, "appListRoot", appRoot);
            Wire(shell, "caseItemPrefab", caseItem);
            Wire(shell, "appItemPrefab", appItem);
            Wire(shell, "deviceLabel", deviceLabel);
            Wire(shell, "phone", phone);
            Wire(shell, "workPanel", panel);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static TopBarView BuildTopBar(RectTransform root, float height)
        {
            var bar = TopBand(Node("TopBar", root), 0, height);
            Img(bar, theme.panel);
            Line(bar, "BottomLine", true, 0, false);
            var view = bar.gameObject.AddComponent<TopBarView>();

            var icon = Place(Node("BrandMark", bar), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(34, 34));
            Rounded(icon, theme.accentSoft, 8f);
            Text(TopBand(Node("BrandTitle", bar), 12, 24, 66, 0), theme.boldFont, 18, theme.text, key: "brand.title");
            Text(TopBand(Node("BrandSub", bar), 36, 16, 67, 0), theme.monoFont, 11, theme.subText, key: "brand.subtitle");

            var caseStatus = Text(Place(Node("CaseStatus", bar), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(420, 0), new Vector2(420, 30)),
                theme.mediumFont, 15, theme.text);

            Text(Place(Node("Session", bar), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-22, 0), new Vector2(110, 30)),
                theme.regularFont, 13, theme.subText, TextAlignmentOptions.MidlineRight, "top.session");

            // UI-07 전까지 전역 검색·알림은 비활성 상태로만 자리를 잡는다.
            var disabled = Place(Node("NotYetAvailable", bar), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-150, 0), new Vector2(430, 38));
            var group = disabled.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0.45f;
            group.interactable = false;
            group.blocksRaycasts = false;
            var search = Fill(Node("Search", disabled), 0, 0, 96, 0);
            Rounded(search, theme.panelRaised, 8f);
            Text(Fill(Node("Placeholder", search), 14, 0, 10, 0), theme.regularFont, 14, theme.subText, key: "top.searchPlaceholder");
            var notify = Place(Node("Notifications", disabled), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(84, 38));
            Rounded(notify, theme.panelRaised, 8f);
            Text(Fill(Node("Label", notify)), theme.mediumFont, 14, theme.subText, TextAlignmentOptions.Center, "top.notifications");

            var index = Place(Node("IndexStatus", bar), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-600, 0), new Vector2(260, 40));
            var indexLabel = Text(TopBand(Node("Label", index), 2, 20), theme.regularFont, 13, theme.subText, TextAlignmentOptions.MidlineRight);
            var track = Place(Node("Track", index), new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 6), new Vector2(160, 4));
            Rounded(track, theme.border, 2f);
            var fill = Img(Fill(Node("Fill", track)), theme.accent, theme.roundedSprite);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;

            Wire(view, "caseStatus", caseStatus);
            Wire(view, "indexStatus", indexLabel);
            Wire(view, "indexFill", fill);
            return view;
        }

        static void BuildSidebar(RectTransform left, out RectTransform caseRoot, out RectTransform appRoot)
        {
            Img(left, theme.panel);
            Line(left, "RightLine", false, 0, false);
            Text(TopBand(Node("CasesLabel", left), 18, 18, 18, 10), theme.mediumFont, 13, theme.subText, key: "side.cases");
            caseRoot = TopBand(Node("Cases", left), 44, 132, 10, 10);
            Stack(caseRoot, 6);
            Line(left, "Divider", true, 190, true, 0);
            Text(TopBand(Node("PhoneLabel", left), 206, 18, 18, 10), theme.mediumFont, 13, theme.subText, key: "side.phone");
            appRoot = TopBand(Node("Apps", left), 232, 320, 10, 10);
            Stack(appRoot, 4);
        }

        static WorkPanelView BuildWorkPanel(RectTransform right, InfoRowView infoRow)
        {
            Img(right, theme.panel);
            Line(right, "LeftLine", false, 0, true);
            var view = right.gameObject.AddComponent<WorkPanelView>();
            const float pad = 22;

            Text(TopBand(Node("Header", right), 18, 26, pad, pad), theme.boldFont, 18, theme.text, key: "panel.header");
            Line(right, "HeaderLine", true, 60, true);
            Text(TopBand(Node("CurrentLabel", right), 76, 18, pad, pad), theme.mediumFont, 13, theme.subText, key: "panel.current");
            var title = Text(TopBand(Node("RecordTitle", right), 98, 52, pad, pad), theme.boldFont, 19, theme.text,
                TextAlignmentOptions.TopLeft, null, true);
            var info = TopBand(Node("Info", right), 158, 180, pad, pad);
            Stack(info, 4);
            Line(right, "InfoLine", true, 350, true);

            // UI-05/06 전까지 핀·작업메모·비교는 비활성 상태로 자리만 둔다.
            var tools = Fill(Node("NotYetAvailable", right), pad, 366, pad, 22);
            var group = tools.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0.4f;
            group.interactable = false;
            group.blocksRaycasts = false;
            Text(TopBand(Node("PinnedLabel", tools), 0, 18), theme.mediumFont, 13, theme.subText, key: "panel.pinned");
            var pinned = TopBand(Node("PinnedEmpty", tools), 26, 64);
            Rounded(pinned, theme.panelRaised, 8f);
            Text(Fill(Node("Label", pinned), 14, 0, 14, 0), theme.regularFont, 14, theme.subText, key: "panel.pinnedEmpty");
            Text(TopBand(Node("MemoLabel", tools), 110, 18), theme.mediumFont, 13, theme.subText, key: "panel.memo");
            var memo = TopBand(Node("Memo", tools), 136, 96);
            Rounded(memo, theme.panelRaised, 8f);
            Text(Fill(Node("Placeholder", memo), 14, 12, 14, 12), theme.regularFont, 14, theme.subText,
                TextAlignmentOptions.TopLeft, "panel.memoPlaceholder", true);
            var clear = BottomBand(Node("ClearSelection", tools), 0, 44, 0, 0);
            clear.anchorMax = new Vector2(0.48f, 0);
            Rounded(clear, theme.panelRaised, 8f);
            Text(Fill(Node("Label", clear)), theme.mediumFont, 15, theme.text, TextAlignmentOptions.Center, "panel.clearSelection");
            var compare = BottomBand(Node("Compare", tools), 0, 44, 0, 0);
            compare.anchorMin = new Vector2(0.52f, 0);
            Rounded(compare, theme.accent, 8f);
            Text(Fill(Node("Label", compare)), theme.boldFont, 15, theme.background, TextAlignmentOptions.Center, "panel.compare");

            Wire(view, "recordTitle", title);
            Wire(view, "infoRoot", info);
            Wire(view, "rowPrefab", infoRow);
            return view;
        }

        static PhoneView BuildCenter(RectTransform center, ThreadRowView threadRow, BubbleRowView bubbleRow,
            DateSeparatorView separator, ListSectionView section, out TMP_Text deviceLabel)
        {
            Img(center, theme.stage);
            var toolbar = TopBand(Node("Toolbar", center), 0, 60);
            Line(toolbar, "BottomLine", true, 0, false);
            var deviceInfo = Fill(Node("DeviceInfo", toolbar), 24, 0, 160, 0);
            var row = deviceInfo.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = false;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            row.spacing = 12;
            var labelRt = Node("DeviceLabel", deviceInfo);
            labelRt.sizeDelta = new Vector2(200, 30);
            deviceLabel = Text(labelRt, theme.boldFont, 17, theme.text);
            var badge = Node("OriginalBadge", deviceInfo);
            badge.sizeDelta = new Vector2(84, 26);
            badge.gameObject.AddComponent<LayoutElement>().minWidth = 84;
            Rounded(badge, theme.panelRaised, 13f);
            Text(Fill(Node("Label", badge), 12, 0, 12, 0), theme.mediumFont, 13, theme.subText, TextAlignmentOptions.Center, "phone.originalBadge");
            var pin = Place(Node("SelectRecord", toolbar), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(110, 36));
            var pinGroup = pin.gameObject.AddComponent<CanvasGroup>();
            pinGroup.alpha = 0.4f;
            pinGroup.interactable = false;
            pinGroup.blocksRaycasts = false;
            Rounded(pin, theme.panelRaised, 8f);
            Text(Fill(Node("Label", pin)), theme.mediumFont, 14, theme.text, TextAlignmentOptions.Center, "toolbar.selectRecord");

            // 휴대전화 프레임: 높이에 맞춰 폭을 정한다.
            var frame = Node("PhoneFrame", center);
            frame.anchorMin = new Vector2(0.5f, 0);
            frame.anchorMax = new Vector2(0.5f, 1);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.offsetMin = new Vector2(0, 26);
            frame.offsetMax = new Vector2(0, -82);
            var fitter = frame.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            fitter.aspectRatio = 0.49f;
            Rounded(frame, theme.phoneFrame, 58f);

            var screen = Fill(Node("Screen", frame), 12, 12, 12, 12);
            Rounded(screen, theme.phoneScreen, 47f);
            screen.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var view = screen.gameObject.AddComponent<PhoneView>();

            var status = TopBand(Node("StatusBar", screen), 0, 48);
            var clock = Text(Place(Node("Clock", status), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(34, -2), new Vector2(90, 24)),
                theme.boldFont, 16, theme.phoneText);
            var indicators = Place(Node("Indicators", status), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-28, -2), new Vector2(64, 14));
            for (int i = 0; i < 4; i++)
                Img(Place(Node("Signal" + i, indicators), new Vector2(0, 0), new Vector2(0, 0), new Vector2(i * 5, 0), new Vector2(3, 5 + i * 3)), theme.phoneText);
            var battery = Place(Node("Battery", indicators), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-3, 0), new Vector2(26, 12));
            Rounded(battery, new Color(1, 1, 1, 0.9f), 3f);
            Img(Place(Node("Nub", indicators), new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(2, 5)), new Color(1, 1, 1, 0.5f));

            var appArea = Fill(Node("AppArea", screen), 0, 48, 0, 0);
            var list = BuildMessageList(appArea, threadRow, section);
            var chat = BuildChat(appArea, bubbleRow, separator);
            var albumList = BuildAlbumList(appArea);
            var photoGrid = BuildPhotoGrid(appArea);
            var photoDetail = BuildPhotoDetail(appArea);

            var home = Place(Node("HomeIndicator", screen), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(128, 5));
            Rounded(home, new Color(1, 1, 1, 0.75f), 2.5f);

            Wire(view, "clockLabel", clock);
            Wire(view, "messageList", list);
            Wire(view, "chat", chat);
            Wire(view, "albumList", albumList);
            Wire(view, "photoGrid", photoGrid);
            Wire(view, "photoDetail", photoDetail);
            return view;
        }

        /// <summary>휴대전화 앱 머리말의 뒤로가기 버튼 (파란 글자).</summary>
        static Button BackButton(RectTransform header, out TMP_Text label)
        {
            var back = Place(Node("Back", header), new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -6), new Vector2(150, 34));
            var backBg = Img(back, Color.white);
            var button = MakeButton(back, backBg, new Color(1, 1, 1, 0), new Color(1, 1, 1, 0.08f));
            label = Text(Fill(Node("Label", back), 6, 0, 0, 0), theme.regularFont, 18, theme.phoneAccent);
            return button;
        }

        static AlbumListView BuildAlbumList(RectTransform area)
        {
            var rt = Fill(Node("AlbumList", area));
            var view = rt.gameObject.AddComponent<AlbumListView>();
            Text(TopBand(Node("Title", rt), 4, 44, 18, 18), theme.boldFont, theme.phoneTitleSize, theme.phoneText, key: "app.photos.title");
            var scrollArea = Fill(Node("Scroll", rt), 0, 56, 0, 22);
            var scroll = MakeScroll(scrollArea, out var content, 0, new RectOffset(0, 0, 0, 12));
            Wire(view, "scroll", scroll);
            Wire(view, "content", content);
            Wire(view, "rowPrefab", photoPrefabs.albumRow);
            rt.gameObject.SetActive(false);
            return view;
        }

        static PhotoGridView BuildPhotoGrid(RectTransform area)
        {
            var rt = Fill(Node("PhotoGrid", area));
            var view = rt.gameObject.AddComponent<PhotoGridView>();
            var header = TopBand(Node("Header", rt), 0, 52);
            var back = BackButton(header, out var backLabel);
            var title = Text(TopBand(Node("Title", header), 12, 26, 140, 140), theme.boldFont, 17, theme.phoneText, TextAlignmentOptions.Center);

            var scrollArea = Fill(Node("Scroll", rt), 0, 56, 0, 22);
            var scroll = MakeScroll(scrollArea, out var content, 0, new RectOffset(0, 0, 0, 0));
            Object.DestroyImmediate(content.GetComponent<VerticalLayoutGroup>());
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            var empty = Text(Fill(Node("Empty", rt), 20, 140, 20, 40), theme.regularFont, 16, theme.phoneSubText, TextAlignmentOptions.Top);

            Wire(view, "backButton", back);
            Wire(view, "backLabel", backLabel);
            Wire(view, "title", title);
            Wire(view, "scroll", scroll);
            Wire(view, "content", content);
            Wire(view, "grid", grid);
            Wire(view, "tilePrefab", photoPrefabs.photoTile);
            Wire(view, "emptyLabel", empty);
            rt.gameObject.SetActive(false);
            return view;
        }

        static PhotoDetailView BuildPhotoDetail(RectTransform area)
        {
            var rt = Fill(Node("PhotoDetail", area));
            Img(rt, theme.phoneScreen);
            var view = rt.gameObject.AddComponent<PhotoDetailView>();
            var header = TopBand(Node("Header", rt), 0, 60);
            var back = BackButton(header, out var backLabel);
            var date = Text(TopBand(Node("Date", header), 8, 22, 140, 140), theme.boldFont, 15, theme.phoneText, TextAlignmentOptions.Center);
            var timeLabel = Text(TopBand(Node("Time", header), 31, 18, 140, 140), theme.regularFont, 12, theme.phoneSubText, TextAlignmentOptions.Center);

            var stage = Fill(Node("Stage", rt), 0, 64, 0, 78);
            var photoRt = Fill(Node("Photo", stage));
            var image = photoRt.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            var fitter = photoRt.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 0.75f;
            var glyph = Img(Place(Node("Glyph", photoRt), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64)),
                theme.photoPlaceholderGlyph, theme.photoGlyph);

            var bar = BottomBand(Node("BottomBar", rt), 22, 52);
            TMP_Text prevLabel, nextLabel;
            var prev = StepButton(bar, "Prev", 0f, out prevLabel);
            var next = StepButton(bar, "Next", 1f, out nextLabel);

            Wire(view, "backButton", back);
            Wire(view, "backLabel", backLabel);
            Wire(view, "dateLabel", date);
            Wire(view, "timeLabel", timeLabel);
            Wire(view, "image", image);
            Wire(view, "fitter", fitter);
            Wire(view, "glyph", glyph);
            Wire(view, "prevButton", prev);
            Wire(view, "nextButton", next);
            Wire(view, "prevLabel", prevLabel);
            Wire(view, "nextLabel", nextLabel);
            rt.gameObject.SetActive(false);
            return view;
        }

        /// <summary>한 장 보기의 앞/뒤 이동 버튼. side 0=왼쪽, 1=오른쪽.</summary>
        static Button StepButton(RectTransform bar, string name, float side, out TMP_Text label)
        {
            var rt = Place(Node(name, bar), new Vector2(side, 0.5f), new Vector2(side, 0.5f), new Vector2(side == 0f ? 20 : -20, 0), new Vector2(52, 44));
            rt.gameObject.AddComponent<CanvasGroup>();
            var bg = Img(rt, Color.white);
            var button = MakeButton(rt, bg, new Color(1, 1, 1, 0), new Color(1, 1, 1, 0.08f));
            label = Text(Fill(Node("Label", rt)), theme.regularFont, 30, theme.phoneAccent, TextAlignmentOptions.Center);
            return button;
        }

        static MessageListView BuildMessageList(RectTransform area, ThreadRowView threadRow, ListSectionView section)
        {
            var rt = Fill(Node("MessageList", area));
            var view = rt.gameObject.AddComponent<MessageListView>();
            Text(TopBand(Node("Title", rt), 4, 44, 18, 18), theme.boldFont, theme.phoneTitleSize, theme.phoneText, key: "app.messages.title");
            var search = BuildSearchField(TopBand(Node("Search", rt), 56, 38, 16, 16));
            var scrollArea = Fill(Node("Scroll", rt), 0, 104, 0, 22);
            var scroll = MakeScroll(scrollArea, out var content, 0, new RectOffset(0, 0, 0, 12));
            var empty = Text(Fill(Node("Empty", rt), 20, 140, 20, 40), theme.regularFont, 16, theme.phoneSubText, TextAlignmentOptions.Top);

            Wire(view, "searchField", search);
            Wire(view, "scroll", scroll);
            Wire(view, "content", content);
            Wire(view, "rowPrefab", threadRow);
            Wire(view, "sectionPrefab", section);
            Wire(view, "emptyLabel", empty);
            return view;
        }

        /// <summary>메시지 앱 내부 검색창 (한 줄 입력).</summary>
        static TMP_InputField BuildSearchField(RectTransform rt)
        {
            var bg = Rounded(rt, theme.phoneField, 10f);
            bg.raycastTarget = true;
            var area = Fill(Node("TextArea", rt), 14, 0, 14, 0);
            area.gameObject.AddComponent<RectMask2D>();
            var placeholder = Text(Fill(Node("Placeholder", area)), theme.regularFont, 16, theme.phoneSubText,
                key: "messages.searchPlaceholder");
            var input = Text(Fill(Node("Text", area)), theme.regularFont, 16, theme.phoneText);
            input.overflowMode = TextOverflowModes.Overflow;

            var field = rt.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textViewport = area;
            field.textComponent = input;
            field.placeholder = placeholder;
            field.fontAsset = theme.regularFont;
            field.pointSize = 16;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 40;
            field.restoreOriginalTextOnEscape = false;
            field.onFocusSelectAll = false;
            field.customCaretColor = true;
            field.caretColor = theme.phoneAccent;
            field.caretWidth = 2;
            field.selectionColor = new Color(theme.phoneAccent.r, theme.phoneAccent.g, theme.phoneAccent.b, 0.35f);
            var colors = field.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = Color.white;
            field.colors = colors;
            return field;
        }

        static ChatView BuildChat(RectTransform area, BubbleRowView bubbleRow, DateSeparatorView separator)
        {
            var rt = Fill(Node("Chat", area));
            var view = rt.gameObject.AddComponent<ChatView>();

            var header = TopBand(Node("Header", rt), 0, 92);
            Line(header, "BottomLine", true, 0, false).color = theme.phoneSeparator;
            var backButton = BackButton(header, out var backLabel);
            Img(Place(Node("Avatar", header), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(44, 44)),
                Color.white, theme.avatarSprite);
            var title = Text(TopBand(Node("Title", header), 54, 22, 40, 40), theme.mediumFont, 14, theme.phoneText, TextAlignmentOptions.Center);

            var scrollArea = Fill(Node("Scroll", rt), 0, 92, 0, 66);
            var scroll = MakeScroll(scrollArea, out var content, 0, new RectOffset(0, 0, 4, 14));

            var input = BottomBand(Node("InputBar", rt), 20, 46);
            var plus = Place(Node("Attach", input), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(34, 34));
            Img(plus, theme.phoneField, theme.circleSprite);
            var plusLabel = Text(Fill(Node("Glyph", plus)), theme.regularFont, 22, theme.phoneSubText, TextAlignmentOptions.Center);
            plusLabel.text = theme.attachGlyph;
            var field = Fill(Node("Field", input), 54, 5, 12, 5);
            Rounded(field, theme.phoneScreen, 17f);
            var outline = Fill(Node("Outline", field));
            Rounded(outline, theme.phoneSeparator, 17f);
            var inner = Fill(Node("Inner", field), 1, 1, 1, 1);
            Rounded(inner, theme.phoneScreen, 16f);
            Text(Fill(Node("Placeholder", field), 16, 0, 16, 0), theme.regularFont, 16, theme.phoneSubText, key: "chat.inputPlaceholder");

            Wire(view, "backButton", backButton);
            Wire(view, "backLabel", backLabel);
            Wire(view, "title", title);
            Wire(view, "scroll", scroll);
            Wire(view, "content", content);
            Wire(view, "bubblePrefab", bubbleRow);
            Wire(view, "separatorPrefab", separator);
            rt.gameObject.SetActive(false);
            return view;
        }
    }
}
