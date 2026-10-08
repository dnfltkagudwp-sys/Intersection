using System;
using System.IO;
using System.Linq;
using Intersection.Core;
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
        static (RecordRowView row, InfoRowView field, ListSectionView section) recordPrefabs;
        static PinnedItemView pinnedPrefab;
        static (Button button, TMP_Text label, Image background, TMP_Text status) toolbarSelect;
        static (Button button, TMP_Text label, Image background) toolbarRecords;
        static RectTransform requestRoot;

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
            recordPrefabs = (BuildRecordRow(), BuildPhoneFieldRow(), section);
            pinnedPrefab = BuildPinnedItem();

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
            if (theme.searchGlyph == null)
                theme.searchGlyph = MakeSprite("glyph_search.png", 64, (x, y) => Glyph(x, y, SearchShape), Color.white, 0);
            if (theme.pageGlyph == null)
                theme.pageGlyph = MakeSprite("glyph_page.png", 64, (x, y) => Glyph(x, y, PageShape), Color.white, 0);
            if (theme.pinGlyph == null)
                theme.pinGlyph = MakeSprite("glyph_pin.png", 64, (x, y) => Glyph(x, y, PinShape), Color.white, 0);
            if (theme.routeGlyph == null)
                theme.routeGlyph = MakeSprite("glyph_route.png", 64, (x, y) => Glyph(x, y, RouteShape), Color.white, 0);
            if (theme.historyGlyph == null)
                theme.historyGlyph = MakeSprite("glyph_history.png", 64, (x, y) => Glyph(x, y, ClockShape), Color.white, 0);
            if (theme.folderGlyph == null)
                theme.folderGlyph = MakeSprite("glyph_folder.png", 64, (x, y) => Glyph(x, y, FolderShape), Color.white, 0);
            if (theme.documentGlyph == null)
                theme.documentGlyph = MakeSprite("glyph_document.png", 64, (x, y) => Glyph(x, y, DocumentShape), Color.white, 0);
            if (theme.mapPlaceholder == null)
                theme.mapPlaceholder = MakeMapPlaceholder("map_placeholder.png", 256);
            if (theme.selectionOutline == null)
                theme.selectionOutline = MakeSprite("selection_outline.png", 64,
                    (x, y) => Mathf.Clamp01(RoundedAlpha(x, y, 64, SpriteRadius) - RoundedAlpha(x - 5, y - 5, 54, SpriteRadius - 5)),
                    Color.white, (int)SpriteRadius);
            if (theme.selectionCheck == null)
                theme.selectionCheck = MakeCheckBadge("selection_check.png", 64);
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

        // ───────── 일반 기호(글리프) — 실제 앱 아이콘을 복제하지 않은 단순 도형 ─────────

        static float Glyph(int px, int py, Func<Vector2, float> shape) => Mathf.Clamp01(shape(new Vector2(px + 0.5f, py + 0.5f)));

        static float Disc(Vector2 p, Vector2 c, float r) => Mathf.Clamp01(r - Vector2.Distance(p, c) + 0.5f);

        static float Ring(Vector2 p, Vector2 c, float r, float w) =>
            Mathf.Clamp01(w * 0.5f - Mathf.Abs(Vector2.Distance(p, c) - r) + 0.5f);

        static float Seg(Vector2 p, Vector2 a, Vector2 b, float w)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Mathf.Clamp01(w * 0.5f - Vector2.Distance(p, a + ab * t) + 0.5f);
        }

        static float Rect01(Vector2 p, float x0, float y0, float x1, float y1) =>
            Mathf.Clamp01(Mathf.Min(Mathf.Min(p.x - x0, x1 - p.x), Mathf.Min(p.y - y0, y1 - p.y)) + 0.5f);

        static float RectOutline(Vector2 p, float x0, float y0, float x1, float y1, float w) =>
            Mathf.Clamp01(Rect01(p, x0, y0, x1, y1) - Rect01(p, x0 + w, y0 + w, x1 - w, y1 - w));

        static float SearchShape(Vector2 p) =>
            Mathf.Max(Ring(p, new Vector2(27, 37), 15, 5), Seg(p, new Vector2(38, 26), new Vector2(54, 10), 6));

        static float PageShape(Vector2 p)
        {
            var c = new Vector2(32, 32);
            float outer = Ring(p, c, 23, 4);
            float equator = Seg(p, new Vector2(10, 32), new Vector2(54, 32), 3) * Disc(p, c, 23);
            var squashed = new Vector2(32 + (p.x - 32) / 0.45f, p.y);
            float meridian = Ring(squashed, c, 23, 4f / 0.45f * 0.45f) * Disc(p, c, 23);
            return Mathf.Max(outer, Mathf.Max(equator, meridian));
        }

        static float PinShape(Vector2 p)
        {
            float head = Disc(p, new Vector2(32, 40), 17);
            float t = Mathf.InverseLerp(40, 6, p.y);
            float tail = p.y <= 40 && p.y >= 6 ? Mathf.Clamp01(13f * (1f - t) - Mathf.Abs(p.x - 32) + 0.5f) : 0f;
            float hole = Disc(p, new Vector2(32, 40), 6.5f);
            return Mathf.Max(head, tail) * (1f - hole);
        }

        static float RouteShape(Vector2 p)
        {
            float a = Ring(p, new Vector2(15, 50), 6, 4);
            float b = Disc(p, new Vector2(49, 14), 7);
            float line = Mathf.Max(Seg(p, new Vector2(20, 46), new Vector2(42, 40), 4),
                Mathf.Max(Seg(p, new Vector2(42, 40), new Vector2(22, 26), 4), Seg(p, new Vector2(22, 26), new Vector2(44, 18), 4)));
            return Mathf.Max(Mathf.Max(a, b), line);
        }

        static float ClockShape(Vector2 p)
        {
            var c = new Vector2(32, 32);
            return Mathf.Max(Ring(p, c, 23, 4.5f),
                Mathf.Max(Seg(p, c, new Vector2(32, 48), 4.5f), Seg(p, c, new Vector2(44, 32), 4.5f)));
        }

        static float FolderShape(Vector2 p) =>
            Mathf.Max(Rect01(p, 6, 10, 58, 46), Rect01(p, 6, 44, 28, 52));

        static float DocumentShape(Vector2 p) =>
            Mathf.Max(RectOutline(p, 13, 6, 51, 58, 4.5f),
                Mathf.Max(Seg(p, new Vector2(21, 42), new Vector2(43, 42), 3.5f),
                    Mathf.Max(Seg(p, new Vector2(21, 32), new Vector2(43, 32), 3.5f), Seg(p, new Vector2(21, 22), new Vector2(35, 22), 3.5f))));

        /// <summary>선택한 항목에 붙는 작은 체크: 선택 색 원 + 흰 체크.</summary>
        static Sprite MakeCheckBadge(string file, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var fill = theme.selectionColor;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float disc = Disc(p, new Vector2(32, 32), 30);
                float mark = Mathf.Max(Seg(p, new Vector2(18, 33), new Vector2(28, 22), 7), Seg(p, new Vector2(28, 22), new Vector2(47, 43), 7));
                var c = Color.Lerp(fill, Color.white, mark);
                c.a = disc;
                tex.SetPixel(x, y, c);
            }
            return SaveSprite(tex, file, 0);
        }

        /// <summary>현실 지형을 암시하지 않는 중립 지도 자리 표시: 균일한 격자뿐이다.</summary>
        static Sprite MakeMapPlaceholder(string file, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var bg = new Color(0.17f, 0.18f, 0.19f, 1f);
            var line = new Color(0.24f, 0.25f, 0.27f, 1f);
            const int step = 32;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool grid = x % step < 3 || y % step < 3;
                tex.SetPixel(x, y, grid ? line : bg);
            }
            return SaveSprite(tex, file, 0);
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

        static void WireBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 선택 모드 표시(얇은 테두리·작은 체크·작은 핀)를 host 위에 겹친다. 평상시에는 모두 꺼져 있고,
        /// SelectableRecord가 선택 모드에서만 켠다. inset이 음수면 host 바깥으로 살짝 나온다.
        /// </summary>
        static SelectableRecord AddSelection(RectTransform host, Button button, bool selectOnClick, float inset)
        {
            var selectable = host.gameObject.AddComponent<SelectableRecord>();
            var outline = Fill(Node("SelectionOutline", host), inset, inset, inset, inset);
            Img(outline, theme.selectionColor, theme.selectionOutline, 12f);
            outline.gameObject.SetActive(false);
            var check = Place(Node("SelectionCheck", host), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-10, -10), new Vector2(18, 18));
            Img(check, Color.white, theme.selectionCheck).preserveAspect = true;
            check.gameObject.SetActive(false);
            var pin = Place(Node("PinMark", host), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-30, -10), new Vector2(14, 14));
            Img(pin, theme.pinMarkColor, theme.pinGlyph).preserveAspect = true;
            pin.gameObject.SetActive(false);

            Wire(selectable, "button", button);
            WireBool(selectable, "selectOnClick", selectOnClick);
            Wire(selectable, "outline", outline.gameObject);
            Wire(selectable, "check", check.gameObject);
            Wire(selectable, "pinMark", pin.gameObject);
            return selectable;
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
            Wire(view, "selectable", AddSelection(root, button, false, 3f));
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
            // 말풍선 버튼은 선택 모드에서만 눌린다 (평상시에는 아무 반응 없음).
            var bubbleButton = bubble.gameObject.AddComponent<Button>();
            bubbleButton.transition = Selectable.Transition.None;
            bubbleButton.targetGraphic = bubbleImage;
            Wire(view, "selectable", AddSelection(bubble, bubbleButton, true, -3f));

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
            Wire(view, "selectable", AddSelection(root, button, false, 2f));
            return SavePrefab<PhotoTileView>(root, "PhotoTile");
        }

        static RecordRowView BuildRecordRow()
        {
            var root = Node("RecordRow", null);
            root.sizeDelta = new Vector2(400, 66);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 66;
            var bg = Img(root, Color.white);
            var button = MakeButton(root, bg, new Color(1, 1, 1, 0), new Color(1, 1, 1, 0.08f));
            var colors = button.colors;
            colors.disabledColor = new Color(1, 1, 1, 0);
            button.colors = colors;
            var view = root.gameObject.AddComponent<RecordRowView>();

            var icon = Img(Place(Node("Icon", root), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(26, 26)),
                theme.phoneSubText);
            icon.preserveAspect = true;
            var block = Fill(Node("Text", root), 58, 0, 150, 0);
            var title = Text(TopBand(Node("Title", block), 10, 26), theme.mediumFont, theme.phoneRowTitleSize - 1, theme.phoneText);
            var subtitle = Text(TopBand(Node("Subtitle", block), 36, 20), theme.regularFont, theme.phoneMetaSize, theme.phoneSubText);
            var trailing = Text(Place(Node("Trailing", root), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-32, 0), new Vector2(140, 22)),
                theme.regularFont, theme.phoneMetaSize, theme.phoneSubText, TextAlignmentOptions.MidlineRight);
            var chevron = Text(Place(Node("Chevron", root), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(14, 24)),
                theme.regularFont, 22, theme.phoneSubText, TextAlignmentOptions.MidlineRight);
            var divider = Node("Divider", root);
            BottomBand(divider, 0, 1, 18);
            Img(divider, theme.phoneSeparator);

            Wire(view, "selectable", AddSelection(root, button, false, 3f));
            Wire(view, "button", button);
            Wire(view, "icon", icon);
            Wire(view, "title", title);
            Wire(view, "subtitle", subtitle);
            Wire(view, "trailing", trailing);
            Wire(view, "chevron", chevron);
            Wire(view, "textBlock", block);
            return SavePrefab<RecordRowView>(root, "RecordRow");
        }

        /// <summary>휴대전화 상세 화면의 이름·값 한 줄 (업무 패널 InfoRow와 같은 컴포넌트, 휴대전화 글꼴 크기).</summary>
        static InfoRowView BuildPhoneFieldRow()
        {
            var root = Node("PhoneFieldRow", null);
            root.sizeDelta = new Vector2(380, 46);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 46;
            var view = root.gameObject.AddComponent<InfoRowView>();
            var label = Text(Fill(Node("Label", root), 0, 0, 260, 0), theme.regularFont, 15, theme.phoneSubText);
            var value = Text(Fill(Node("Value", root), 100, 0, 0, 0), theme.regularFont, 16, theme.phoneText, TextAlignmentOptions.MidlineRight);
            var divider = Node("Divider", root);
            BottomBand(divider, 0, 1);
            Img(divider, theme.phoneSeparator);
            Wire(view, "label", label);
            Wire(view, "value", value);
            return SavePrefab<InfoRowView>(root, "PhoneFieldRow");
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

            var canvasGo = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(MinScaleCanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = camera != null ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            // 작은 화면에서도 테마의 최소 배율 아래로 줄이지 않는다(글자·버튼 가독성).
            Wire(scaler, "theme", theme);
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
            var compareView = BuildCompare(center, bubbleRow, separator, infoRow);
            var searchView = BuildSearch(center);
            // 비교 화면이 열려 있는 동안 좌측·우측 조작을 막는다(종료 후 상태 그대로 복귀).
            var sidebarGroup = left.gameObject.AddComponent<CanvasGroup>();
            var workPanelGroup = right.gameObject.AddComponent<CanvasGroup>();

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
            Wire(shell, "selectToggle", toolbarSelect.button);
            Wire(shell, "selectToggleLabel", toolbarSelect.label);
            Wire(shell, "selectToggleBackground", toolbarSelect.background);
            Wire(shell, "selectStatus", toolbarSelect.status);
            Wire(shell, "workRecordsButton", toolbarRecords.button);
            Wire(shell, "workRecordsLabel", toolbarRecords.label);
            Wire(shell, "workRecordsBackground", toolbarRecords.background);
            Wire(shell, "requestListRoot", requestRoot);
            Wire(shell, "compareView", compareView);
            Wire(shell, "sidebarGroup", sidebarGroup);
            Wire(shell, "workPanelGroup", workPanelGroup);
            // 업무 알림 목록은 화면 맨 위에 그린다.
            Wire(shell, "searchView", searchView);
            Wire(shell, "notificationPanel", BuildNotifications(root, topH));

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

            // 전역 검색: 현재 접근·인덱싱된 자료만 찾는다. 입력하면 중앙에 검색 결과 화면이 열린다.
            var search = Place(Node("Search", bar), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-246, 0), new Vector2(330, 38));
            var searchGroup = search.gameObject.AddComponent<CanvasGroup>();
            var searchField = BuildTopSearchField(search);

            // 업무 알림: 읽지 않은 수 배지. 누르면 아래에 목록이 열린다.
            var notify = Place(Node("Notifications", bar), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-150, 0), new Vector2(84, 38));
            var notifyBg = Rounded(notify, theme.panelRaised, 8f);
            var notifyButton = MakeButton(notify, notifyBg, Color.white, new Color(1.25f, 1.25f, 1.25f, 1f));
            Text(Fill(Node("Label", notify)), theme.mediumFont, 14, theme.text, TextAlignmentOptions.Center, "top.notifications");
            var badge = Place(Node("Badge", notify), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-6, -6), new Vector2(22, 18));
            Rounded(badge, theme.accent, 9f);
            var badgeLabel = Text(Fill(Node("Label", badge)), theme.boldFont, 11, theme.panel, TextAlignmentOptions.Center);
            badge.gameObject.SetActive(false);

            // 현재 의뢰의 업무 상태와 진행 표시 (비율을 모르면 오가는 띠, 끝났으면 가득 찬 막대)
            var index = Place(Node("IndexStatus", bar), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-600, 0), new Vector2(300, 40));
            var indexLabel = Text(TopBand(Node("Label", index), 2, 20), theme.regularFont, 13, theme.subText, TextAlignmentOptions.MidlineRight);
            var track = Place(Node("Track", index), new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 6), new Vector2(160, 4));
            Rounded(track, theme.border, 2f);
            track.gameObject.AddComponent<RectMask2D>();
            var fill = Img(Fill(Node("Fill", track)), theme.accent, theme.roundedSprite);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            var sweep = Place(Node("Sweep", track), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(48, 4));
            Rounded(sweep, theme.accent, 2f);
            sweep.gameObject.SetActive(false);

            Wire(view, "caseStatus", caseStatus);
            Wire(view, "indexStatus", indexLabel);
            Wire(view, "indexFill", fill);
            Wire(view, "indeterminate", sweep);
            Wire(view, "searchField", searchField);
            Wire(view, "searchGroup", searchGroup);
            Wire(view, "notificationsButton", notifyButton);
            Wire(view, "notificationsBackground", notifyBg);
            Wire(view, "badge", badge.gameObject);
            Wire(view, "badgeLabel", badgeLabel);
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
            appRoot = TopBand(Node("Apps", left), 232, 300, 10, 10);
            Stack(appRoot, 4);
            Line(left, "RequestsDivider", true, 548, true, 0);
            Text(TopBand(Node("RequestsLabel", left), 564, 18, 18, 10), theme.mediumFont, 13, theme.subText, key: "side.requests");
            requestRoot = TopBand(Node("Requests", left), 590, 150, 10, 10);
            Stack(requestRoot, 4);
        }

        static WorkPanelView BuildWorkPanel(RectTransform right, InfoRowView infoRow)
        {
            Img(right, theme.panel);
            Line(right, "LeftLine", false, 0, true);
            var view = right.gameObject.AddComponent<WorkPanelView>();
            const float pad = 22;

            Text(TopBand(Node("Header", right), 18, 26, pad, pad), theme.boldFont, 18, theme.text, key: "panel.header");
            Line(right, "HeaderLine", true, 60, true);
            // 아래 영역 높이는 실행 중 WorkPanelView가 작업 기록 개수·접힘 상태에 맞춰 정한다. 여기 값은 초기값.
            const float bottomH = 160;

            // 위: 머리말(현재 열람·선택한 기록·작업 기록) → 기록 정보 → 핀 → 처리 후보 → 작업메모 (넘치면 스크롤)
            var recordArea = Fill(Node("Record", right), pad, 72, pad, 22 + bottomH + 10);
            // 돌아가기(`< 현재 화면` / `< 작업 기록`)는 패널 맨 위 왼쪽에 고정한다. 보일 때만 아래 스크롤 영역을 그만큼 내린다.
            var backButton = PanelBackButton(recordArea, "Back", out var backLabel);
            backLabel.gameObject.AddComponent<LocalizedText>().Key = UIKeys.PanelBackToCurrent;
            var recordScroll = Fill(Node("Scroll", recordArea));
            MakeScroll(recordScroll, out var content, 8, new RectOffset(0, 0, 0, 8));
            var headingRow = Node("HeadingRow", content);
            headingRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
            var heading = Text(Fill(Node("Heading", headingRow)), theme.mediumFont, 13, theme.subText);
            var title = Text(Node("RecordTitle", content), theme.boldFont, 19, theme.text, TextAlignmentOptions.TopLeft, null, true);
            var body = Text(Node("RecordBody", content), theme.regularFont, 14, theme.text, TextAlignmentOptions.TopLeft, null, true);
            var info = Node("Info", content);
            Stack(info, 0);

            // 보조 행동: 작업 기록을 띄웠을 때만 `원본 열기` (열 수 없으면 이유 한 줄)
            // 보조 행동: 원본 열기 · 비교에 추가/빼기 (작업 기록을 띄웠을 때만), 아래에 이유·안내 한 줄
            var tools = Node("RecordTools", content);
            Stack(tools, 4);
            var toolButtons = Node("Buttons", tools);
            toolButtons.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            var toolRow = toolButtons.gameObject.AddComponent<HorizontalLayoutGroup>();
            toolRow.spacing = 6;
            toolRow.childAlignment = TextAnchor.MiddleLeft;
            toolRow.childControlWidth = toolRow.childControlHeight = true;
            toolRow.childForceExpandWidth = false;
            toolRow.childForceExpandHeight = true;
            var sourceButton = ToolButton(toolButtons, "OpenSource", out var sourceLabel);
            var compareToggle = ToolButton(toolButtons, "CompareToggle", out var compareToggleLabel);
            var toolsNote = Text(Node("Note", tools), theme.regularFont, 13, theme.subText, TextAlignmentOptions.TopLeft, null, true);
            tools.gameObject.SetActive(false);

            // 의뢰 진행 작업 (의뢰 요청을 띄웠을 때만): 시작 버튼 + 업무 상태 한 줄. 작업 수만큼 원형을 복제한다.
            var jobs = Node("Jobs", content);
            Stack(jobs, 8).padding = new RectOffset(0, 0, 4, 0);
            var jobTemplate = Node("JobTemplate", jobs);
            Stack(jobTemplate, 4);
            var jobButtonRt = Node("Button", jobTemplate);
            jobButtonRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 38;
            ActionButton(jobButtonRt, out _, null, 14);
            Text(Node("Status", jobTemplate), theme.regularFont, 13, theme.subText, TextAlignmentOptions.TopLeft, null, true);
            jobTemplate.gameObject.SetActive(false);
            jobs.gameObject.SetActive(false);

            // 기본 행동(넓은 핀 버튼) → 처리 후보 라벨 → 보존·삭제 토글
            var actions = Node("Actions", content);
            Stack(actions, 8).padding = new RectOffset(0, 0, 6, 0);
            var pinRt = Node("Pin", actions);
            pinRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 42;
            var pin = ActionButton(pinRt, out var pinLabel, null, 15, theme.boldFont);
            Text(Node("ClassLabel", actions), theme.mediumFont, 13, theme.subText, key: "panel.classLabel")
                .gameObject.AddComponent<LayoutElement>().preferredHeight = 18;
            var classRow = Node("ClassRow", actions);
            classRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
            var toggles = classRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            toggles.spacing = 6;
            toggles.childControlWidth = toggles.childControlHeight = true;
            toggles.childForceExpandWidth = toggles.childForceExpandHeight = true;
            var keep = ActionButton(Node("Keep", classRow), out var keepLabel, "panel.action.keep", 14);
            var delete = ActionButton(Node("Delete", classRow), out var deleteLabel, "panel.action.delete", 14);
            var keepOn = ToggleOutline(keep);
            var deleteOn = ToggleOutline(delete);

            var memoRoot = Node("MemoRoot", content);
            Stack(memoRoot, 6).padding = new RectOffset(0, 0, 4, 0);
            Text(Node("MemoLabel", memoRoot), theme.mediumFont, 13, theme.subText, key: "panel.memo");
            var memoRt = Node("Memo", memoRoot);
            memoRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 84;
            var memo = BuildMemoField(memoRt);

            // 아래: 업무 진행 한 줄 · 작업 기록(접기/펼치기) · 비교(UI-06 전까지 비활성)
            var bottom = BottomBand(Node("WorkMemo", right), 22, bottomH, pad, pad);
            Line(bottom, "TopLine", true, 0, true);
            var tutorialRt = TopBand(Node("Tutorial", bottom), 12, 20);
            var tutorial = Text(tutorialRt, theme.mediumFont, 13, theme.accent);
            var headerRt = TopBand(Node("WorkListHeader", bottom), 38, 24);
            var header = MakeButton(headerRt, Img(headerRt, Color.clear), Color.white, Color.white);
            var headerLabel = Text(Fill(Node("Label", headerRt), 0, 0, 150, 0), theme.mediumFont, 13, theme.subText);
            var headerToggle = Text(Fill(Node("Toggle", headerRt)), theme.mediumFont, 13, theme.accent, TextAlignmentOptions.MidlineRight);
            // 머리말 오른쪽: `전체 보기`(우측 패널 전체를 작업 기록 목록으로) · 접기/펼치기
            var fullOpenRt = Place(Node("FullView", headerRt), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-60, 0), new Vector2(76, 24));
            var fullOpen = MakeButton(fullOpenRt, Rounded(fullOpenRt, theme.accentSoft, 6f), Color.white, new Color(1.25f, 1.25f, 1.25f, 1f));
            Text(Fill(Node("Label", fullOpenRt)), theme.mediumFont, 12, theme.accent, TextAlignmentOptions.Center, UIKeys.PanelFullViewOpen);
            var listArea = TopBand(Node("WorkList", bottom), 68, 120);
            MakeScroll(listArea, out var listContent, 6, new RectOffset(0, 0, 0, 4));
            var listEmpty = Text(TopBand(Node("WorkListEmpty", bottom), 68, 20, 2, 2), theme.regularFont, 14, theme.subText);

            // 비교할 기록 두 칸 (임시 선택). 위치는 실행 중 WorkPanelView가 정한다.
            var slotsLabel = Text(TopBand(Node("CompareSlotsLabel", bottom), 200, 18), theme.mediumFont, 13, theme.subText);
            var slotsRow = TopBand(Node("CompareSlots", bottom), 224, 30);
            var slotsNote = Text(TopBand(Node("CompareSlotsNote", bottom), 258, 16, 2, 2), theme.regularFont, 12, theme.subText);
            slotsNote.gameObject.SetActive(false);
            var slotButtons = new Button[2];
            var slotTitles = new TMP_Text[2];
            var slotMetas = new TMP_Text[2];
            var slotRemoves = new Button[2];
            for (int i = 0; i < 2; i++)
            {
                var slot = Node("Slot" + i, slotsRow);
                slot.anchorMin = new Vector2(i == 0 ? 0f : 0.5f, 0);
                slot.anchorMax = new Vector2(i == 0 ? 0.5f : 1f, 1);
                slot.offsetMin = new Vector2(i == 0 ? 0 : 3, 0);
                slot.offsetMax = new Vector2(i == 0 ? -3 : 0, 0);
                var slotBg = Rounded(slot, theme.panelRaised, 8f);
                var outline = Fill(Node("Outline", slot));
                Img(outline, theme.panelRaised, theme.selectionOutline, 8f).raycastTarget = false;
                slotButtons[i] = MakeButton(slot, slotBg, Color.white, new Color(1.25f, 1.25f, 1.25f, 1f));
                // 두 줄: 제목 / 의뢰 표시명 · 기록 코드 (필터로 목록에서 숨겨져도 칸에서 알아볼 수 있게)
                slotTitles[i] = Text(TopBand(Node("Title", slot), 4, 17, 10, 28), theme.mediumFont, 12, theme.text);
                slotMetas[i] = Text(TopBand(Node("Meta", slot), 21, 15, 10, 28), theme.monoFont, 11, theme.subText);
                var remove = Place(Node("Remove", slot), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-2, 0), new Vector2(26, 26));
                slotRemoves[i] = MakeButton(remove, Img(remove, Color.clear), Color.white, Color.white);
                Text(Fill(Node("Label", remove)), theme.mediumFont, 15, theme.subText, TextAlignmentOptions.Center);
            }

            var compare = BottomBand(Node("Compare", bottom), 0, 44, 0, 0);
            var compareGroup = compare.gameObject.AddComponent<CanvasGroup>();
            compareGroup.alpha = 0.4f;
            compareGroup.interactable = false;
            compareGroup.blocksRaycasts = false;
            var compareButton = MakeButton(compare, Rounded(compare, theme.accentSoft, 8f), Color.white, new Color(1.25f, 1.25f, 1.25f, 1f));
            Text(Fill(Node("Label", compare)), theme.boldFont, 15, theme.accent, TextAlignmentOptions.Center, "panel.compare");

            var full = BuildFullList(right, pad, bottomH, view);

            // 키보드 포커스 테두리 (키보드로 조작할 때만 보이며, 위치는 실행 중 WorkPanelView가 맞춘다)
            var ring = Node("FocusRing", right);
            Img(ring, theme.accent, theme.selectionOutline, 8f).raycastTarget = false;
            ring.gameObject.SetActive(false);

            Wire(view, "focusRing", ring);
            Wire(view, "fullArea", full);
            Wire(view, "fullViewButton", fullOpen);
            WireArray(view, "slotMetas", slotMetas);
            Wire(view, "backLinkLabel", backLabel);
            Wire(view, "recordScroll", recordScroll);
            Wire(view, "heading", heading);
            Wire(view, "backLink", backButton);
            Wire(view, "recordTitle", title);
            Wire(view, "recordBody", body);
            Wire(view, "infoRoot", info);
            Wire(view, "rowPrefab", infoRow);
            Wire(view, "toolsRoot", tools.gameObject);
            Wire(view, "sourceButton", sourceButton);
            Wire(view, "sourceLabel", sourceLabel);
            Wire(view, "compareToggle", compareToggle);
            Wire(view, "compareToggleLabel", compareToggleLabel);
            Wire(view, "toolsNote", toolsNote);
            Wire(view, "jobsRoot", jobs);
            Wire(view, "jobTemplate", jobTemplate.gameObject);
            Wire(view, "slotsLabel", slotsLabel);
            Wire(view, "slotsRow", slotsRow);
            Wire(view, "slotsNote", slotsNote);
            WireArray(view, "slotButtons", slotButtons);
            WireArray(view, "slotTitles", slotTitles);
            WireArray(view, "slotRemoveButtons", slotRemoves);
            Wire(view, "compareButton", compareButton);
            Wire(view, "compareGroup", compareGroup);
            Wire(view, "actionsRoot", actions.gameObject);
            Wire(view, "pinButton", pin);
            Wire(view, "pinLabel", pinLabel);
            Wire(view, "keepButton", keep);
            Wire(view, "keepLabel", keepLabel);
            Wire(view, "deleteButton", delete);
            Wire(view, "deleteLabel", deleteLabel);
            Wire(view, "keepOn", keepOn);
            Wire(view, "deleteOn", deleteOn);
            Wire(view, "memoRoot", memoRoot.gameObject);
            Wire(view, "memoField", memo);
            Wire(view, "recordArea", recordArea);
            Wire(view, "bottomArea", bottom);
            Wire(view, "tutorialRect", tutorialRt);
            Wire(view, "tutorialLine", tutorial);
            Wire(view, "listHeader", header);
            Wire(view, "listHeaderLabel", headerLabel);
            Wire(view, "listToggleLabel", headerToggle);
            Wire(view, "listViewport", listArea);
            Wire(view, "listRoot", listContent);
            Wire(view, "itemPrefab", pinnedPrefab);
            Wire(view, "listEmpty", listEmpty);
            Wire(view, "compareRect", compare);
            return view;
        }

        /// <summary>
        /// 작업 기록 전체 보기. 기록 정보 영역과 같은 자리(아래 고정 영역 위)를 채운다.
        /// 머리말(제목 · 개수, `< 현재 화면`) → 상태 필터 한 줄 → 의뢰 드롭다운 → 독립 스크롤 목록.
        /// 필터 이름·의뢰 이름은 실행 중 문자열·데이터에서 채운다.
        /// </summary>
        static RectTransform BuildFullList(RectTransform right, float pad, float bottomH, WorkPanelView view)
        {
            var full = Fill(Node("FullList", right), pad, 72, pad, 22 + bottomH + 10);

            // 맨 위 왼쪽 `< 현재 화면` → 제목 · 개수 → 필터 → 의뢰 → 목록
            var closeButton = PanelBackButton(full, "Close", out var closeLabel);
            var titleRow = TopBand(Node("TitleRow", full), PanelBackHeight + 10, 26);
            var titleGroup = Fill(Node("Title", titleRow));
            var titleLayout = titleGroup.gameObject.AddComponent<HorizontalLayoutGroup>();
            titleLayout.spacing = 8;
            titleLayout.childAlignment = TextAnchor.MiddleLeft;
            titleLayout.childControlWidth = titleLayout.childControlHeight = true;
            titleLayout.childForceExpandWidth = false;
            titleLayout.childForceExpandHeight = true;
            var title = Text(Node("Label", titleGroup), theme.boldFont, 17, theme.text);
            var count = Text(Node("Count", titleGroup), theme.monoFont, 13, theme.subText);
            const float top = PanelBackHeight + 10 + 26;

            // 상태 필터 한 줄 (전체·핀·보존·삭제·메모). 한 번에 하나만 켜진다.
            var filters = TopBand(Node("Filters", full), top + 12, 32);
            var filterRow = filters.gameObject.AddComponent<HorizontalLayoutGroup>();
            filterRow.spacing = 4;
            filterRow.childControlWidth = filterRow.childControlHeight = true;
            filterRow.childForceExpandWidth = filterRow.childForceExpandHeight = true;
            const int filterCount = 5;
            var filterButtons = new Button[filterCount];
            var filterLabels = new TMP_Text[filterCount];
            var filterOn = new GameObject[filterCount];
            for (int i = 0; i < filterCount; i++)
            {
                filterButtons[i] = ActionButton(Node("Filter" + i, filters), out filterLabels[i], null, 13);
                filterOn[i] = ToggleOutline(filterButtons[i]);
            }

            // 의뢰 필터: `모든 의뢰` + 데이터의 의뢰 표시명
            var caseFilter = BuildDropdown(TopBand(Node("CaseFilter", full), top + 50, 32));

            var listArea = Fill(Node("List", full), 0, top + 92, 0, 0);
            var scroll = MakeScroll(listArea, out var listContent, 6, new RectOffset(0, 0, 0, 4));
            var empty = Text(TopBand(Node("Empty", full), top + 96, 20, 2, 2), theme.regularFont, 14, theme.subText);

            Wire(view, "fullTitle", title);
            Wire(view, "fullCount", count);
            Wire(view, "fullBack", closeButton);
            Wire(view, "fullBackLabel", closeLabel);
            WireArray(view, "filterButtons", filterButtons);
            WireArray(view, "filterLabels", filterLabels);
            WireArray(view, "filterOn", filterOn);
            Wire(view, "caseFilter", caseFilter);
            Wire(view, "fullScroll", scroll);
            Wire(view, "fullListRoot", listContent);
            Wire(view, "fullEmpty", empty);
            full.gameObject.SetActive(false);
            return full;
        }

        /// <summary>업무 프로그램 톤의 드롭다운 (TMP_Dropdown 템플릿 구조: Template/Viewport/Content/Item).</summary>
        static TMP_Dropdown BuildDropdown(RectTransform rt)
        {
            const float itemH = 30;
            var bg = Rounded(rt, theme.panelRaised, 8f);
            bg.raycastTarget = true;
            var caption = Text(Fill(Node("Label", rt), 12, 0, 30, 0), theme.mediumFont, 13, theme.text);
            Text(Place(Node("Arrow", rt), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(16, 16)),
                theme.regularFont, 9, theme.subText, TextAlignmentOptions.Center, UIKeys.DropdownArrow);

            var template = Node("Template", rt);
            template.anchorMin = new Vector2(0, 0);
            template.anchorMax = new Vector2(1, 0);
            template.pivot = new Vector2(0.5f, 1);
            template.anchoredPosition = new Vector2(0, -2);
            template.sizeDelta = new Vector2(0, itemH * 4 + 8);
            Rounded(template, theme.panelRaised, 8f).raycastTarget = true;
            var outline = Fill(Node("Outline", template));
            Img(outline, theme.border, theme.selectionOutline, 8f).raycastTarget = false;
            var templateScroll = template.gameObject.AddComponent<ScrollRect>();
            templateScroll.horizontal = false;
            templateScroll.movementType = ScrollRect.MovementType.Clamped;
            templateScroll.scrollSensitivity = 30f;
            var viewport = Fill(Node("Viewport", template), 4, 4, 4, 4);
            viewport.gameObject.AddComponent<RectMask2D>();
            Img(viewport, new Color(0, 0, 0, 0)).raycastTarget = true;
            var content = Node("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(0, itemH);
            templateScroll.viewport = viewport;
            templateScroll.content = content;

            var item = TopBand(Node("Item", content), 0, itemH);
            var itemBg = Rounded(Fill(Node("Item Background", item)), theme.panelRaised, 6f);
            itemBg.raycastTarget = true;
            var check = Place(Node("Item Checkmark", item), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(14, 14));
            var checkImg = Img(check, theme.accent, theme.selectionCheck);
            checkImg.preserveAspect = true;
            var itemLabel = Text(Fill(Node("Item Label", item), 30, 0, 8, 0), theme.mediumFont, 13, theme.text);
            var toggle = item.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = itemBg;
            toggle.graphic = checkImg;
            toggle.isOn = true;
            var colors = toggle.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = colors.selectedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(1.4f, 1.4f, 1.4f, 1f);
            toggle.colors = colors;
            template.gameObject.SetActive(false);

            var dropdown = rt.gameObject.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = bg;
            dropdown.template = template;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            var ddColors = dropdown.colors;
            ddColors.normalColor = Color.white;
            ddColors.highlightedColor = ddColors.selectedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            ddColors.pressedColor = new Color(1.4f, 1.4f, 1.4f, 1f);
            dropdown.colors = ddColors;
            dropdown.options.Clear();
            return dropdown;
        }

        static Button ActionButton(RectTransform rt, out TMP_Text label, string key, float size, TMP_FontAsset font = null)
        {
            var bg = Rounded(rt, theme.panelRaised, 8f);
            var button = MakeButton(rt, bg, Color.white, new Color(1.25f, 1.25f, 1.25f, 1f));
            label = Text(Fill(Node("Label", rt), 4, 0, 4, 0), font ?? theme.mediumFont, size, theme.text, TextAlignmentOptions.Center, key);
            return button;
        }

        const float PanelBackHeight = 32;

        /// <summary>
        /// 우측 패널 맨 위 왼쪽의 돌아가기 버튼 (`< 현재 화면` / `< 작업 기록`). 휴대전화 앱의 뒤로처럼 늘 같은 자리·크기로 둔다.
        /// 문구는 실행 중 상황에 맞춰 채운다.
        /// </summary>
        static Button PanelBackButton(RectTransform parent, string name, out TMP_Text label)
        {
            var rt = Place(Node(name, parent), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(132, PanelBackHeight));
            var button = MakeButton(rt, Rounded(rt, theme.accentSoft, 8f), Color.white, new Color(1.25f, 1.25f, 1.25f, 1f));
            label = Text(Fill(Node("Label", rt), 10, 0, 10, 0), theme.boldFont, 14, theme.accent, TextAlignmentOptions.Center);
            return button;
        }

        /// <summary>우측 패널의 작은 보조 행동 버튼 (원본 열기·비교에 추가).</summary>
        static Button ToolButton(RectTransform parent, string name, out TMP_Text label)
        {
            var rt = Node(name, parent);
            rt.gameObject.AddComponent<LayoutElement>().preferredWidth = 140;
            var button = MakeButton(rt, Rounded(rt, theme.panelRaised, 8f), Color.white, new Color(1.25f, 1.25f, 1.25f, 1f));
            label = Text(Fill(Node("Label", rt), 8, 0, 8, 0), theme.mediumFont, 13, theme.accent, TextAlignmentOptions.Center);
            return button;
        }

        static void WireArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>처리 후보 토글이 켜졌을 때 보이는 테두리 (평소에는 꺼짐).</summary>
        static GameObject ToggleOutline(Button button)
        {
            var rt = Fill(Node("OnOutline", (RectTransform)button.transform));
            Img(rt, theme.accent, theme.selectionOutline, 8f).raycastTarget = false;
            rt.gameObject.SetActive(false);
            return rt.gameObject;
        }

        /// <summary>상단 전역 검색 입력창 (한 줄).</summary>
        static TMP_InputField BuildTopSearchField(RectTransform rt)
        {
            var bg = Rounded(rt, theme.panelRaised, 8f);
            bg.raycastTarget = true;
            var area = Fill(Node("TextArea", rt), 14, 0, 12, 0);
            area.gameObject.AddComponent<RectMask2D>();
            var placeholder = Text(Fill(Node("Placeholder", area)), theme.regularFont, 14, theme.subText,
                TextAlignmentOptions.MidlineLeft, "top.searchPlaceholder");
            var input = Text(Fill(Node("Text", area)), theme.regularFont, 14, theme.text, TextAlignmentOptions.MidlineLeft);
            input.richText = false;
            var field = rt.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textViewport = area;
            field.textComponent = input;
            field.placeholder = placeholder;
            field.fontAsset = theme.regularFont;
            field.pointSize = 14;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 60;
            field.richText = false;
            field.restoreOriginalTextOnEscape = false;
            field.customCaretColor = true;
            field.caretColor = theme.accent;
            field.selectionColor = new Color(theme.accent.r, theme.accent.g, theme.accent.b, 0.35f);
            return field;
        }

        /// <summary>
        /// 전역 검색 결과 화면. 휴대전화 자리(중앙)를 덮는 PC 업무 화면으로, 의뢰 · 앱별 구역과 결과 행을 독립 스크롤로 보여준다.
        /// </summary>
        static SearchView BuildSearch(RectTransform center)
        {
            var root = Fill(Node("Search", center));
            Img(root, theme.stage).raycastTarget = true;
            var view = root.gameObject.AddComponent<SearchView>();
            var header = TopBand(Node("Header", root), 0, 60);
            Line(header, "BottomLine", true, 0, false);
            var titleGroup = Fill(Node("Title", header), 24, 0, 160, 0);
            var titleLayout = titleGroup.gameObject.AddComponent<HorizontalLayoutGroup>();
            titleLayout.spacing = 10;
            titleLayout.childAlignment = TextAnchor.MiddleLeft;
            titleLayout.childControlWidth = titleLayout.childControlHeight = true;
            titleLayout.childForceExpandWidth = false;
            titleLayout.childForceExpandHeight = true;
            var heading = Text(Node("Label", titleGroup), theme.boldFont, 17, theme.text);
            var count = Text(Node("Count", titleGroup), theme.monoFont, 13, theme.subText);
            var close = Place(Node("Close", header), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(110, 36));
            var closeButton = MakeButton(close, Rounded(close, theme.panelRaised, 8f), Color.white, new Color(1.25f, 1.25f, 1.25f, 1f));
            Text(Fill(Node("Label", close)), theme.mediumFont, 14, theme.text, TextAlignmentOptions.Center, UIKeys.SearchClose);

            var listArea = Fill(Node("Results", root), 24, 72, 24, 20);
            var scroll = MakeScroll(listArea, out var content, 6, new RectOffset(0, 0, 0, 12));
            var empty = Text(TopBand(Node("Empty", root), 84, 22, 26, 26), theme.regularFont, 15, theme.subText);

            Wire(view, "heading", heading);
            Wire(view, "countLabel", count);
            Wire(view, "closeButton", closeButton);
            Wire(view, "scroll", scroll);
            Wire(view, "listRoot", content);
            Wire(view, "emptyLabel", empty);
            Wire(view, "rowPrefab", BuildSearchRow());
            Wire(view, "sectionPrefab", BuildSearchSection());
            root.gameObject.SetActive(false);
            return view;
        }

        static SearchResultRowView BuildSearchRow()
        {
            var root = Node("SearchResultRow", null);
            root.sizeDelta = new Vector2(900, 76);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 76;
            var bg = Rounded(root, theme.panel, 8f);
            var button = MakeButton(root, bg, Color.white, new Color(1.25f, 1.25f, 1.25f, 1f));
            var view = root.gameObject.AddComponent<SearchResultRowView>();
            var title = Text(TopBand(Node("Title", root), 10, 22, 16, 16), theme.boldFont, 15, theme.text);
            title.richText = false;
            var snippet = Text(TopBand(Node("Snippet", root), 33, 20, 16, 16), theme.regularFont, 14, theme.text);
            snippet.richText = true;
            var meta = Text(TopBand(Node("Meta", root), 54, 16, 16, 16), theme.regularFont, 12, theme.subText);
            meta.richText = false;
            Wire(view, "button", button);
            Wire(view, "title", title);
            Wire(view, "snippet", snippet);
            Wire(view, "meta", meta);
            return SavePrefab<SearchResultRowView>(root, "SearchResultRow");
        }

        static ListSectionView BuildSearchSection()
        {
            var root = Node("SearchSection", null);
            root.sizeDelta = new Vector2(900, 34);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;
            var view = root.gameObject.AddComponent<ListSectionView>();
            var label = Text(Fill(Node("Label", root), 4, 10, 4, 0), theme.mediumFont, 13, theme.accent, TextAlignmentOptions.BottomLeft);
            label.richText = false;
            Wire(view, "label", label);
            return SavePrefab<ListSectionView>(root, "SearchSection");
        }

        /// <summary>
        /// 업무 알림 목록. 상단 `알림` 아래에 열리며, 바깥을 누르면 닫힌다. 알림을 눌러도 앱·기록을 열지 않는다.
        /// </summary>
        static NotificationPanelView BuildNotifications(RectTransform root, float topH)
        {
            var panelRoot = Fill(Node("NotificationPanel", root));
            var view = panelRoot.gameObject.AddComponent<NotificationPanelView>();
            var blockerImg = Img(panelRoot, Color.clear);
            var blocker = MakeButton(panelRoot, blockerImg, Color.white, Color.white);
            var card = Place(Node("Card", panelRoot), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-22, -(topH + 6)), new Vector2(440, 400));
            Rounded(card, theme.panelRaised, 10f).raycastTarget = true;
            Img(Fill(Node("Outline", card)), theme.border, theme.selectionOutline, 10f).raycastTarget = false;
            var heading = Text(TopBand(Node("Title", card), 14, 24, 18, 140), theme.boldFont, 16, theme.text);
            var markAll = Place(Node("MarkAll", card), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -12), new Vector2(110, 28));
            var markAllButton = MakeButton(markAll, Rounded(markAll, theme.accentSoft, 6f), Color.white, new Color(1.25f, 1.25f, 1.25f, 1f));
            var markAllLabel = Text(Fill(Node("Label", markAll)), theme.mediumFont, 13, theme.accent, TextAlignmentOptions.Center);
            var listArea = Fill(Node("List", card), 10, 52, 10, 10);
            MakeScroll(listArea, out var content, 4, new RectOffset(0, 0, 0, 4));
            var empty = Text(TopBand(Node("Empty", card), 58, 22, 18, 18), theme.regularFont, 14, theme.subText);

            Wire(view, "card", card);
            Wire(view, "heading", heading);
            Wire(view, "markAllButton", markAllButton);
            Wire(view, "markAllLabel", markAllLabel);
            Wire(view, "blocker", blocker);
            Wire(view, "listRoot", content);
            Wire(view, "emptyLabel", empty);
            Wire(view, "rowPrefab", BuildNotificationRow());
            panelRoot.gameObject.SetActive(false);
            return view;
        }

        static NotificationRowView BuildNotificationRow()
        {
            var root = Node("NotificationRow", null);
            root.sizeDelta = new Vector2(420, 48);
            root.gameObject.AddComponent<LayoutElement>().minHeight = 48;
            var bg = Rounded(root, theme.panel, 8f);
            var button = MakeButton(root, bg, Color.white, new Color(1.25f, 1.25f, 1.25f, 1f));
            var view = root.gameObject.AddComponent<NotificationRowView>();
            var dot = Place(Node("Unread", root), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(16, 0), new Vector2(8, 8));
            Img(dot, theme.accent, theme.circleSprite);
            var label = Text(Fill(Node("Label", root), 30, 6, 14, 6), theme.mediumFont, 14, theme.text, TextAlignmentOptions.MidlineLeft, null, true);
            label.richText = false;
            Wire(view, "button", button);
            Wire(view, "unreadDot", dot.gameObject);
            Wire(view, "label", label);
            return SavePrefab<NotificationRowView>(root, "NotificationRow");
        }

        /// <summary>선택한 기록의 작업메모 (여러 줄 입력).</summary>
        static TMP_InputField BuildMemoField(RectTransform rt)
        {
            var bg = Rounded(rt, theme.panelRaised, 8f);
            bg.raycastTarget = true;
            var area = Fill(Node("TextArea", rt), 12, 10, 12, 10);
            area.gameObject.AddComponent<RectMask2D>();
            var placeholder = Text(Fill(Node("Placeholder", area)), theme.regularFont, 14, theme.subText,
                TextAlignmentOptions.TopLeft, "panel.memoPlaceholder", true);
            var input = Text(Fill(Node("Text", area)), theme.regularFont, 14, theme.text, TextAlignmentOptions.TopLeft, null, true);
            var field = rt.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textViewport = area;
            field.textComponent = input;
            field.placeholder = placeholder;
            field.fontAsset = theme.regularFont;
            field.pointSize = 14;
            field.lineType = TMP_InputField.LineType.MultiLineNewline;
            field.characterLimit = 400;
            field.restoreOriginalTextOnEscape = false;
            field.customCaretColor = true;
            field.caretColor = theme.accent;
            field.selectionColor = new Color(theme.accent.r, theme.accent.g, theme.accent.b, 0.35f);
            return field;
        }

        static PinnedItemView BuildPinnedItem()
        {
            var root = Node("PinnedItem", null);
            root.sizeDelta = new Vector2(316, 56);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;
            var bg = Rounded(root, theme.panelRaised, 8f);
            var button = MakeButton(root, bg, Color.white, new Color(1.25f, 1.25f, 1.25f, 1f));
            var view = root.gameObject.AddComponent<PinnedItemView>();
            // 핀 아이콘 자리는 항상 비워 두어 핀 여부와 관계없이 제목 시작 위치를 맞춘다.
            var pinIcon = Place(Node("PinIcon", root), new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -13), new Vector2(14, 14));
            Img(pinIcon, theme.pinMarkColor, theme.pinGlyph).preserveAspect = true;
            // 오른쪽: 비교 버튼(`+ 비교`/`비교 중`, 핀한 기록만), 그 왼쪽 위에 보존·삭제 배지
            var title = Text(TopBand(Node("Title", root), 9, 22, 32, 124), theme.mediumFont, 14, theme.text);
            var meta = Text(TopBand(Node("Meta", root), 31, 18, 32, 124), theme.monoFont, 11, theme.subText);
            var badge = Text(Place(Node("Badge", root), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-78, -9), new Vector2(44, 22)),
                theme.mediumFont, 12, theme.accent, TextAlignmentOptions.MidlineRight);
            // 작업메모가 있으면 배지 아래에 `메모`
            var memoMark = Text(Place(Node("MemoMark", root), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-78, -31), new Vector2(44, 18)),
                theme.mediumFont, 11, theme.subText, TextAlignmentOptions.MidlineRight);
            var compareRt = Place(Node("Compare", root), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(62, 28));
            var compareGroup = compareRt.gameObject.AddComponent<CanvasGroup>();
            var compareBg = Rounded(compareRt, theme.panel, 8f);
            var compareButton = MakeButton(compareRt, compareBg, Color.white, new Color(1.25f, 1.25f, 1.25f, 1f));
            var compareLabel = Text(Fill(Node("Label", compareRt), 4, 0, 4, 0), theme.mediumFont, 12, theme.accent, TextAlignmentOptions.Center);
            Wire(view, "button", button);
            Wire(view, "background", bg);
            Wire(view, "pinIcon", pinIcon.gameObject);
            Wire(view, "compareButton", compareButton);
            Wire(view, "compareBackground", compareBg);
            Wire(view, "compareLabel", compareLabel);
            Wire(view, "compareGroup", compareGroup);
            Wire(view, "title", title);
            Wire(view, "meta", meta);
            Wire(view, "badge", badge);
            Wire(view, "memoMark", memoMark);
            return SavePrefab<PinnedItemView>(root, "PinnedItem");
        }

        static PhoneView BuildCenter(RectTransform center, ThreadRowView threadRow, BubbleRowView bubbleRow,
            DateSeparatorView separator, ListSectionView section, out TMP_Text deviceLabel)
        {
            Img(center, theme.stage);
            var toolbar = TopBand(Node("Toolbar", center), 0, 60);
            Line(toolbar, "BottomLine", true, 0, false);
            var deviceInfo = Fill(Node("DeviceInfo", toolbar), 24, 0, 400, 0);
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
            // 기록 고르기 시작·완료 (단축키 S). 문구는 실행 중 상태에 따라 바뀐다.
            var select = Place(Node("SelectRecord", toolbar), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(110, 36));
            var selectBg = Rounded(select, theme.panelRaised, 8f);
            var selectButton = MakeButton(select, selectBg, Color.white, new Color(1.25f, 1.25f, 1.25f, 1f));
            var selectLabel = Text(Fill(Node("Label", select)), theme.mediumFont, 14, theme.text, TextAlignmentOptions.Center);
            // 고르기 중 안내 또는 고른 기록 종류 ("고를 기록을 누르세요" / "메시지 선택됨")
            // 작업 기록 전체 보기 진입 (`작업 기록 {count}`, 전체 보기 중에는 켜진 상태로 표시). 문구·개수는 실행 중 채운다.
            var records = Place(Node("WorkRecords", toolbar), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-140, 0), new Vector2(124, 36));
            var recordsBg = Rounded(records, theme.panelRaised, 8f);
            var recordsButton = MakeButton(records, recordsBg, Color.white, new Color(1.25f, 1.25f, 1.25f, 1f));
            var recordsLabel = Text(Fill(Node("Label", records), 8, 0, 8, 0), theme.mediumFont, 14, theme.text, TextAlignmentOptions.Center);
            toolbarRecords = (recordsButton, recordsLabel, recordsBg);
            var selectStatus = Text(Place(Node("SelectStatus", toolbar), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-274, 0), new Vector2(240, 30)),
                theme.mediumFont, 14, theme.accent, TextAlignmentOptions.MidlineRight);
            toolbarSelect = (selectButton, selectLabel, selectBg, selectStatus);

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
            var recordList = BuildRecordList(appArea);
            var recordDetail = BuildRecordDetail(appArea);

            var home = Place(Node("HomeIndicator", screen), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(128, 5));
            Rounded(home, new Color(1, 1, 1, 0.75f), 2.5f);

            Wire(view, "clockLabel", clock);
            Wire(view, "messageList", list);
            Wire(view, "chat", chat);
            Wire(view, "albumList", albumList);
            Wire(view, "photoGrid", photoGrid);
            Wire(view, "photoDetail", photoDetail);
            Wire(view, "recordList", recordList);
            Wire(view, "recordDetail", recordDetail);
            return view;
        }

        /// <summary>
        /// 비교 모드 화면 (UI-06). 중앙 영역 전체를 덮는 PC 업무 화면으로, 휴대전화 프레임 대신
        /// 두 기록을 같은 크기의 좌우 패널로 나란히 둔다. 평소에는 꺼져 있다.
        /// </summary>
        static CompareView BuildCompare(RectTransform center, BubbleRowView bubbleRow, DateSeparatorView separator, InfoRowView infoRow)
        {
            var root = Fill(Node("Compare", center));
            Img(root, theme.stage).raycastTarget = true;
            var view = root.gameObject.AddComponent<CompareView>();
            var header = TopBand(Node("Header", root), 0, 60);
            Line(header, "BottomLine", true, 0, false);
            Text(Fill(Node("Title", header), 24, 0, 200, 0), theme.boldFont, 17, theme.text, key: "compare.title");
            var close = Place(Node("Close", header), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(110, 36));
            var closeButton = MakeButton(close, Rounded(close, theme.panelRaised, 8f), Color.white, new Color(1.25f, 1.25f, 1.25f, 1f));
            Text(Fill(Node("Label", close)), theme.mediumFont, 14, theme.text, TextAlignmentOptions.Center, "compare.close");

            Wire(view, "closeButton", closeButton);
            Wire(view, "left", BuildComparePane(root, "Left", true, bubbleRow, separator, infoRow));
            Wire(view, "right", BuildComparePane(root, "Right", false, bubbleRow, separator, infoRow));
            root.gameObject.SetActive(false);
            return view;
        }

        /// <summary>
        /// 비교 화면 한쪽. 위 고정 영역에 기록 정보, 아래에 원본 표현(휴대전화와 같은 대화·사진·상세 화면을 읽기 전용으로,
        /// 의뢰 요청은 업무 카드). 패널 크기는 앵커로 고정되어 내용 길이와 무관하고, 내용은 패널 안에서만 스크롤한다.
        /// </summary>
        static ComparePaneView BuildComparePane(RectTransform root, string name, bool isLeft, BubbleRowView bubbleRow,
            DateSeparatorView separator, InfoRowView infoRow)
        {
            var pane = Node(name, root);
            pane.anchorMin = new Vector2(isLeft ? 0f : 0.5f, 0f);
            pane.anchorMax = new Vector2(isLeft ? 0.5f : 1f, 1f);
            pane.offsetMin = new Vector2(isLeft ? 24 : 10, 24);
            pane.offsetMax = new Vector2(isLeft ? -10 : -24, -84);
            Rounded(pane, theme.panel, 12f);
            var view = pane.gameObject.AddComponent<ComparePaneView>();
            var owner = Text(TopBand(Node("Owner", pane), 16, 28, 20, 20), theme.boldFont, 19, theme.text);
            var info = TopBand(Node("Info", pane), 50, 132, 20, 20);
            Stack(info, 0);

            const float contentTop = 194;
            var screen = Fill(Node("Screen", pane), 20, contentTop, 20, 20);
            Rounded(screen, theme.phoneScreen, 16f);
            screen.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var chat = BuildChat(screen, bubbleRow, separator);
            var photo = BuildPhotoDetail(screen);
            var detail = BuildRecordDetail(screen);
            // 읽기 전용: 뒤로가기·앞뒤 사진 이동은 두지 않는다.
            foreach (var path in new[] { "Chat/Header/Back", "PhotoDetail/Header/Back", "PhotoDetail/BottomBar", "RecordDetail/Header/Back" })
                screen.Find(path).gameObject.SetActive(false);

            var card = Fill(Node("Card", pane), 20, contentTop, 20, 20);
            Rounded(card, theme.panelRaised, 12f);
            var cardScroll = MakeScroll(card, out var cardContent, 12, new RectOffset(20, 20, 18, 20));
            var cardTitle = Text(Node("Title", cardContent), theme.boldFont, 19, theme.text, TextAlignmentOptions.TopLeft, null, true);
            var cardBody = Text(Node("Body", cardContent), theme.regularFont, 15, theme.text, TextAlignmentOptions.TopLeft, null, true);
            card.gameObject.SetActive(false);

            Wire(view, "ownerLabel", owner);
            Wire(view, "infoRoot", info);
            Wire(view, "rowPrefab", infoRow);
            Wire(view, "screenRoot", screen.gameObject);
            Wire(view, "chat", chat);
            Wire(view, "photo", photo);
            Wire(view, "detail", detail);
            Wire(view, "cardRoot", card.gameObject);
            Wire(view, "cardScroll", cardScroll);
            Wire(view, "cardTitle", cardTitle);
            Wire(view, "cardBody", cardBody);
            return view;
        }

        /// <summary>브라우저·지도·파일·설정 앱이 함께 쓰는 목록 화면.</summary>
        static PhoneListView BuildRecordList(RectTransform area)
        {
            var rt = Fill(Node("RecordList", area));
            var view = rt.gameObject.AddComponent<PhoneListView>();
            var rootTitle = Text(TopBand(Node("Title", rt), 4, 44, 18, 18), theme.boldFont, theme.phoneTitleSize, theme.phoneText);
            var header = TopBand(Node("Header", rt), 0, 52);
            var back = BackButton(header, out var backLabel);
            var headerTitle = Text(TopBand(Node("Title", header), 12, 26, 140, 140), theme.boldFont, 17, theme.phoneText, TextAlignmentOptions.Center);
            var scrollArea = Fill(Node("Scroll", rt), 0, 56, 0, 22);
            var scroll = MakeScroll(scrollArea, out var content, 0, new RectOffset(0, 0, 0, 12));
            var empty = Text(Fill(Node("Empty", rt), 20, 140, 20, 40), theme.regularFont, 16, theme.phoneSubText, TextAlignmentOptions.Top);

            Wire(view, "rootTitle", rootTitle);
            Wire(view, "header", header.gameObject);
            Wire(view, "backButton", back);
            Wire(view, "backLabel", backLabel);
            Wire(view, "headerTitle", headerTitle);
            Wire(view, "scroll", scroll);
            Wire(view, "content", content);
            Wire(view, "rowPrefab", recordPrefabs.row);
            Wire(view, "sectionPrefab", recordPrefabs.section);
            Wire(view, "emptyLabel", empty);
            rt.gameObject.SetActive(false);
            return view;
        }

        /// <summary>브라우저·지도·파일·설정 앱이 함께 쓰는 상세 화면.</summary>
        static PhoneDetailView BuildRecordDetail(RectTransform area)
        {
            var rt = Fill(Node("RecordDetail", area));
            var view = rt.gameObject.AddComponent<PhoneDetailView>();
            var header = TopBand(Node("Header", rt), 0, 52);
            var back = BackButton(header, out var backLabel);
            var headerTitle = Text(TopBand(Node("Title", header), 12, 26, 140, 140), theme.boldFont, 17, theme.phoneText, TextAlignmentOptions.Center);
            var scrollArea = Fill(Node("Scroll", rt), 0, 56, 0, 22);
            var scroll = MakeScroll(scrollArea, out var content, 14, new RectOffset(16, 16, 6, 16));

            var hero = Node("Hero", content);
            hero.gameObject.AddComponent<LayoutElement>().preferredHeight = 220;
            var heroBg = Rounded(hero, theme.photoPlaceholder, 12f);
            heroBg.type = Image.Type.Simple;
            hero.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var heroTex = Fill(Node("Texture", hero)).gameObject.AddComponent<RawImage>();
            heroTex.raycastTarget = false;
            var glyph = Img(Place(Node("Glyph", hero), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(48, 48)),
                theme.phoneAccent, theme.pinGlyph);
            glyph.preserveAspect = true;
            var message = Text(Place(Node("Message", hero), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(320, 24)),
                theme.regularFont, 14, theme.phoneSubText, TextAlignmentOptions.Center);

            var headlineRt = Node("Headline", content);
            var headline = Text(headlineRt, theme.boldFont, 21, theme.phoneText, TextAlignmentOptions.TopLeft, null, true);

            var fields = Node("Fields", content);
            Stack(fields, 0);

            Wire(view, "backButton", back);
            Wire(view, "backLabel", backLabel);
            Wire(view, "headerTitle", headerTitle);
            Wire(view, "scroll", scroll);
            Wire(view, "content", content);
            Wire(view, "heroRoot", hero.gameObject);
            Wire(view, "heroBackground", heroBg);
            Wire(view, "heroTexture", heroTex);
            Wire(view, "heroGlyph", glyph);
            Wire(view, "heroMessage", message);
            Wire(view, "headline", headline);
            Wire(view, "fieldRoot", fields);
            Wire(view, "fieldPrefab", recordPrefabs.field);
            // 열린 기록 전체(페이지·장소·파일·설정 변경)를 선택 모드에서 화면 영역으로 선택한다.
            var viewport = scroll.viewport;
            var pageButton = viewport.gameObject.AddComponent<Button>();
            pageButton.transition = Selectable.Transition.None;
            pageButton.targetGraphic = viewport.GetComponent<Image>();
            Wire(view, "selectable", AddSelection(viewport, pageButton, true, 2f));
            rt.gameObject.SetActive(false);
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
            var stageTarget = Img(stage, new Color(0, 0, 0, 0));
            var stageButton = stage.gameObject.AddComponent<Button>();
            stageButton.transition = Selectable.Transition.None;
            stageButton.targetGraphic = stageTarget;
            Wire(view, "selectable", AddSelection(stage, stageButton, true, 2f));
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
            // 머리말 가운데(상대 프로필·이름)를 선택 모드에서 누르면 대화방 전체가 선택된다.
            var headerArea = Place(Node("ThreadSelect", header), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -2), new Vector2(200, 84));
            var headerTarget = Img(headerArea, new Color(0, 0, 0, 0));
            var headerButton = headerArea.gameObject.AddComponent<Button>();
            headerButton.transition = Selectable.Transition.None;
            headerButton.targetGraphic = headerTarget;
            Wire(view, "headerSelectable", AddSelection(headerArea, headerButton, true, 2f));
            rt.gameObject.SetActive(false);
            return view;
        }
    }
}
