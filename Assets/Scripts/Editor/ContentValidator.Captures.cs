using System.Collections.Generic;
using System.Linq;
using Intersection.Data;
using UnityEngine;

namespace Intersection.EditorTools
{
    public static partial class ContentValidator
    {
        /// <summary>
        /// 휴대전화 화면 캡처 제작 원본(CaptureScreenData) 검사.
        /// 캡처에 읽히는 글자는 파일 표시 문구(visibleText, 검색 대상)와 작성자가 나눈 줄(문단·수동 줄바꿈) 단위로 같아야 하고, 렌더링한 PNG가 파일 미리보기로 연결돼 있어야 한다.
        /// 화면 폭에 따른 자동 줄바꿈은 템플릿이 정하므로 대조하지 않는다(데이터의 줄만 비교).
        /// 이미지에 박힌 글자라 {date:N} 같은 토큰은 쓸 수 없다. 의도적으로 둔 임시 캡처는 오류·경고가 아니라 제작 상태로 따로 알린다.
        /// </summary>
        static void CheckCaptures(System.Func<int, string> date,
            System.Action<string, Object> error, System.Action<string, Object> warn, System.Action<string, Object> info)
        {
            var specs = CaptureRenderer.FindAll();
            foreach (var g in specs.Where(c => c.record != null).GroupBy(c => c.record).Where(g => g.Count() > 1))
                error($"한 파일에 캡처 원본이 {g.Count()}개 있습니다: {string.Join(", ", g.Select(Path))}", g.First());

            foreach (var c in specs)
            {
                string where = Path(c);
                if (c.record == null)
                {
                    error($"캡처 원본의 파일 기록이 비어 있습니다: {where}", c);
                    continue;
                }
                if (c.style == null)
                {
                    error($"캡처 원본의 스타일이 비어 있습니다: {where}", c);
                    continue;
                }

                var shown = c.Lines().ToList();
                foreach (var line in shown.Where(TextTokens.HasTokens))
                    error($"캡처 글자에는 토큰({{date:N}} 등)을 쓸 수 없습니다(이미지에 박힌 글자): '{line}' — {where}", c);
                var expected = c.record.VisibleLines(date).ToList();
                if (!shown.SequenceEqual(expected))
                {
                    int i = 0;
                    while (i < shown.Count && i < expected.Count && shown[i] == expected[i])
                        i++;
                    string a = i < shown.Count ? shown[i] : "(없음)", b = i < expected.Count ? expected[i] : "(없음)";
                    error($"캡처에 보이는 글자와 파일 표시 문구가 다릅니다({i + 1}번째 줄: 캡처 '{a}' / 표시 문구 '{b}'): {where}", c);
                }

                if (c.output == null)
                    warn($"캡처를 아직 렌더링하지 않았습니다. Intersection/Content/Render Captures를 실행하세요: {where}", c);
                else
                {
                    if (c.record.preview != c.output)
                        warn($"파일 미리보기가 캡처 결과와 다릅니다. Intersection/Content/Render Captures를 실행하세요: {Path(c.record)}", c.record);
                    if (c.renderedHash != CaptureRenderer.Hash(c))
                        warn($"캡처 원본이 마지막 렌더링 뒤에 바뀌었습니다. Intersection/Content/Render Captures를 실행하세요: {where}", c);
                    if (Path(c.output) != CaptureRenderer.OutputPath(c))
                        warn($"캡처 결과 파일 이름이 제작 상태(임시 여부)와 맞지 않습니다: {Path(c.output)}", c);
                }

                if (c.temporary)
                    info($"임시 제작 자산(정식 미완료): {Path(c.record)} — {(string.IsNullOrWhiteSpace(c.temporaryNote) ? "사유 없음" : c.temporaryNote.Trim())}", c);
            }
        }
    }
}
