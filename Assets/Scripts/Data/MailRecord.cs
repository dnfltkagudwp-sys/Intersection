using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Intersection.Data
{
    /// <summary>
    /// 메일 앱의 메일 한 통 (읽기 전용 기록). 작성·답장·전달·삭제·첨부 다운로드는 없다.
    /// time은 받은 시각(임시 보관 메일은 마지막 저장 시각)이다. 메일함은 문자열 키로 목록의 구역을 나눈다.
    /// </summary>
    [CreateAssetMenu(menuName = "Intersection/Records/Mail", fileName = "Mail")]
    public class MailRecord : DeviceRecord
    {
        [Tooltip("메일함 문자열 키 (예: mail.box.inbox / mail.box.drafts). 목록 구역 제목이 된다.")]
        public string mailboxKey = "mail.box.inbox";

        [Tooltip("보낸 사람 표시명 (서비스 이름 등). 비우면 주소만 보인다.")]
        public string fromName;

        [Tooltip("보낸 사람 주소. 주소·계정은 번역하지 않는다. 가린 주소는 가린 그대로 적는다.")]
        public string fromAddress;

        [Tooltip("받는 사람 문자열 키 (예: mail.to.me). 받는 사람이 문구로 정해지면 toText를 쓴다. 둘 다 비면 `(비어 있음)`.")]
        public string toKey;

        [Tooltip("받는 사람을 문구로 적을 때만 (주소·표시명). toKey보다 우선한다.")]
        public string toText;

        [Tooltip("제목. 비우면 `(제목 없음)`으로 보인다.")]
        public string subject;

        [Tooltip("본문. 플레이어가 메일 상세에서 읽는 문구만 넣는다(파일 미리보기와 같은 블록 형식). 시각·토큰을 쓰지 않는다.")]
        public List<FileTextBlock> body = new List<FileTextBlock>();

        [Tooltip("첨부 표시 줄 (예: 자료_2.zip · 링크). 내려받기 기능은 없다.")]
        public List<FileTextBlock> attachments = new List<FileTextBlock>();

        [Tooltip("상태 문자열 키 (예: mail.status.unsent). 일반 메일은 비운다.")]
        public string statusKey;

        public const string FieldSubject = "subject";
        public const string FieldFromName = "fromName";
        public const string FieldToText = "toText";

        /// <summary>본문 블록 번역 키: body:{blockId}</summary>
        public static string BodyField(string blockId) => "body:" + blockId;

        /// <summary>첨부 줄 번역 키: attachment:{blockId}</summary>
        public static string AttachmentField(string blockId) => "attachment:" + blockId;

        public string Subject => ContentText.Get(Id, FieldSubject, subject);
        public string FromName => ContentText.Get(Id, FieldFromName, fromName);
        public string ToText => ContentText.Get(Id, FieldToText, toText);

        /// <summary>본문 블록 (현재 언어, 빈 블록 제외).</summary>
        public IReadOnlyList<FileTextBlock> Body => Localized(body, BodyField);

        /// <summary>첨부 줄 (현재 언어, 빈 줄 제외).</summary>
        public IEnumerable<string> AttachmentLines => Localized(attachments, AttachmentField).SelectMany(b => b.Lines());

        /// <summary>검색 대상 본문 줄 (현재 언어).</summary>
        public IEnumerable<string> BodyLines => Body.SelectMany(b => b.DisplayLines());

        List<FileTextBlock> Localized(List<FileTextBlock> blocks, System.Func<string, string> field) =>
            blocks == null ? new List<FileTextBlock>()
                : blocks.Where(b => b != null && !b.IsEmpty)
                    .Select(b => new FileTextBlock { kind = b.kind, text = ContentText.Get(Id, field(b.Id), b.text) })
                    .Where(b => !b.IsEmpty)
                    .ToList();

        public override IEnumerable<ContentField> ContentFields()
        {
            var list = new List<ContentField>();
            ContentText.Add(list, Id, FieldSubject, subject);
            ContentText.Add(list, Id, FieldFromName, fromName);
            ContentText.Add(list, Id, FieldToText, toText);
            foreach (var b in (body ?? new List<FileTextBlock>()).Where(b => b != null && !b.IsEmpty))
                ContentText.Add(list, Id, BodyField(b.Id), b.text);
            foreach (var b in (attachments ?? new List<FileTextBlock>()).Where(b => b != null && !b.IsEmpty))
                ContentText.Add(list, Id, AttachmentField(b.Id), b.text);
            return list;
        }

        /// <summary>본문·첨부 블록 ID를 발급한다. 두 목록 전체에서 겹치지 않게 한다(복제한 블록은 새 ID).</summary>
        public override bool EnsureIds()
        {
            bool changed = base.EnsureIds();
            var seen = new HashSet<string>();
            foreach (var b in (body ?? new List<FileTextBlock>()).Concat(attachments ?? new List<FileTextBlock>()))
            {
                if (b == null)
                    continue;
                changed |= b.EnsureId();
                if (!seen.Add(b.Id))
                {
                    b.RegenerateId();
                    seen.Add(b.Id);
                    changed = true;
                }
            }
            return changed;
        }

        public override void RegenerateIds()
        {
            base.RegenerateIds();
            foreach (var b in (body ?? new List<FileTextBlock>()).Concat(attachments ?? new List<FileTextBlock>()))
                b?.RegenerateId();
        }
    }
}
