using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Intersection.Data
{
    /// <summary>캡처 화면 한 덩어리의 모양. 에셋에 정수로 저장되므로 순서를 바꾸지 않고 끝에 붙인다.</summary>
    public enum CaptureBlockKind
    {
        /// <summary>화면 위 막대. title이 있으면 가운데 제목, 없으면 account(+ 구분자 + meta).</summary>
        TopBar,
        /// <summary>프로필 머리: images[0] 프로필 사진(비우면 기본 사람 모양) + items(게시물·팔로워·팔로잉 수치).</summary>
        Profile,
        /// <summary>문단. 줄바꿈 그대로, textStyle 모양.</summary>
        Text,
        /// <summary>게시물 글: account(굵게) + text 첫 줄을 이어 쓰고 나머지 줄은 아래.</summary>
        Caption,
        /// <summary>items 버튼 한 줄.</summary>
        Buttons,
        /// <summary>3칸 그리드: images. 비어 있으면 text를 빈 그리드 안내로 보인다.</summary>
        Grid,
        /// <summary>게시 이미지: images[0], aspect, 화면에 보이는 세로 범위, badge, multiIcon.</summary>
        Image,
        /// <summary>댓글: account(굵게) + text, 아래 meta.</summary>
        Comment,
        /// <summary>검색창: text.</summary>
        SearchBar,
        /// <summary>탭 한 줄: items (첫 탭이 선택됨).</summary>
        Tabs,
        /// <summary>브라우저 주소창: text.</summary>
        AddressBar,
        /// <summary>안내 화면: title(제목) + text(본문) + items[0](링크). 남는 높이 가운데.</summary>
        Notice,
        /// <summary>목록 항목: 왼쪽 아이콘 + title(굵게) + text 줄 + images 미리보기(폭 가득, aspect).</summary>
        ListItem,
        /// <summary>글 카드: title(위 작은 표시) + account·text(글) + meta(아래).</summary>
        PostCard,
        /// <summary>프로필 사진 크게 보기: account, title(표시 이름), images[0](원형), text(뒤로 흐린 소개문).</summary>
        PhotoViewer,
        /// <summary>빈 높이(height). 0이면 남는 높이를 채운다.</summary>
        Spacer,
    }

    /// <summary>상단 막대 모양. 에셋에 정수로 저장되므로 순서를 바꾸지 않고 끝에 붙인다.</summary>
    public enum CaptureBarStyle
    {
        /// <summary>뒤로 + 가운데 제목 또는 왼쪽 계정명 (프로필·설정 화면).</summary>
        Nav,
        /// <summary>게시물 머리: 뒤로 + 작은 프로필 자리 + 계정명·상대 시간 한 줄.</summary>
        PostHeader,
        /// <summary>아래에서 올라온 시트: 손잡이 + 가운데 제목 (댓글 등).</summary>
        Sheet,
    }

    /// <summary>화면 아래 고정 영역. 에셋에 정수로 저장되므로 순서를 바꾸지 않고 끝에 붙인다.</summary>
    public enum CaptureBottomBar
    {
        /// <summary>스타일의 기본값을 따른다.</summary>
        StyleDefault,
        None,
        /// <summary>브랜드 없는 얇은 하단 탐색(선형 아이콘만, 글자 없음).</summary>
        Navigation,
        /// <summary>댓글 입력 자리: 중립 프로필·빈 입력 칸·전송 아이콘(글자 없음).</summary>
        CommentInput,
        /// <summary>모바일 브라우저 뒤로·앞으로.</summary>
        BrowserToolbar,
    }

    public enum CaptureTextStyle
    {
        Body,
        Strong,
        Muted,
        Link,
        /// <summary>가운데 정렬 흐린 안내(검색 결과 없음 등).</summary>
        Empty,
    }

    /// <summary>
    /// 캡처 화면 안 사진 한 장. 실제 원본(texture)에서 crop → 게시자 가림 → 축소본 → JPEG 재압축 순서로 만든다.
    /// texture가 없으면 바탕색·노이즈와 가림 박스만 있는 중립 이미지(의미 없는 썸네일·검수용 임시 이미지)다.
    /// 좌표는 모두 왼쪽 위 기준 0~1.
    /// </summary>
    [Serializable]
    public class CaptureImage
    {
        [Tooltip("원본 이미지. 비우면 바탕색 + 가림 박스만 있는 중립 이미지")]
        public Texture2D texture;
        [Tooltip("원본에서 쓸 영역 (왼쪽 위 기준 x, y, 폭, 높이 0~1)")]
        public Rect crop = new Rect(0, 0, 1, 1);
        [Tooltip("게시자가 한 가림: 회색 둥근 사각형 (crop 기준 왼쪽 위 0~1)")]
        public List<Rect> redactions = new List<Rect>();
        [Tooltip("0보다 크면 이 폭으로 줄였다가 다시 키운다 (플랫폼 축소본)")]
        public int sampleWidth;
        [Tooltip("0보다 크면 이 품질로 JPEG 재압축한다")]
        [Range(0, 100)] public int jpegQuality;
        [Tooltip("texture가 없을 때 바탕색")]
        public Color background = new Color(0.78f, 0.76f, 0.72f);
        [Tooltip("texture가 없을 때 바탕 얼룩의 난수 씨앗")]
        public int seed;
    }

    [Serializable]
    public class CaptureBlock
    {
        public CaptureBlockKind kind;
        [Tooltip("TopBar 모양")]
        public CaptureBarStyle barStyle;
        [Tooltip("게시물 사진 아래 반응 아이콘 줄(좋아요·댓글·공유 선형 아이콘, 수치 없음)")]
        public bool reactions;
        public string title;
        public string account;
        public string meta;
        [TextArea(1, 6)] public string text;
        public CaptureTextStyle textStyle;
        public List<string> items = new List<string>();
        public List<CaptureImage> images = new List<CaptureImage>();
        [Tooltip("이미지 가로/세로 (4:5 = 0.8)")]
        public float aspect = 0.8f;
        [Tooltip("이미지 중 화면에 보이는 세로 범위 (위 0 ~ 아래 1). 스크롤로 화면 밖에 나간 부분은 그리지 않는다.")]
        [Range(0, 1)] public float visibleFrom;
        [Range(0, 1)] public float visibleTo = 1f;
        [Tooltip("이미지 오른쪽 위 표시 (예: 2/3)")]
        public string badge;
        [Tooltip("이미지 오른쪽 위 여러 장 표시")]
        public bool multiIcon;
        [Tooltip("Spacer 높이(px). 0이면 남는 높이를 채운다.")]
        public float height;
        [Tooltip("이 덩어리 중 화면 아래로 나가 잘리는 비율 (0~1). 화면 맨 아래에 붙여 그린다.")]
        [Range(0, 1)] public float cutBelow;

        /// <summary>이 덩어리에서 화면에 읽히는 글자 (위에서 아래 순서).</summary>
        public IEnumerable<string> Lines(CaptureStyleData style)
        {
            switch (kind)
            {
                case CaptureBlockKind.TopBar:
                    if (!string.IsNullOrEmpty(title))
                        yield return title;
                    else if (!string.IsNullOrEmpty(account))
                        yield return string.IsNullOrEmpty(meta) ? account : account + style.metaSeparator + meta;
                    break;
                case CaptureBlockKind.Profile:
                case CaptureBlockKind.Buttons:
                case CaptureBlockKind.Tabs:
                    foreach (var i in items.Where(i => !string.IsNullOrEmpty(i)))
                        yield return i;
                    break;
                case CaptureBlockKind.Text:
                case CaptureBlockKind.SearchBar:
                case CaptureBlockKind.AddressBar:
                    foreach (var l in Split(text))
                        yield return l;
                    break;
                case CaptureBlockKind.Grid:
                    if (images.Count == 0)
                        foreach (var l in Split(text))
                            yield return l;
                    break;
                case CaptureBlockKind.Image:
                    if (!string.IsNullOrEmpty(badge))
                        yield return badge;
                    break;
                case CaptureBlockKind.Caption:
                    foreach (var l in Joined(account, text))
                        yield return l;
                    break;
                case CaptureBlockKind.Comment:
                    foreach (var l in Joined(account, text))
                        yield return l;
                    if (!string.IsNullOrEmpty(meta))
                        yield return meta;
                    break;
                case CaptureBlockKind.Notice:
                    if (!string.IsNullOrEmpty(title))
                        yield return title;
                    foreach (var l in Split(text))
                        yield return l;
                    foreach (var i in items.Where(i => !string.IsNullOrEmpty(i)))
                        yield return i;
                    break;
                case CaptureBlockKind.ListItem:
                    if (!string.IsNullOrEmpty(title))
                        yield return title;
                    foreach (var l in Split(text))
                        yield return l;
                    break;
                case CaptureBlockKind.PostCard:
                    if (!string.IsNullOrEmpty(title))
                        yield return title;
                    foreach (var l in Joined(account, text))
                        yield return l;
                    if (!string.IsNullOrEmpty(meta))
                        yield return meta;
                    break;
                case CaptureBlockKind.PhotoViewer:
                    if (!string.IsNullOrEmpty(account))
                        yield return account;
                    if (!string.IsNullOrEmpty(title))
                        yield return title;
                    foreach (var l in Split(text))
                        yield return l;
                    break;
            }
        }

        public static IEnumerable<string> Split(string s) =>
            string.IsNullOrEmpty(s) ? Enumerable.Empty<string>()
                : s.Replace("\r", string.Empty).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);

        /// <summary>계정명 + 첫 줄을 한 줄로, 나머지 줄은 그대로.</summary>
        static IEnumerable<string> Joined(string account, string text)
        {
            var lines = Split(text).ToList();
            if (string.IsNullOrEmpty(account))
                return lines;
            if (lines.Count == 0)
                return new[] { account };
            lines[0] = account + " " + lines[0];
            return lines;
        }

        /// <summary>글자를 바꿔 넣을 사본. 사진 목록은 같은 원본을 가리킨다(바꾸지 않음).</summary>
        public CaptureBlock Clone() => new CaptureBlock
        {
            kind = kind, barStyle = barStyle, reactions = reactions, title = title, account = account, meta = meta, text = text,
            textStyle = textStyle, items = new List<string>(items), images = images, aspect = aspect,
            visibleFrom = visibleFrom, visibleTo = visibleTo, badge = badge, multiIcon = multiIcon, height = height, cutBelow = cutBelow,
        };

        /// <summary>
        /// Lines()와 같은 순서로 줄을 받아 글자 칸을 바꾼다(다른 언어 캡처용). 계정명은 그대로 두고, 계정명이 붙는 줄은 그 계정명으로 시작해야 한다.
        /// </summary>
        public bool Assign(Queue<string> q, CaptureStyleData style, out string error)
        {
            string err = null;
            string Next()
            {
                if (q.Count > 0)
                    return q.Dequeue();
                err ??= $"{kind} 덩어리에 넣을 줄이 모자랍니다";
                return null;
            }
            string ReplaceText(string original)
            {
                // 작성자가 나눈 빈 줄·문단 구조는 두고 글자 줄만 차례로 바꾼다.
                if (string.IsNullOrEmpty(original))
                    return original;
                var src = original.Replace("\r", string.Empty).Split('\n');
                for (int i = 0; i < src.Length; i++)
                    if (src[i].Trim().Length > 0)
                        src[i] = Next() ?? src[i];
                return string.Join("\n", src);
            }
            string StripAccount(string line, string separator)
            {
                if (line == null)
                    return null;
                string prefix = account + separator;
                if (line.StartsWith(prefix, StringComparison.Ordinal))
                    return line.Substring(prefix.Length);
                err ??= $"{kind} 줄이 계정명 '{account}'으로 시작해야 합니다: '{line}'";
                return null;
            }
            void ReplaceJoined()
            {
                var count = Split(text).Count();
                if (string.IsNullOrEmpty(account))
                {
                    text = ReplaceText(text);
                    return;
                }
                if (count == 0)
                {
                    string only = Next();
                    if (only != null && only != account)
                        err ??= $"{kind} 줄은 계정명 '{account}'이어야 합니다: '{only}'";
                    return;
                }
                string first = StripAccount(Next(), " ");
                var rest = new List<string> { first };
                for (int i = 1; i < count; i++)
                    rest.Add(Next());
                if (err == null)
                    text = string.Join("\n", rest);
            }
            void ReplaceItems()
            {
                for (int i = 0; i < items.Count; i++)
                    if (!string.IsNullOrEmpty(items[i]))
                        items[i] = Next() ?? items[i];
            }

            switch (kind)
            {
                case CaptureBlockKind.TopBar:
                    if (!string.IsNullOrEmpty(title))
                        title = Next();
                    else if (!string.IsNullOrEmpty(account))
                    {
                        if (string.IsNullOrEmpty(meta))
                        {
                            string only = Next();
                            if (only != null && only != account)
                                err ??= $"TopBar 줄은 계정명 '{account}'이어야 합니다: '{only}'";
                        }
                        else
                            meta = StripAccount(Next(), style.metaSeparator) ?? meta;
                    }
                    break;
                case CaptureBlockKind.Profile:
                case CaptureBlockKind.Buttons:
                case CaptureBlockKind.Tabs:
                    ReplaceItems();
                    break;
                case CaptureBlockKind.Text:
                case CaptureBlockKind.SearchBar:
                case CaptureBlockKind.AddressBar:
                    text = ReplaceText(text);
                    break;
                case CaptureBlockKind.Grid:
                    if (images.Count == 0)
                        text = ReplaceText(text);
                    break;
                case CaptureBlockKind.Image:
                    if (!string.IsNullOrEmpty(badge))
                        badge = Next();
                    break;
                case CaptureBlockKind.Caption:
                    ReplaceJoined();
                    break;
                case CaptureBlockKind.Comment:
                    ReplaceJoined();
                    if (!string.IsNullOrEmpty(meta))
                        meta = Next();
                    break;
                case CaptureBlockKind.Notice:
                    if (!string.IsNullOrEmpty(title))
                        title = Next();
                    text = ReplaceText(text);
                    ReplaceItems();
                    break;
                case CaptureBlockKind.ListItem:
                    if (!string.IsNullOrEmpty(title))
                        title = Next();
                    text = ReplaceText(text);
                    break;
                case CaptureBlockKind.PostCard:
                    if (!string.IsNullOrEmpty(title))
                        title = Next();
                    ReplaceJoined();
                    if (!string.IsNullOrEmpty(meta))
                        meta = Next();
                    break;
                case CaptureBlockKind.PhotoViewer:
                    if (!string.IsNullOrEmpty(account))
                    {
                        string only = Next();
                        if (only != null && only != account)
                            err ??= $"PhotoViewer 첫 줄은 계정명 '{account}'이어야 합니다: '{only}'";
                    }
                    if (!string.IsNullOrEmpty(title))
                        title = Next();
                    text = ReplaceText(text);
                    break;
            }
            error = err;
            return err == null;
        }
    }

    /// <summary>
    /// 휴대전화 화면 캡처(파일 미리보기 PNG)의 제작 원본. 글자·사진 참조는 모두 여기 두고,
    /// 에디터 메뉴 Intersection/Content/Render Captures가 공통 템플릿(CaptureStyleData)으로 조판해 PNG를 만든 뒤 record.preview에 연결한다.
    /// 화면에 읽히는 글자(Lines)는 record.visibleText와 같아야 한다(검증 도구가 대조).
    /// 런타임에는 쓰지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Intersection/Production/Capture Screen", fileName = "Capture")]
    public class CaptureScreenData : ScriptableObject
    {
        [Tooltip("이 캡처를 미리보기로 쓰는 파일. 상태바 시각은 이 파일의 시각에서 계산한다.")]
        public FileRecord record;
        public CaptureStyleData style;
        public List<CaptureBlock> blocks = new List<CaptureBlock>();
        [Tooltip("화면 아래 고정 영역. 기본은 스타일 값. 글자를 넣지 않으며 검색·전사 대상이 아니다.")]
        public CaptureBottomBar bottom;

        [Header("제작 상태")]
        [Tooltip("정식 자산이 아닌 교체 예정 임시 캡처. 출력 파일명 앞에 TEMP_가 붙고 검증 결과에 임시 자산으로 따로 표시된다.")]
        public bool temporary;
        [TextArea(1, 4), Tooltip("임시인 이유와 교체 조건 (제작용, 화면에 표시하지 않음)")]
        public string temporaryNote;

        [Header("렌더링 결과 (자동)")]
        public Texture2D output;
        [Tooltip("마지막 렌더링 때의 원본 해시. 원본이 바뀌면 검증 도구가 다시 렌더링하라고 경고한다.")]
        public string renderedHash;

        [Tooltip("원문 언어 외 언어별 렌더링 결과(자동). 글자는 그 언어의 파일 표시 문구(visibleText 번역)에서 가져온다.")]
        public List<LocalizedCaptureOutput> localizedOutputs = new List<LocalizedCaptureOutput>();

        /// <summary>캡처에서 읽히는 글자 전체 (상태바 시각·배터리 제외).</summary>
        public IEnumerable<string> Lines() =>
            style == null ? Enumerable.Empty<string>() : blocks.Where(b => b != null).SelectMany(b => b.Lines(style));

        public LocalizedCaptureOutput OutputFor(string language) =>
            localizedOutputs?.FirstOrDefault(o => o != null && o.language == language);

        /// <summary>
        /// 다른 언어의 화면 글자(파일 표시 문구 한 줄씩, Lines()와 같은 순서·같은 줄 수)를 덩어리에 다시 나눠 넣은 사본.
        /// 사진·모양·수치 배치는 그대로이고 글자만 바뀐다. 계정명은 바꾸지 않으므로 계정명이 붙는 줄은 같은 계정명으로 시작해야 한다.
        /// 맞지 않으면 null과 이유를 돌려준다(검증 도구가 오류로 알린다).
        /// </summary>
        public List<CaptureBlock> LocalizedBlocks(IReadOnlyList<string> lines, out string error)
        {
            error = null;
            if (style == null)
            {
                error = "스타일이 비어 있습니다";
                return null;
            }
            var queue = new Queue<string>(lines ?? new List<string>());
            var result = new List<CaptureBlock>();
            foreach (var b in blocks)
            {
                if (b == null)
                    continue;
                var copy = b.Clone();
                if (!copy.Assign(queue, style, out error))
                    return null;
                result.Add(copy);
            }
            if (queue.Count > 0)
            {
                error = $"글자 줄이 {queue.Count}줄 남습니다(첫 줄: '{queue.Peek()}')";
                return null;
            }
            return result;
        }
    }

    /// <summary>원문 언어 외 언어 하나의 캡처 렌더링 결과.</summary>
    [Serializable]
    public class LocalizedCaptureOutput
    {
        public string language;
        public Texture2D output;
        [Tooltip("마지막 렌더링 때의 원본 해시(그 언어의 글자 포함)")]
        public string renderedHash;
    }
}
