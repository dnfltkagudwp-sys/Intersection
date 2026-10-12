using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Intersection.Data
{
    /// <summary>파일 미리보기 블록의 표현 방식. 에셋에 정수로 저장되므로 순서를 바꾸지 않고 끝에 붙인다.</summary>
    public enum FileTextKind
    {
        /// <summary>일반 문단. 줄바꿈 그대로.</summary>
        Text,
        /// <summary>표. 한 줄이 한 행, 칸은 '|'로 구분한다.</summary>
        Table,
        /// <summary>로그·기록. 고정폭 글꼴로 줄마다.</summary>
        Log,
    }

    /// <summary>파일 미리보기에서 플레이어가 실제로 읽을 수 있는 문구 한 덩어리.</summary>
    [Serializable]
    public class FileTextBlock
    {
        public const char CellSeparator = '|';

        public FileTextKind kind;

        [TextArea(2, 12), Tooltip("화면에 그대로 보이는 문구. 표는 한 줄 = 한 행, 칸은 '|'로 구분한다.")]
        public string text;

        public bool IsEmpty => string.IsNullOrWhiteSpace(text);

        /// <summary>비어 있지 않은 줄 (줄 끝 공백 제거).</summary>
        public IEnumerable<string> Lines() =>
            IsEmpty ? Enumerable.Empty<string>()
                : text.Replace("\r", string.Empty).Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0);

        /// <summary>표의 행별 칸 (앞뒤 공백 제거).</summary>
        public IEnumerable<string[]> Rows() => Lines().Select(l => l.Split(CellSeparator).Select(c => c.Trim()).ToArray());

        /// <summary>
        /// 화면에 보이는 한 줄씩의 문구. 표는 칸을 공백 두 칸으로 이어 한 행을 한 줄로 만든다.
        /// 미리보기 렌더링과 전역 검색이 같은 원본(text)에서 계산한다.
        /// </summary>
        public IEnumerable<string> DisplayLines() =>
            kind == FileTextKind.Table ? Rows().Select(r => string.Join("  ", r.Where(c => c.Length > 0))) : Lines();
    }

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

        [Tooltip("미리보기 이미지. 비어 있으면 visibleText를 문서로 그리고, 그것도 비어 있으면 중립 자리 표시로 보인다.")]
        public Texture2D preview;

        [Tooltip("플레이어가 이 파일 미리보기에서 실제로 읽을 수 있는 문구만 넣는다.\n" +
                 "· preview 이미지가 없으면 이 블록이 그대로 미리보기로 그려진다.\n" +
                 "· preview 이미지가 있으면 그 이미지에 실제로 적힌 글자의 정확한 전사본이다(따로 그리지 않음).\n" +
                 "전역 검색은 이 문구를 그대로 검색한다. 검색용 태그·정답 키워드·작가 메모·증거 ID를 넣지 않는다.")]
        public List<FileTextBlock> visibleText = new List<FileTextBlock>();

        public bool HasVisibleText => visibleText != null && visibleText.Any(b => b != null && !b.IsEmpty);

        /// <summary>미리보기로 직접 그릴 블록. 이미지가 있으면(전사본) 또는 비어 있으면 null.</summary>
        public IReadOnlyList<FileTextBlock> RenderedDocument =>
            preview == null && HasVisibleText ? visibleText.Where(b => b != null && !b.IsEmpty).ToList() : null;

        /// <summary>플레이어가 미리보기에서 읽을 수 있는 문구 한 줄씩 (전역 검색 대상).</summary>
        public IEnumerable<string> VisibleLines() =>
            visibleText == null ? Enumerable.Empty<string>() : visibleText.Where(b => b != null).SelectMany(b => b.DisplayLines());

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
