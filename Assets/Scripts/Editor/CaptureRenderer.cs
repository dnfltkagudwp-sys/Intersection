using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Intersection.Core;
using Intersection.Data;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Intersection.EditorTools
{
    /// <summary>
    /// 휴대전화 화면 캡처(CaptureScreenData)를 공통 디자인 시스템(CaptureStyleData)으로 조판해 PNG로 만든다.
    /// 글자는 모두 TMP로 그리고(이미지 생성 도구에 글자를 그리게 하지 않음), 사진은 실제 원본을 crop·가림·축소·재압축해서만 쓴다.
    /// 아이콘은 브랜드 없는 선형 아이콘을 코드로 그린다(문자 기호·이모지를 쓰지 않음).
    /// 결과는 Assets/Art/Captures에 저장하고 파일 기록의 미리보기(preview)로 연결한다. 콘텐츠별 문구·사진 참조·좌표는 이 코드에 두지 않는다.
    /// </summary>
    public static class CaptureRenderer
    {
        const string OutputFolder = "Assets/Art/Captures";
        const string DraftFolder = "Assets/Art/Captures/Draft";
        const string TemporaryPrefix = "TEMP_";
        /// <summary>템플릿 코드가 바뀌어 모든 캡처를 다시 만들어야 할 때 올린다(검증 도구가 해시 차이로 경고).</summary>
        const int TemplateVersion = 3;
        const int Layer = 31;

        [MenuItem("Intersection/Content/Render Captures")]
        static void RenderMenu()
        {
            int n = RenderAll();
            Debug.Log($"[Capture] 캡처 {n}건을 렌더링했습니다.");
        }

        /// <summary>
        /// 원문 언어 외 언어(콘텐츠 번역 테이블마다)의 캡처를 같은 템플릿으로 만든다. 글자는 그 언어의 파일 표시 문구(visibleText 번역)에서 가져오고,
        /// 원문 PNG·원문 미리보기는 건드리지 않는다. 결과는 Assets/Art/Captures/{언어}/에 저장하고 파일 기록의 언어별 미리보기로 연결한다.
        /// </summary>
        [MenuItem("Intersection/Content/Render Localized Captures")]
        static void RenderLocalizedMenu()
        {
            int n = RenderAllLocalized(out var errors);
            foreach (var e in errors)
                Debug.LogError("[Capture] " + e);
            Debug.Log($"[Capture] 언어별 캡처 {n}건을 렌더링했습니다.");
        }

        public static int RenderAllLocalized(out List<string> errors)
        {
            errors = new List<string>();
            var config = Config();
            int count = 0;
            if (config == null)
            {
                errors.Add("GameConfig를 찾지 못했습니다.");
                return 0;
            }
            foreach (var table in config.contentTables.Where(t => t != null && !string.IsNullOrEmpty(t.language)))
                foreach (var spec in FindAll())
                {
                    if (RenderLocalized(spec, table, config.StringsFor(table.language), out var error))
                        count++;
                    else
                        errors.Add($"{AssetDatabase.GetAssetPath(spec)} ({table.language}): {error}");
                }
            AssetDatabase.SaveAssets();
            return count;
        }

        public static GameConfig Config() =>
            AssetDatabase.FindAssets("t:" + nameof(GameConfig))
                .Select(g => AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(c => c != null);

        /// <summary>그 언어의 파일 표시 문구 한 줄씩(캡처 글자의 원본). 캡처에는 토큰이 없으므로 날짜 변환은 쓰지 않는다.</summary>
        public static List<string> LocalizedLines(CaptureScreenData spec, ContentLocaleTable table)
        {
            var previous = ContentText.Current;
            ContentText.Use(table);
            try
            {
                return spec.record.VisibleLines(_ => string.Empty).ToList();
            }
            finally
            {
                ContentText.Use(previous);
            }
        }

        public static string OutputPath(CaptureScreenData spec, string language) =>
            $"{OutputFolder}/{language}/{(spec.temporary ? TemporaryPrefix : string.Empty)}{spec.name}.png";

        public static List<CaptureScreenData> FindAll() =>
            AssetDatabase.FindAssets("t:" + nameof(CaptureScreenData), new[] { "Assets/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<CaptureScreenData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null)
                .OrderBy(a => a.name, StringComparer.Ordinal)
                .ToList();

        public static int RenderAll()
        {
            int count = 0;
            foreach (var spec in FindAll())
                if (Render(spec))
                    count++;
            AssetDatabase.SaveAssets();
            return count;
        }

        public static string OutputPath(CaptureScreenData spec) =>
            $"{OutputFolder}/{(spec.temporary ? TemporaryPrefix : string.Empty)}{spec.name}.png";

        // ───────────────────────── 해시 (원본이 바뀌었는지)

        [Serializable]
        class HashSource
        {
            public int version;
            public List<CaptureBlock> blocks;
            public bool temporary;
            public string style;
            public string clock;
            public CaptureBottomBar bottom;
        }

        public static string Hash(CaptureScreenData spec) => Hash(spec, spec.blocks, Clock(spec));

        /// <summary>그 언어의 캡처 원본 해시(글자를 바꿔 넣은 덩어리와 그 언어의 상태바 시각 포함).</summary>
        public static string Hash(CaptureScreenData spec, List<CaptureBlock> blocks, string clock)
        {
            var src = new HashSource
            {
                version = TemplateVersion,
                blocks = blocks,
                temporary = spec.temporary,
                style = spec.style != null ? EditorJsonUtility.ToJson(spec.style) : string.Empty,
                clock = clock,
                bottom = spec.bottom,
            };
            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(EditorJsonUtility.ToJson(src)));
                return string.Concat(bytes.Select(b => b.ToString("x2")));
            }
        }

        /// <summary>
        /// 상태바 시각: 파일 기록 시각을 게임 휴대전화 상태바와 같은 형식으로 (장식, 검색 대상 아님).
        /// strings를 주지 않으면 원문 언어 형식(플레이어가 고른 실행 중 언어와 무관).
        /// </summary>
        public static string Clock(CaptureScreenData spec, StringTable strings = null)
        {
            var config = Config();
            if (spec.record == null || spec.style == null || config == null)
                return string.Empty;
            strings ??= config.StringsFor(config.contentSourceLanguage);
            if (strings == null)
                return string.Empty;
            CultureInfo culture;
            try
            {
                culture = CultureInfo.GetCultureInfo(!string.IsNullOrEmpty(strings.cultureName) ? strings.cultureName : config.cultureName);
            }
            catch (CultureNotFoundException)
            {
                culture = CultureInfo.InvariantCulture;
            }
            var t = spec.record.time;
            return new DateTime(2000, 1, 1, t.hour, t.minute, 0).ToString(new UIText(strings).Get(spec.style.clockFormatKey), culture);
        }

        /// <summary>그 언어 캡처의 덩어리(글자 교체본)와 해시. 줄이 맞지 않으면 null.</summary>
        public static List<CaptureBlock> LocalizedBlocks(CaptureScreenData spec, ContentLocaleTable table, out string error) =>
            spec.LocalizedBlocks(LocalizedLines(spec, table), out error);

        // ───────────────────────── 렌더링

        /// <summary>렌더링해 저장하고 파일 미리보기로 연결한다.</summary>
        public static bool Render(CaptureScreenData spec)
        {
            if (!Valid(spec))
                return false;
            string path = OutputPath(spec);
            WriteAndImport(path, RenderPng(spec), spec.style);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            spec.output = tex;
            spec.renderedHash = Hash(spec);
            spec.record.preview = tex;
            EditorUtility.SetDirty(spec);
            EditorUtility.SetDirty(spec.record);
            return true;
        }

        /// <summary>
        /// 그 언어의 캡처를 렌더링해 언어 폴더에 저장하고 파일 기록의 언어별 미리보기로 연결한다. 원문 PNG·원문 미리보기는 바꾸지 않는다.
        /// </summary>
        public static bool RenderLocalized(CaptureScreenData spec, ContentLocaleTable table, StringTable strings, out string error)
        {
            error = null;
            if (!Valid(spec) || table == null)
            {
                error ??= "캡처 원본 또는 번역 테이블이 비어 있습니다";
                return false;
            }
            var blocks = LocalizedBlocks(spec, table, out error);
            if (blocks == null)
                return false;
            string clock = Clock(spec, strings);
            string folder = $"{OutputFolder}/{table.language}";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(OutputFolder, table.language);
            string path = OutputPath(spec, table.language);
            WriteAndImport(path, RenderPng(spec, blocks, clock), spec.style);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            spec.localizedOutputs ??= new List<LocalizedCaptureOutput>();
            spec.localizedOutputs.RemoveAll(o => o == null || o.language == table.language);
            spec.localizedOutputs.Add(new LocalizedCaptureOutput { language = table.language, output = tex, renderedHash = Hash(spec, blocks, clock) });
            spec.record.SetLocalizedPreview(table.language, tex);
            EditorUtility.SetDirty(spec);
            EditorUtility.SetDirty(spec.record);
            return true;
        }

        /// <summary>시안: 같은 템플릿으로 Draft 폴더에만 저장한다. 제작 원본·파일 기록·미리보기 연결은 바꾸지 않는다.</summary>
        public static Texture2D RenderDraft(CaptureScreenData spec)
        {
            if (!Valid(spec))
                return null;
            if (!AssetDatabase.IsValidFolder(DraftFolder))
                AssetDatabase.CreateFolder(OutputFolder, "Draft");
            string path = $"{DraftFolder}/Draft_{spec.name}.png";
            WriteAndImport(path, RenderPng(spec), spec.style);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static bool Valid(CaptureScreenData spec)
        {
            if (spec != null && spec.style != null && spec.record != null)
                return true;
            Debug.LogError($"[Capture] 스타일 또는 파일 기록이 비어 있습니다: {AssetDatabase.GetAssetPath(spec)}", spec);
            return false;
        }

        /// <summary>
        /// 캡처 텍스처 가져오기 설정: 원본 해상도 유지(1080×2340이 4의 배수라 블록 압축 가능), 글자 가장자리 손상이 적은 고품질 압축(Windows BC7),
        /// Crunch 없음. 밉맵은 스타일 값(축소 화면 비교로 정함).
        /// </summary>
        static void WriteAndImport(string path, byte[] png, CaptureStyleData style)
        {
            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder("Assets/Art", "Captures");
            File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = style.mipmaps;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = style.mipmaps ? FilterMode.Trilinear : FilterMode.Bilinear;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.crunchedCompression = false;
            // Windows: 글자 가장자리 손상이 적은 BC7, 원본 크기 유지
            var standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true;
            standalone.maxTextureSize = 4096;
            standalone.format = TextureImporterFormat.BC7;
            standalone.textureCompression = TextureImporterCompression.CompressedHQ;
            standalone.crunchedCompression = false;
            importer.SetPlatformTextureSettings(standalone);
            importer.SaveAndReimport();
        }

        static byte[] RenderPng(CaptureScreenData spec) => RenderPng(spec, spec.blocks, Clock(spec));

        static byte[] RenderPng(CaptureScreenData spec, List<CaptureBlock> blocks, string clock)
        {
            var style = spec.style;
            var temps = new List<Object>();
            var scene = EditorSceneManager.NewPreviewScene();
            var rt = new RenderTexture(style.width, style.height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            // cam.Render()가 활성 RenderTexture를 rt로 남길 수 있으므로 렌더 전 값을 기억했다가 해제 전에 되돌린다.
            var previousActive = RenderTexture.active;
            try
            {
                var camGo = new GameObject("CaptureCamera");
                SceneManager.MoveGameObjectToScene(camGo, scene);
                var cam = camGo.AddComponent<Camera>();
                cam.scene = scene;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = style.background;
                cam.orthographic = true;
                cam.cullingMask = 1 << Layer;
                cam.targetTexture = rt;
                cam.enabled = false;

                var canvasGo = new GameObject("Canvas", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(canvasGo, scene);
                canvasGo.layer = Layer;
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f;
                canvasGo.AddComponent<CanvasScaler>();

                new Page(style, (RectTransform)canvasGo.transform, temps, spec.bottom).Build(blocks, clock);

                Canvas.ForceUpdateCanvases();
                foreach (var t in canvasGo.GetComponentsInChildren<TMP_Text>(true))
                    t.ForceMeshUpdate(true, true);
                Canvas.ForceUpdateCanvases();
                cam.Render();

                RenderTexture.active = rt;
                var shot = new Texture2D(style.width, style.height, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, style.width, style.height), 0, 0);
                shot.Apply();
                temps.Add(shot);
                return shot.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previousActive;
                EditorSceneManager.ClosePreviewScene(scene);
                rt.Release();
                Object.DestroyImmediate(rt);
                foreach (var o in temps)
                    if (o != null)
                        Object.DestroyImmediate(o);
            }
        }

        // ───────────────────────── 한 화면 조판

        enum Glyph { ChevronLeft, ChevronRight, Dots, Heart, Bubble, Share, Search, Multi, Home, Person, Send, Flag }

        class Page
        {
            readonly CaptureStyleData s;
            readonly RectTransform root;
            readonly List<Object> temps;
            readonly CaptureBottomBar bottom;
            readonly Dictionary<Glyph, Texture2D> icons = new Dictionary<Glyph, Texture2D>();
            /// <summary>이 화면의 주인 계정(상단 막대의 계정명). 프로필 머리의 프로필 이미지에 쓴다.</summary>
            string screenAccount;
            float W => s.width;
            float CW => s.width - s.padding * 2;
            float BottomHeight => bottom == CaptureBottomBar.None ? 0 : bottom == CaptureBottomBar.CommentInput ? s.navHeight + s.gap : s.navHeight;
            /// <summary>내용이 쓸 수 있는 화면 아래 끝 (하단 고정 영역 위).</summary>
            float ContentBottom => s.height - BottomHeight;

            public Page(CaptureStyleData style, RectTransform root, List<Object> temps, CaptureBottomBar bottom)
            {
                s = style;
                this.root = root;
                this.temps = temps;
                this.bottom = bottom == CaptureBottomBar.StyleDefault ? style.defaultBottom : bottom;
            }

            public void Build(List<CaptureBlock> blocks, string clock)
            {
                bool viewer = blocks.Any(b => b != null && b.kind == CaptureBlockKind.PhotoViewer);
                screenAccount = blocks.FirstOrDefault(b => b != null && b.kind == CaptureBlockKind.TopBar && !string.IsNullOrEmpty(b.account))?.account;
                Fill(Node("Background", root), viewer ? Dim(s.background) : s.background);

                // 1차: 고정 높이 덩어리를 만들고 높이를 잰다. 2차: 남는 높이를 채움 덩어리에 나눠 위에서부터 놓는다.
                var list = blocks.Where(b => b != null).ToList();
                var built = new RectTransform[list.Count];
                var heights = new float[list.Count];
                int fills = 0;
                float used = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    if (IsFill(list[i]))
                    {
                        fills++;
                        continue;
                    }
                    built[i] = Node(list[i].kind.ToString(), root);
                    heights[i] = BuildBlock(list[i], built[i], 0);
                    used += heights[i] * (1f - list[i].cutBelow);
                }
                float top = s.statusBarHeight;
                float remain = Mathf.Max(0, ContentBottom - top - used);
                float y = top;
                for (int i = 0; i < list.Count; i++)
                {
                    var b = list[i];
                    if (IsFill(b))
                    {
                        float h = fills > 0 ? remain / fills : 0;
                        built[i] = Node(b.kind.ToString(), root);
                        BuildBlock(b, built[i], h);
                        heights[i] = h;
                    }
                    float at = b.cutBelow > 0 ? ContentBottom - heights[i] * (1f - b.cutBelow) : y;
                    Place(built[i], 0, at, W, heights[i]);
                    y = at + heights[i];
                }
                // 하단 고정 영역은 내용 위에 겹쳐 그리지 않도록 내용 아래 끝부터 화면 끝까지를 덮는 불투명 막대다.
                switch (bottom)
                {
                    case CaptureBottomBar.Navigation: Navigation(); break;
                    case CaptureBottomBar.CommentInput: CommentInput(); break;
                    case CaptureBottomBar.BrowserToolbar: Toolbar(); break;
                }
                StatusBar(clock, viewer);
            }

            static bool IsFill(CaptureBlock b) =>
                b.kind == CaptureBlockKind.Notice || (b.kind == CaptureBlockKind.Spacer && b.height <= 0);

            /// <summary>덩어리를 그리고 높이를 돌려준다. fill이면 주어진 높이를 쓴다.</summary>
            float BuildBlock(CaptureBlock b, RectTransform c, float fill)
            {
                switch (b.kind)
                {
                    case CaptureBlockKind.TopBar: return TopBar(b, c);
                    case CaptureBlockKind.Profile: return Profile(b, c);
                    case CaptureBlockKind.Text: return TextBlock(b, c);
                    case CaptureBlockKind.Caption:
                        return s.tightGap + Rich(c, s.padding, s.tightGap, CW, b.account, b.text, s.bodySize) + s.gap;
                    case CaptureBlockKind.Buttons: return Buttons(b, c);
                    case CaptureBlockKind.Grid: return Grid(b, c);
                    case CaptureBlockKind.Image: return ImageBlock(b, c);
                    case CaptureBlockKind.Comment: return Comment(b, c);
                    case CaptureBlockKind.SearchBar: return Field(b, c, true);
                    case CaptureBlockKind.AddressBar: return Field(b, c, false);
                    case CaptureBlockKind.Tabs: return Tabs(b, c);
                    case CaptureBlockKind.Notice: return Notice(b, c, fill);
                    case CaptureBlockKind.ListItem: return ListItem(b, c);
                    case CaptureBlockKind.PostCard: return PostCard(b, c);
                    case CaptureBlockKind.PhotoViewer: return PhotoViewer(b, c);
                    case CaptureBlockKind.Spacer: return b.height > 0 ? b.height : fill;
                    default: return 0;
                }
            }

            // ── 덩어리별

            /// <summary>상단 막대: 앱 이름 없이 최소한의 탐색 영역. 게시물 머리는 작은 프로필 자리·계정명·상대 시간을 한 기준선에.</summary>
            float TopBar(CaptureBlock b, RectTransform c)
            {
                float h = s.navHeight, icon = s.iconSize;
                if (b.barStyle == CaptureBarStyle.Sheet)
                {
                    float handle = 12;
                    Rounded(Place(Node("Handle", c), (W - 96) * 0.5f, 18, 96, handle), s.divider, handle * 0.5f);
                    h += 16;
                    if (!string.IsNullOrEmpty(b.title))
                        Label(c, Esc(b.title), s.boldFont, s.titleSize, s.text, 0, 16, W, s.navHeight, TextAlignmentOptions.Center, false);
                    Divider(c, h - s.dividerThickness);
                    return h;
                }

                Icon(c, Glyph.ChevronLeft, s.padding - icon * 0.2f, (h - icon) * 0.5f, icon, s.icon);
                Icon(c, Glyph.Dots, W - s.padding - icon * 0.8f, (h - icon) * 0.5f, icon, s.icon);
                float x = s.padding + icon * 1.05f;
                if (b.barStyle == CaptureBarStyle.PostHeader)
                {
                    float a = s.headerAvatarSize;
                    Avatar(c, x, (h - a) * 0.5f, a, null, b.account);
                    x += a + s.gap * 0.6f;
                }
                if (!string.IsNullOrEmpty(b.title))
                    Label(c, Esc(b.title), s.boldFont, s.titleSize, s.text, 0, 0, W, h, TextAlignmentOptions.Center, false);
                else if (!string.IsNullOrEmpty(b.account))
                    Label(c, AccountLine(b.account, b.meta), s.regularFont, s.accountSize, s.text, x, 0, W - x - s.padding - icon, h,
                        TextAlignmentOptions.MidlineLeft, false);
                if (b.barStyle == CaptureBarStyle.Nav)
                    Divider(c, h - s.dividerThickness);
                return h;
            }

            float Profile(CaptureBlock b, RectTransform c)
            {
                float top = s.gap, size = s.profileAvatarSize;
                Avatar(c, s.padding, top, size, b.images.FirstOrDefault(), screenAccount);
                var items = b.items.Where(i => !string.IsNullOrEmpty(i)).ToList();
                float x0 = s.padding + size + s.gap, colW = items.Count > 0 ? (W - s.padding - x0) / items.Count : 0;
                for (int i = 0; i < items.Count; i++)
                {
                    // "게시물 3": 이름은 보조 색, 수치는 굵게. 한 줄, 같은 기준선.
                    string item = items[i];
                    int sp = item.LastIndexOf(' ');
                    string rich = sp < 0
                        ? Esc(item)
                        : $"<color={Hex(s.muted)}>{Esc(item.Substring(0, sp))}</color> <b>{Esc(item.Substring(sp + 1))}</b>";
                    // Number-first languages need two lines in these narrow columns.
                    // Keep the source language's single baseline and the existing avatar/row size.
                    if (sp > 0 && char.IsDigit(item[0]))
                        rich = $"<b>{Esc(item.Substring(0, sp))}</b>\n<color={Hex(s.muted)}>{Esc(item.Substring(sp + 1))}</color>";
                    Label(c, rich, s.regularFont, s.labelSize, s.text, x0 + colW * i, top, colW, size, TextAlignmentOptions.Center, false);
                }
                return top + size + s.tightGap;
            }

            float TextBlock(CaptureBlock b, RectTransform c)
            {
                string text = Esc(string.Join("\n", CaptureBlock.Split(b.text)));
                float t = s.tightGap;
                switch (b.textStyle)
                {
                    case CaptureTextStyle.Strong:
                        return t + Label(c, text, s.boldFont, s.labelSize, s.text, s.padding, t, CW, 0).rectTransform.sizeDelta.y + s.tightGap;
                    case CaptureTextStyle.Muted:
                        return t + Label(c, text, s.regularFont, s.metaSize, s.muted, s.padding, t, CW, 0).rectTransform.sizeDelta.y + s.tightGap;
                    case CaptureTextStyle.Link:
                        return t + Label(c, text, s.mediumFont, s.metaSize, s.muted, s.padding, t, CW, 0).rectTransform.sizeDelta.y + s.gap;
                    case CaptureTextStyle.Empty:
                        t = s.sectionGap * 3;
                        return t + Label(c, text, s.regularFont, s.bodySize, s.muted, s.padding, t, CW, 0, TextAlignmentOptions.Top)
                            .rectTransform.sizeDelta.y + s.sectionGap;
                    default:
                        return t + Label(c, text, s.regularFont, s.bodySize, s.text, s.padding, t, CW, 0).rectTransform.sizeDelta.y + s.gap;
                }
            }

            /// <summary>버튼은 본문보다 시선을 덜 끌게: 같은 연회색 면, 보통 굵기 글자.</summary>
            float Buttons(CaptureBlock b, RectTransform c)
            {
                var items = b.items.Where(i => !string.IsNullOrEmpty(i)).ToList();
                float gap = s.tightGap * 1.2f, w = items.Count > 0 ? (CW - gap * (items.Count - 1)) / items.Count : 0, top = s.tightGap;
                for (int i = 0; i < items.Count; i++)
                {
                    var r = Place(Node("Button", c), s.padding + (w + gap) * i, top, w, s.buttonHeight);
                    Rounded(r, s.button, s.cornerRadius);
                    Label(r, Esc(items[i]), s.mediumFont, s.labelSize, s.buttonText, 0, 0, w, s.buttonHeight, TextAlignmentOptions.Center, false);
                }
                return top + s.buttonHeight + s.gap;
            }

            float Grid(CaptureBlock b, RectTransform c)
            {
                Divider(c, 0);
                if (b.images.Count == 0)
                {
                    float t = s.sectionGap * 3;
                    return t + Label(c, Esc(string.Join("\n", CaptureBlock.Split(b.text))), s.regularFont, s.bodySize, s.muted, s.padding, t, CW, 0,
                        TextAlignmentOptions.Top).rectTransform.sizeDelta.y + s.sectionGap * 2;
                }
                const int columns = 3;
                float top = s.dividerThickness + 4, gap = 4, cell = (W - gap * (columns - 1)) / columns;
                for (int i = 0; i < b.images.Count; i++)
                    Raw(Place(Node("Cell", c), (cell + gap) * (i % columns), top + (cell + gap) * (i / columns), cell, cell), Process(b.images[i], 1f));
                int rows = (b.images.Count + columns - 1) / columns;
                return top + rows * cell + (rows - 1) * gap + s.gap;
            }

            /// <summary>게시물 사진: 화면 폭 가득, 원본 비율. 반응 아이콘 줄(수치 없음)은 사진 바로 아래.</summary>
            float ImageBlock(CaptureBlock b, RectTransform c)
            {
                float aspect = b.aspect > 0 ? b.aspect : 0.8f;
                float from = Mathf.Clamp01(b.visibleFrom), to = Mathf.Clamp(b.visibleTo, from, 1f);
                float full = W / aspect, h = full * (to - from);
                var raw = Raw(Place(Node("Image", c), 0, 0, W, h), b.images.Count > 0 ? Process(b.images[0], aspect) : Process(new CaptureImage(), aspect));
                raw.uvRect = new Rect(0, 1f - to, 1, to - from);
                float corner = s.padding * 0.8f;
                if (!string.IsNullOrEmpty(b.badge))
                {
                    var t = Label(c, Esc(b.badge), s.mediumFont, s.metaSize * 0.9f, Color.white, 0, 0, W, 0, TextAlignmentOptions.Center, false);
                    float bw = t.GetPreferredValues(t.text).x + 44, bh = s.metaSize * 1.55f;
                    var pill = Place(Node("Badge", c), W - corner - bw, corner, bw, bh);
                    Rounded(pill, new Color(0, 0, 0, 0.55f), bh * 0.5f);
                    t.rectTransform.SetParent(pill, false);
                    Place(t.rectTransform, 0, 0, bw, bh);
                }
                if (b.multiIcon)
                    Icon(c, Glyph.Multi, W - corner - s.iconSize, corner, s.iconSize, Color.white);
                if (!b.reactions)
                    return h;
                float icon = s.iconSize, row = icon + s.tightGap * 2.4f, y = h + (row - icon) * 0.5f, x = s.padding - icon * 0.12f;
                foreach (var g in new[] { Glyph.Heart, Glyph.Bubble, Glyph.Share })
                {
                    Icon(c, g, x, y, icon, s.icon);
                    x += icon * 1.45f;
                }
                return h + row;
            }

            float Comment(CaptureBlock b, RectTransform c)
            {
                float avatar = s.commentAvatarSize, top = s.gap * 0.6f, x = s.padding + avatar + s.gap * 0.8f, w = W - x - s.padding;
                Avatar(c, s.padding, top + 4, avatar, null, b.account);
                float h = Rich(c, x, top, w, b.account, b.text, s.commentSize);
                if (!string.IsNullOrEmpty(b.meta))
                    h += s.tightGap * 0.6f + Label(c, Esc(b.meta), s.regularFont, s.metaSize, s.muted, x, top + h + s.tightGap * 0.6f, w, 0)
                        .rectTransform.sizeDelta.y;
                return top + Mathf.Max(avatar, h) + s.gap * 0.6f;
            }

            float Field(CaptureBlock b, RectTransform c, bool search)
            {
                float h = s.fieldHeight, top = s.tightGap * 1.4f;
                var f = Place(Node("Field", c), s.padding, top, CW, h);
                Rounded(f, s.field, search ? s.cornerRadius : h * 0.5f);
                float x = s.gap;
                if (search)
                {
                    float icon = s.iconSize * 0.75f;
                    Icon(f, Glyph.Search, s.gap * 0.8f, (h - icon) * 0.5f, icon, s.muted);
                    x = s.gap * 0.8f + icon + s.tightGap;
                }
                Label(f, Esc(string.Join(" ", CaptureBlock.Split(b.text))), s.regularFont, search ? s.bodySize : s.metaSize,
                    search ? s.text : s.muted, x, 0, CW - x - s.gap, h, TextAlignmentOptions.MidlineLeft, false);
                float total = top + h + s.tightGap * 1.4f;
                if (!search)
                    Divider(c, total - s.dividerThickness);
                return total;
            }

            float Tabs(CaptureBlock b, RectTransform c)
            {
                var items = b.items.Where(i => !string.IsNullOrEmpty(i)).ToList();
                float h = s.navHeight * 0.85f, w = items.Count > 0 ? W / items.Count : W;
                for (int i = 0; i < items.Count; i++)
                {
                    Label(c, Esc(items[i]), s.mediumFont, s.labelSize, i == 0 ? s.text : s.muted, w * i, 0, w, h, TextAlignmentOptions.Center, false);
                    if (i == 0)
                        Fill(Place(Node("Underline", c), w * i + w * 0.2f, h - 4, w * 0.6f, 4), s.text, false);
                }
                Divider(c, h - s.dividerThickness);
                return h;
            }

            float Notice(CaptureBlock b, RectTransform c, float height)
            {
                var parts = new List<(RectTransform rt, float h)>();
                float w = W - s.padding * 4;
                if (!string.IsNullOrEmpty(b.title))
                {
                    var t = Label(c, Esc(b.title), s.boldFont, s.titleSize, s.text, s.padding * 2, 0, w, 0, TextAlignmentOptions.Top);
                    parts.Add((t.rectTransform, t.rectTransform.sizeDelta.y + s.gap));
                }
                string body = Esc(string.Join("\n", CaptureBlock.Split(b.text)));
                if (body.Length > 0)
                {
                    var t = Label(c, body, s.regularFont, s.metaSize, s.muted, s.padding * 2, 0, w, 0, TextAlignmentOptions.Top);
                    parts.Add((t.rectTransform, t.rectTransform.sizeDelta.y + s.sectionGap));
                }
                foreach (var item in b.items.Where(i => !string.IsNullOrEmpty(i)))
                {
                    var t = Label(c, Esc(item), s.mediumFont, s.labelSize, s.link, s.padding * 2, 0, w, 0, TextAlignmentOptions.Top);
                    parts.Add((t.rectTransform, t.rectTransform.sizeDelta.y));
                }
                float y = Mathf.Max(0, (height - parts.Sum(p => p.h)) * s.noticeAnchor);
                foreach (var (rt, h) in parts)
                {
                    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -y);
                    y += h;
                }
                return height;
            }

            float ListItem(CaptureBlock b, RectTransform c)
            {
                float icon = s.listIconSize, top = s.gap, x = s.padding + icon + s.gap * 0.8f, w = W - x - s.padding;
                // 신고 내역 항목은 계정 프로필이 아니라 중립 표시(잘린 계정명의 주인을 그림으로 암시하지 않음)
                NeutralIcon(c, s.padding, top, icon, Glyph.Flag);
                float h = 0;
                if (!string.IsNullOrEmpty(b.title))
                    h += Label(c, $"<b>{Esc(b.title)}</b>", s.regularFont, s.accountSize, s.text, x, top, w, 0, TextAlignmentOptions.TopLeft)
                        .rectTransform.sizeDelta.y + s.tightGap * 0.4f;
                foreach (var line in CaptureBlock.Split(b.text))
                    h += Label(c, Esc(line), s.regularFont, s.metaSize, s.muted, x, top + h, w, 0).rectTransform.sizeDelta.y + s.tightGap * 0.3f;
                float total = top + Mathf.Max(icon, h) + s.gap;
                // 신고한 대상의 미리보기(중립 이미지). 길면 화면 아래로 나가 잘린다.
                foreach (var image in b.images)
                {
                    float aspect = b.aspect > 0 ? b.aspect : 1f, ih = (W - x - s.padding) / aspect;
                    Raw(Place(Node("Preview", c), x, total, W - x - s.padding, ih), Process(image, aspect));
                    total += ih + s.gap;
                }
                Divider(c, total);
                return total + s.dividerThickness;
            }

            /// <summary>고정된 글: 카드 상자 없이 얇은 구분선과 여백으로 나눈다.</summary>
            float PostCard(CaptureBlock b, RectTransform c)
            {
                Divider(c, s.tightGap);
                float y = s.tightGap + s.dividerThickness + s.gap;
                if (!string.IsNullOrEmpty(b.title))
                    y += Label(c, Esc(b.title), s.mediumFont, s.metaSize, s.muted, s.padding, y, CW, 0).rectTransform.sizeDelta.y + s.tightGap;
                y += Rich(c, s.padding, y, CW, b.account, b.text, s.bodySize) + s.tightGap;
                if (!string.IsNullOrEmpty(b.meta))
                    y += Label(c, Esc(b.meta), s.regularFont, s.metaSize, s.muted, s.padding, y, CW, 0).rectTransform.sizeDelta.y;
                return y + s.gap;
            }

            /// <summary>
            /// 프로필 사진 크게 보기. 어둡게 덮인 프로필 화면(글자 없는 모양만) 위에 계정·이름, 큰 원형 사진, 흐린 소개문.
            /// 덮개는 미리 섞은 불투명 색으로 그린다(반투명 겹침은 색 공간에 따라 밝기가 달라짐).
            /// </summary>
            float PhotoViewer(CaptureBlock b, RectTransform c)
            {
                float height = s.height - s.statusBarHeight;
                var ghost = Place(Node("Ghost", c), 0, 0, W, height);
                Rounded(Place(Node("GhostAvatar", ghost), s.padding, s.navHeight + s.gap * 4, s.profileAvatarSize, s.profileAvatarSize),
                    Dim(s.avatarPlaceholder), s.profileAvatarSize * 0.5f);
                float cell = (W - 8) / 3f, gridTop = height * 0.62f;
                for (int i = 0; i < 6; i++)
                    Fill(Place(Node("GhostCell", ghost), (cell + 4) * (i % 3), gridTop + (cell + 4) * (i / 3), cell, cell), Dim(s.field), false);

                float y = s.gap * 2;
                if (!string.IsNullOrEmpty(b.account))
                    y += Label(c, Esc(b.account), s.boldFont, s.accountSize, s.viewerText, s.padding, y, CW, 0).rectTransform.sizeDelta.y + 6;
                if (!string.IsNullOrEmpty(b.title))
                    Label(c, Esc(b.title), s.regularFont, s.metaSize, Color.Lerp(Dim(s.background), s.viewerText, 0.8f), s.padding, y, CW, 0);

                float size = W * 0.75f, cy = height * 0.42f - size * 0.5f;
                Raw(Place(Node("Photo", c), (W - size) * 0.5f, cy, size, size),
                    Circle(b.images.Count > 0 ? Process(b.images[0], 1f) : Process(new CaptureImage(), 1f)));

                string bio = Esc(string.Join("\n", CaptureBlock.Split(b.text)));
                if (bio.Length > 0)
                    Label(c, bio, s.regularFont, s.bodySize, Color.Lerp(Dim(s.background), s.viewerText, 0.62f),
                        s.padding, cy + size + s.gap * 3, CW, 0, TextAlignmentOptions.Top);
                return height;
            }

            /// <summary>모바일 브라우저 아래 도구 막대: 뒤로·앞으로 아이콘만(글자 없음).</summary>
            void Toolbar()
            {
                var bar = BottomBar("Toolbar");
                float h = BottomHeight, icon = s.iconSize;
                Icon(bar, Glyph.ChevronLeft, W * 0.25f - icon * 0.5f, (h - icon) * 0.5f, icon, s.icon);
                Icon(bar, Glyph.ChevronRight, W * 0.75f - icon * 0.5f, (h - icon) * 0.5f, icon, s.muted);
            }

            /// <summary>
            /// 브랜드 없는 얇은 하단 탐색: 홈·검색·내 계정 선형 아이콘 셋만(글자·배지 없음). 특정 서비스의 배열을 따르지 않는다.
            /// </summary>
            void Navigation()
            {
                var bar = BottomBar("Navigation");
                float h = BottomHeight, icon = s.iconSize * 0.9f;
                var glyphs = new[] { Glyph.Home, Glyph.Search, Glyph.Person };
                for (int i = 0; i < glyphs.Length; i++)
                {
                    float cx = W * (i + 0.5f) / glyphs.Length;
                    Icon(bar, glyphs[i], cx - icon * 0.5f, (h - icon) * 0.5f, icon, i == 0 ? s.icon : s.muted);
                }
            }

            /// <summary>
            /// 댓글 화면 하단 입력 자리: 보는 사람의 중립 프로필 표시, 빈 입력 칸, 전송 아이콘. 안내 문구·새 댓글은 넣지 않는다.
            /// </summary>
            void CommentInput()
            {
                var bar = BottomBar("CommentInput");
                float h = BottomHeight, a = s.commentAvatarSize * 0.9f, icon = s.iconSize * 0.85f;
                NeutralIcon(bar, s.padding, (h - a) * 0.5f, a, Glyph.Person);
                float x = s.padding + a + s.gap * 0.6f, w = W - x - s.padding - icon - s.gap * 0.6f, fh = s.fieldHeight * 0.85f;
                Rounded(Place(Node("Field", bar), x, (h - fh) * 0.5f, w, fh), s.field, fh * 0.5f);
                Icon(bar, Glyph.Send, W - s.padding - icon, (h - icon) * 0.5f, icon, s.muted);
            }

            /// <summary>화면 아래 고정 막대 바탕(내용 아래 끝 ~ 화면 끝, 위쪽 얇은 구분선).</summary>
            RectTransform BottomBar(string name)
            {
                var bar = Place(Node(name, root), 0, ContentBottom, W, BottomHeight);
                Fill(bar, s.statusBar, false);
                Divider(bar, 0);
                return bar;
            }

            /// <summary>상태바: 장식(시각·신호·배터리). 검색·전사 대상이 아니다.</summary>
            void StatusBar(string clock, bool viewer)
            {
                var bar = Place(Node("StatusBar", root), 0, 0, W, s.statusBarHeight);
                if (!viewer)
                    Fill(bar, s.statusBar, false);
                Color ink = viewer ? s.viewerText : s.statusText;
                Label(bar, Esc(clock), s.mediumFont, s.statusSize, ink, s.padding + 24, 0, 300, s.statusBarHeight, TextAlignmentOptions.MidlineLeft, false);
                float x = W - s.padding - 24 - 64, cy = s.statusBarHeight * 0.5f;
                Rounded(Place(Node("Battery", bar), x, cy - 13, 56, 26), ink, 7);
                Fill(Place(Node("Nub", bar), x + 58, cy - 5, 4, 10), new Color(ink.r, ink.g, ink.b, 0.5f), false);
                for (int i = 0; i < 4; i++)
                {
                    float h = 9 + i * 6;
                    Fill(Place(Node("Signal", bar), x - 72 + i * 13, cy + 13 - h, 8, h), ink, false);
                }
            }

            // ── 부품

            /// <summary>
            /// 프로필 사진. 실제 사진이 있으면 그 사진, 없으면 계정명으로 정해지는 추상 무늬(같은 계정은 어디서나 같음),
            /// 계정도 없으면 중립 원.
            /// </summary>
            void Avatar(RectTransform parent, float x, float y, float size, CaptureImage image, string account)
            {
                var r = Place(Node("Avatar", parent), x, y, size, size);
                if (image != null && (image.texture != null || image.redactions.Count > 0))
                    Raw(r, Circle(Process(image, 1f)));
                else if (!string.IsNullOrEmpty(account))
                {
                    var tex = AccountAvatar.Draw(account, s.avatarPalette, 160);
                    temps.Add(tex);
                    Raw(r, tex);
                }
                else
                    Img(r, s.avatarPlaceholder, s.circleSprite);
            }

            /// <summary>계정과 무관한 중립 원 + 선형 기호 (보는 사람 자신, 신고 항목 등).</summary>
            void NeutralIcon(RectTransform parent, float x, float y, float size, Glyph glyph)
            {
                Img(Place(Node("Neutral", parent), x, y, size, size), s.avatarPlaceholder, s.circleSprite);
                float g = size * 0.56f;
                Icon(parent, glyph, x + (size - g) * 0.5f, y + (size - g) * 0.5f, g, s.muted);
            }

            void Divider(RectTransform parent, float y) =>
                Fill(Place(Node("Divider", parent), 0, y, W, s.dividerThickness), s.divider, false);

            void Icon(RectTransform parent, Glyph glyph, float x, float y, float size, Color color)
            {
                if (!icons.TryGetValue(glyph, out var tex))
                {
                    tex = Painter.Draw(glyph, 128, s.iconStroke);
                    temps.Add(tex);
                    icons[glyph] = tex;
                }
                var raw = Raw(Place(Node("Icon" + glyph, parent), x, y, size, size), tex);
                raw.color = color;
            }

            /// <summary>계정명(굵게) + 구분자 + 상대 시간(보조 색·작게)을 한 줄, 같은 기준선에.</summary>
            string AccountLine(string account, string meta) =>
                string.IsNullOrEmpty(meta)
                    ? $"<b>{Esc(account)}</b>"
                    : $"<b>{Esc(account)}</b><size={s.metaSize}><color={Hex(s.muted)}>{Esc(s.metaSeparator + meta)}</color></size>";

            /// <summary>계정명(굵게) + 본문 첫 줄을 이어 쓰고 나머지 줄은 아래. 긴 문장은 글자를 줄이지 않고 줄바꿈한다. 높이를 돌려준다.</summary>
            float Rich(RectTransform parent, float x, float y, float w, string account, string text, float size)
            {
                var lines = CaptureBlock.Split(text).Select(Esc).ToList();
                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(account))
                    sb.Append("<b>").Append(Esc(account)).Append("</b>").Append(lines.Count > 0 ? " " : string.Empty);
                sb.Append(string.Join("\n", lines));
                return Label(parent, sb.ToString(), s.regularFont, size, s.text, x, y, w, 0, TextAlignmentOptions.TopLeft)
                    .rectTransform.sizeDelta.y;
            }

            /// <summary>
            /// 데이터 글자가 서식 태그로 해석되지 않게 하고, 한국어 문장이 글자 단위가 아니라 띄어쓰기에서만 줄바꿈되도록 어절을 묶는다.
            /// (글자 크기는 줄이지 않는다. 화면에 보이는 글자는 그대로.)
            /// </summary>
            static string Esc(string s)
            {
                if (string.IsNullOrEmpty(s))
                    return string.Empty;
                return string.Join("\n", s.Split('\n').Select(EscLine));
            }

            /// <summary>
            /// 한 줄(작성자가 나눈 문단)의 어절을 묶는다. 마지막 어절이 한두 글자면(예: "하나 더"의 "더")
            /// 앞 어절과 함께 넘어가게 해 짧은 말이 혼자 다음 줄에 남지 않게 한다.
            /// </summary>
            static string EscLine(string line)
            {
                var words = line.Split(' ').ToList();
                const int shortWord = 2;
                if (words.Count >= 3 && words[words.Count - 1].Length > 0 && words[words.Count - 1].Length <= shortWord)
                {
                    words[words.Count - 2] = words[words.Count - 2] + " " + words[words.Count - 1];
                    words.RemoveAt(words.Count - 1);
                }
                return string.Join(" ", words.Select(w => w.Length == 0 ? string.Empty : "<nobr><noparse>" + w + "</noparse></nobr>"));
            }

            static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

            /// <summary>글자. height가 0이면 내용 높이에 맞춘다(줄바꿈). 위치는 부모 왼쪽 위 기준. 서식은 템플릿이 만든 태그만 쓴다.</summary>
            TextMeshProUGUI Label(RectTransform parent, string text, TMP_FontAsset font, float size, Color color, float x, float y, float w, float h,
                TextAlignmentOptions align = TextAlignmentOptions.TopLeft, bool wrap = true)
            {
                var rt = Node("Text", parent);
                var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
                t.font = font;
                t.fontSize = size;
                t.color = color;
                t.alignment = align;
                t.richText = true;
                t.lineSpacing = s.lineSpacing;
                t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                t.overflowMode = TextOverflowModes.Overflow;
                t.raycastTarget = false;
                t.text = text ?? string.Empty;
                if (h <= 0)
                    h = Mathf.Ceil(t.GetPreferredValues(t.text, wrap ? w : 100000f, 0).y);
                Place(rt, x, y, w, h);
                return t;
            }

            RawImage Raw(RectTransform rt, Texture2D tex)
            {
                var raw = rt.gameObject.AddComponent<RawImage>();
                raw.texture = tex;
                raw.raycastTarget = false;
                return raw;
            }

            Image Img(RectTransform rt, Color color, Sprite sprite)
            {
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = sprite;
                img.color = color;
                img.raycastTarget = false;
                return img;
            }

            Image Rounded(RectTransform rt, Color color, float radius)
            {
                var img = Img(rt, color, s.roundedSprite);
                if (s.roundedSprite != null && radius > 0)
                {
                    img.type = Image.Type.Sliced;
                    img.pixelsPerUnitMultiplier = s.roundedSprite.border.x / radius;
                }
                return img;
            }

            void Fill(RectTransform rt, Color color, bool stretch = true)
            {
                if (stretch)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = rt.offsetMax = Vector2.zero;
                }
                Img(rt, color, null);
            }

            /// <summary>어두운 덮개를 씌운 색 (덮개 색·불투명도는 스타일 값).</summary>
            Color Dim(Color under)
            {
                var o = s.viewerOverlay;
                return new Color(Mathf.Lerp(under.r, o.r, o.a), Mathf.Lerp(under.g, o.g, o.a), Mathf.Lerp(under.b, o.b, o.a), 1);
            }

            RectTransform Node(string name, Transform parent)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.layer = Layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(parent, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                return rt;
            }

            static RectTransform Place(RectTransform rt, float x, float y, float w, float h)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(x, -y);
                rt.sizeDelta = new Vector2(w, h);
                return rt;
            }

            // ── 사진 처리: crop → 가림 → 축소본 → JPEG 재압축

            Texture2D Process(CaptureImage img, float aspect)
            {
                Texture2D t;
                if (img.texture != null)
                {
                    var src = Readable(img.texture);
                    t = Crop(src, img.crop);
                    Object.DestroyImmediate(src);
                    t = CoverAspect(t, aspect);
                }
                else
                    t = Neutral(img, aspect);
                foreach (var r in img.redactions)
                    StickerPainter.Paint(t, r, s);
                t.Apply();
                if (img.sampleWidth > 0 && img.sampleWidth < t.width)
                    t = Resize(t, img.sampleWidth, Mathf.Max(1, Mathf.RoundToInt(img.sampleWidth * (float)t.height / t.width)));
                if (img.jpegQuality > 0)
                {
                    var jpg = t.EncodeToJPG(img.jpegQuality);
                    Object.DestroyImmediate(t);
                    t = new Texture2D(2, 2, TextureFormat.RGB24, false);
                    t.LoadImage(jpg);
                }
                t.filterMode = FilterMode.Bilinear;
                t.wrapMode = TextureWrapMode.Clamp;
                temps.Add(t);
                return t;
            }

            /// <summary>원형으로 잘라 바깥을 투명하게 (가장자리 1px만 매끄럽게).</summary>
            Texture2D Circle(Texture2D src)
            {
                int w = src.width, h = src.height;
                var px = src.GetPixels();
                float r = Mathf.Min(w, h) * 0.5f, cx = w * 0.5f, cy = h * 0.5f;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    px[y * w + x].a = Mathf.Clamp01(r - d + 0.5f);
                }
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                t.SetPixels(px);
                t.Apply();
                temps.Add(t);
                return t;
            }

            static Texture2D Readable(Texture2D tex)
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                t.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(tex)));
                return t;
            }

            static Texture2D Crop(Texture2D src, Rect r)
            {
                int x = Mathf.Clamp(Mathf.RoundToInt(r.x * src.width), 0, src.width - 1);
                int w = Mathf.Clamp(Mathf.RoundToInt(r.width * src.width), 1, src.width - x);
                int top = Mathf.Clamp(Mathf.RoundToInt(r.y * src.height), 0, src.height - 1);
                int h = Mathf.Clamp(Mathf.RoundToInt(r.height * src.height), 1, src.height - top);
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                t.SetPixels(src.GetPixels(x, src.height - top - h, w, h));
                t.Apply();
                return t;
            }

            /// <summary>crop 결과가 표시 비율과 다르면 가운데를 잘라 맞춘다(늘리지 않음).</summary>
            static Texture2D CoverAspect(Texture2D t, float aspect)
            {
                float current = (float)t.width / t.height;
                if (Mathf.Abs(current - aspect) < 0.01f)
                    return t;
                Rect r = current > aspect
                    ? new Rect((1f - aspect / current) * 0.5f, 0, aspect / current, 1)
                    : new Rect(0, (1f - current / aspect) * 0.5f, 1, current / aspect);
                var c = Crop(t, r);
                Object.DestroyImmediate(t);
                return c;
            }

            /// <summary>원본 없는 중립 이미지: 바탕색 + 낮은 대비 얼룩 (인물·사물 없음).</summary>
            static Texture2D Neutral(CaptureImage img, float aspect)
            {
                int w = 540, h = Mathf.RoundToInt(w / aspect);
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var px = new Color[w * h];
                float ox = img.seed * 17.31f, oy = img.seed * 7.77f;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float n = Mathf.PerlinNoise(ox + x * 0.006f, oy + y * 0.006f) - 0.5f;
                    float n2 = Mathf.PerlinNoise(ox * 2 + x * 0.03f, oy * 2 + y * 0.03f) - 0.5f;
                    float k = n * 0.12f + n2 * 0.03f;
                    px[y * w + x] = new Color(Mathf.Clamp01(img.background.r + k), Mathf.Clamp01(img.background.g + k),
                        Mathf.Clamp01(img.background.b + k), 1);
                }
                t.SetPixels(px);
                return t;
            }

            /// <summary>절반씩 줄여 가며 축소(한 번에 크게 줄일 때 생기는 깨짐 방지).</summary>
            static Texture2D Resize(Texture2D src, int w, int h)
            {
                var current = src;
                while (current.width / 2 >= w && current.height / 2 >= h)
                {
                    var half = Blit(current, current.width / 2, current.height / 2);
                    Object.DestroyImmediate(current);
                    current = half;
                }
                var result = Blit(current, w, h);
                Object.DestroyImmediate(current);
                return result;
            }

            static Texture2D Blit(Texture2D src, int w, int h)
            {
                src.filterMode = FilterMode.Bilinear;
                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(src, rt);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                t.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                t.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                return t;
            }
        }

        // ───────────────────────── 게시자 가림 스티커

        /// <summary>
        /// 게시자가 붙인 얼굴 가림 스티커: 크림색 둥근 바탕에 단순한 눈·입, 아주 약한 테두리와 그림자.
        /// 브랜드·OS 이모지를 따라 하지 않는다. rect(왼쪽 위 기준 0~1)를 덮는 원으로 그린다(지름 = rect의 긴 변, 사진 안에 들어가도록 제한).
        /// 사진 파생본(c-post-01 등)과 캡처 안 이미지가 같은 모양을 쓴다.
        /// </summary>
        public static class StickerPainter
        {
            public static void Paint(Texture2D t, Rect r, CaptureStyleData s)
            {
                float cx = (r.x + r.width * 0.5f) * t.width, cy = (r.y + r.height * 0.5f) * t.height;
                float rad = Mathf.Max(r.width * t.width, r.height * t.height) * 0.5f;
                // 스티커는 사진 밖으로 넘치지 않는다(사진 전체를 가리는 영역이면 사진 안에 꽉 차는 원).
                rad = Mathf.Min(rad, Mathf.Min(t.width, t.height) * 0.5f);
                cx = Mathf.Clamp(cx, rad, t.width - rad);
                cy = Mathf.Clamp(cy, rad, t.height - rad);
                Paint(t, new Vector2(cx, cy), rad, s.stickerFill, s.stickerRim, s.stickerInk, s.stickerShadow);
            }

            /// <param name="center">왼쪽 위 기준 픽셀 좌표</param>
            public static void Paint(Texture2D t, Vector2 center, float rad, Color fill, Color rim, Color ink, float shadow)
            {
                var shadowOffset = new Vector2(rad * 0.03f, rad * 0.05f);
                float rimWidth = Mathf.Max(1.2f, rad * 0.035f), soft = Mathf.Max(1.5f, rad * 0.08f);
                var eyeL = center + new Vector2(-0.30f, -0.14f) * rad;
                var eyeR = center + new Vector2(0.30f, -0.14f) * rad;
                float eyeR0 = rad * 0.085f;
                var mouthC = center + new Vector2(0, -0.02f) * rad;
                float mouthR = rad * 0.42f, mouthW = Mathf.Max(1.2f, rad * 0.06f);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(center.x - rad - soft * 2)), x1 = Mathf.Min(t.width, Mathf.CeilToInt(center.x + rad + soft * 3));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(center.y - rad - soft * 2)), y1 = Mathf.Min(t.height, Mathf.CeilToInt(center.y + rad + soft * 3));
                for (int ty = y0; ty < y1; ty++)
                for (int x = x0; x < x1; x++)
                {
                    var p = new Vector2(x + 0.5f, ty + 0.5f);
                    int y = t.height - 1 - ty;
                    var c = t.GetPixel(x, y);
                    // 아주 약한 그림자
                    float ds = Vector2.Distance(p, center + shadowOffset) - rad;
                    float sh = shadow * Mathf.Clamp01(1f - ds / soft);
                    c = Color.Lerp(c, Color.black, Mathf.Clamp01(sh));
                    float d = Vector2.Distance(p, center);
                    float inside = Mathf.Clamp01(rad - d + 0.5f);
                    if (inside > 0)
                    {
                        // 테두리 → 바탕
                        float rimA = Mathf.Clamp01(d - (rad - rimWidth) + 0.5f);
                        var face = Color.Lerp(fill, rim, rimA);
                        // 눈
                        float eye = Mathf.Max(Mathf.Clamp01(eyeR0 - Vector2.Distance(p, eyeL) + 0.5f), Mathf.Clamp01(eyeR0 - Vector2.Distance(p, eyeR) + 0.5f));
                        // 입: 아래쪽 원호(약 35°~145°)
                        var mv = p - mouthC;
                        float ang = Mathf.Atan2(mv.y, mv.x) * Mathf.Rad2Deg;
                        float mouth = ang > 35 && ang < 145 ? Mathf.Clamp01(mouthW * 0.5f - Mathf.Abs(mv.magnitude - mouthR) + 0.5f) : 0;
                        face = Color.Lerp(face, ink, Mathf.Max(eye, mouth));
                        c = Color.Lerp(c, face, inside);
                    }
                    c.a = 1;
                    t.SetPixel(x, y, c);
                }
            }
        }

        // ───────────────────────── 계정 프로필 무늬

        /// <summary>
        /// 실제 얼굴·인물 정보 없이 계정명만으로 정해지는 추상 프로필 이미지. 계정명 해시(FNV-1a)로 팔레트의 낮은 채도 색 2~3개와
        /// 도형 하나를 골라 원형으로 그린다. 같은 계정명이면 어느 캡처에서나 같은 이미지다.
        /// </summary>
        public static class AccountAvatar
        {
            public static uint Hash(string account)
            {
                uint h = 2166136261;
                foreach (char ch in account ?? string.Empty)
                {
                    h ^= ch;
                    h *= 16777619;
                }
                return h;
            }

            public static Texture2D Draw(string account, Color[] palette, int size)
            {
                uint h = Hash(account);
                int n = Mathf.Max(1, palette.Length);
                var bg = palette[h % n];
                var fg = palette[(h / (uint)n + 1 + h % n) % n];
                if (fg == bg)
                    fg = palette[(h % n + 1) % n];
                var accent = Color.Lerp(fg, Color.white, 0.35f);
                int shape = (int)((h >> 16) % 3);
                var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[size * size];
                float r = size * 0.5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size, v = 1f - (y + 0.5f) / size; // v: 위 0 → 아래 1
                    Color c = bg;
                    switch (shape)
                    {
                        case 0: // 아래쪽 큰 원호(언덕)
                            if (Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 1.15f)) < 0.62f) c = fg;
                            break;
                        case 1: // 사선 띠
                            float k = u + v;
                            if (k > 0.85f && k < 1.25f) c = fg;
                            else if (k >= 1.25f) c = accent;
                            break;
                        default: // 비켜 놓은 작은 원
                            if (Vector2.Distance(new Vector2(u, v), new Vector2(0.62f, 0.4f)) < 0.24f) c = fg;
                            else if (v > 0.72f) c = accent;
                            break;
                    }
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    c.a = Mathf.Clamp01(r - d + 0.5f);
                    px[y * size + x] = c;
                }
                t.SetPixels(px);
                t.Apply();
                return t;
            }
        }

        // ───────────────────────── 선형 아이콘 (브랜드 없음, 흰색 + 투명 → RawImage 색으로 칠함)

        static class Painter
        {
            /// <summary>도형을 선분·원 목록으로 정의하고 픽셀마다 거리로 칠한다. 좌표는 왼쪽 위 0 ~ 1.</summary>
            public static Texture2D Draw(Glyph glyph, int size, float strokeRatio)
            {
                var segments = new List<(Vector2 a, Vector2 b)>();
                var rings = new List<(Vector2 c, float r)>();
                var dots = new List<(Vector2 c, float r)>();
                void Poly(params Vector2[] p)
                {
                    for (int i = 0; i + 1 < p.Length; i++)
                        segments.Add((p[i], p[i + 1]));
                }
                switch (glyph)
                {
                    case Glyph.ChevronLeft:
                        Poly(new Vector2(0.60f, 0.20f), new Vector2(0.32f, 0.50f), new Vector2(0.60f, 0.80f));
                        break;
                    case Glyph.ChevronRight:
                        Poly(new Vector2(0.40f, 0.20f), new Vector2(0.68f, 0.50f), new Vector2(0.40f, 0.80f));
                        break;
                    case Glyph.Dots:
                        dots.Add((new Vector2(0.24f, 0.5f), 0.065f));
                        dots.Add((new Vector2(0.5f, 0.5f), 0.065f));
                        dots.Add((new Vector2(0.76f, 0.5f), 0.065f));
                        break;
                    case Glyph.Heart:
                    {
                        var pts = new List<Vector2>();
                        for (int i = 0; i <= 64; i++)
                        {
                            float t = Mathf.PI * 2 * i / 64f;
                            float x = 16 * Mathf.Pow(Mathf.Sin(t), 3);
                            float y = 13 * Mathf.Cos(t) - 5 * Mathf.Cos(2 * t) - 2 * Mathf.Cos(3 * t) - Mathf.Cos(4 * t);
                            pts.Add(new Vector2(0.5f + x / 40f, 0.47f - y / 40f));
                        }
                        Poly(pts.ToArray());
                        break;
                    }
                    case Glyph.Bubble:
                    {
                        // 일반적인 말풍선: 둥근 사각형 + 왼쪽 아래 짧은 꼬리 (좌표는 왼쪽 위 기준, y 아래로)
                        const float l = 0.14f, rgt = 0.86f, top = 0.18f, bot = 0.70f, r = 0.14f;
                        var pts = new List<Vector2> { new Vector2(0.44f, bot), new Vector2(rgt - r, bot) };
                        void Arc(Vector2 c, float a0, float a1)
                        {
                            for (int i = 1; i <= 10; i++)
                            {
                                float a = Mathf.Deg2Rad * Mathf.Lerp(a0, a1, i / 10f);
                                pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                            }
                        }
                        Arc(new Vector2(rgt - r, bot - r), 90, 0);
                        pts.Add(new Vector2(rgt, top + r));
                        Arc(new Vector2(rgt - r, top + r), 0, -90);
                        pts.Add(new Vector2(l + r, top));
                        Arc(new Vector2(l + r, top + r), -90, -180);
                        pts.Add(new Vector2(l, bot - r));
                        Arc(new Vector2(l + r, bot - r), 180, 90);
                        pts.Add(new Vector2(0.30f, bot));
                        pts.Add(new Vector2(0.25f, 0.86f));
                        pts.Add(new Vector2(0.44f, bot));
                        Poly(pts.ToArray());
                        break;
                    }
                    case Glyph.Share:
                        Poly(new Vector2(0.5f, 0.60f), new Vector2(0.5f, 0.16f));
                        Poly(new Vector2(0.33f, 0.32f), new Vector2(0.5f, 0.16f), new Vector2(0.67f, 0.32f));
                        Poly(new Vector2(0.30f, 0.46f), new Vector2(0.24f, 0.46f), new Vector2(0.24f, 0.84f), new Vector2(0.76f, 0.84f),
                            new Vector2(0.76f, 0.46f), new Vector2(0.70f, 0.46f));
                        break;
                    case Glyph.Home:
                        Poly(new Vector2(0.18f, 0.48f), new Vector2(0.5f, 0.20f), new Vector2(0.82f, 0.48f));
                        Poly(new Vector2(0.27f, 0.42f), new Vector2(0.27f, 0.82f), new Vector2(0.73f, 0.82f), new Vector2(0.73f, 0.42f));
                        break;
                    case Glyph.Person:
                    {
                        rings.Add((new Vector2(0.5f, 0.36f), 0.15f));
                        var arc = new List<Vector2>();
                        for (int i = 0; i <= 24; i++)
                        {
                            float a = Mathf.Deg2Rad * (200 + 140f * i / 24f);
                            arc.Add(new Vector2(0.5f, 0.92f) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.30f);
                        }
                        Poly(arc.ToArray());
                        break;
                    }
                    case Glyph.Send:
                        Poly(new Vector2(0.18f, 0.5f), new Vector2(0.80f, 0.5f));
                        Poly(new Vector2(0.56f, 0.26f), new Vector2(0.80f, 0.5f), new Vector2(0.56f, 0.74f));
                        break;
                    case Glyph.Flag:
                        Poly(new Vector2(0.30f, 0.84f), new Vector2(0.30f, 0.16f));
                        Poly(new Vector2(0.30f, 0.20f), new Vector2(0.74f, 0.20f), new Vector2(0.64f, 0.36f), new Vector2(0.74f, 0.52f), new Vector2(0.30f, 0.52f));
                        break;
                    case Glyph.Search:
                        rings.Add((new Vector2(0.44f, 0.44f), 0.24f));
                        Poly(new Vector2(0.62f, 0.62f), new Vector2(0.82f, 0.82f));
                        break;
                    case Glyph.Multi:
                        Poly(new Vector2(0.14f, 0.32f), new Vector2(0.68f, 0.32f), new Vector2(0.68f, 0.86f), new Vector2(0.14f, 0.86f), new Vector2(0.14f, 0.32f));
                        Poly(new Vector2(0.32f, 0.32f), new Vector2(0.32f, 0.14f), new Vector2(0.86f, 0.14f), new Vector2(0.86f, 0.68f), new Vector2(0.68f, 0.68f));
                        break;
                }

                float half = size * strokeRatio * 0.5f;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f) / size;
                    float d = float.MaxValue;
                    foreach (var (a, b) in segments)
                        d = Mathf.Min(d, SegmentDistance(p, a, b));
                    foreach (var (c, r) in rings)
                        d = Mathf.Min(d, Mathf.Abs(Vector2.Distance(p, c) - r));
                    float alpha = Mathf.Clamp01(half - d * size + 0.5f);
                    foreach (var (c, r) in dots)
                        alpha = Mathf.Max(alpha, Mathf.Clamp01((r - Vector2.Distance(p, c)) * size + 0.5f));
                    // 텍스처는 아래가 y=0
                    px[(size - 1 - y) * size + x] = new Color(1, 1, 1, alpha);
                }
                tex.SetPixels(px);
                tex.Apply();
                return tex;
            }

            static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
            {
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                return Vector2.Distance(p, a + ab * t);
            }
        }
    }
}
