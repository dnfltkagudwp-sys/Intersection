using System.Collections.Generic;

namespace Intersection.Data
{
    /// <summary>현지화할 수 있는 콘텐츠 문구 하나: 조회 키(소유 불변 ID + 필드 키)와 에셋의 원문.</summary>
    public readonly struct ContentField
    {
        public readonly string ownerId;
        public readonly string field;
        public readonly string original;

        public ContentField(string ownerId, string field, string original)
        {
            this.ownerId = ownerId;
            this.field = field;
            this.original = original;
        }

        public string Key => ContentText.Key(ownerId, field);
    }

    /// <summary>
    /// 콘텐츠 원문 현지화. 에셋의 한국어 원문이 기본값이고, 현재 언어의 콘텐츠 테이블(ContentLocaleTable)에
    /// `소유 불변 ID:필드 키` 값이 있으면 그것으로 덮어쓴다. 테이블이 없거나(기본 언어) 값이 비어 있으면 원문을 그대로 돌려준다.
    /// 목록·상세·미리보기·검색·비교·작업함이 모두 데이터 에셋의 표시용 접근자(예: <c>BrowserRecord.Title</c>)를 거쳐 이 값을 쓴다.
    /// UI 공통 문구는 여기가 아니라 StringTable(UIText)이 담당한다.
    /// </summary>
    public static class ContentText
    {
        /// <summary>필드 키. 데이터 에셋에 저장되는 값이 아니라 조회 키의 일부이므로 바꾸면 기존 번역이 끊긴다.</summary>
        public static class Field
        {
            public const string Title = "title";
            public const string Body = "body";
            public const string FileName = "fileName";
            public const string VisibleText = "visibleText";
            public const string PlaceLabel = "placeLabel";
            public const string RouteFrom = "routeFrom";
            public const string RouteTo = "routeTo";
            public const string RouteNote = "routeNote";
            public const string CategoryLabel = "categoryLabel";
            public const string ItemLabel = "itemLabel";
            public const string ValueBefore = "valueBefore";
            public const string ValueAfter = "valueAfter";
            public const string GroupTitle = "groupTitle";
            public const string FullName = "fullName";
            public const string DisplayName = "displayName";
            public const string UnsavedContactLabel = "unsavedContactLabel";
            public const string TargetLabel = "targetLabel";
            public const string Hint = "hint";

            /// <summary>파일 미리보기 블록: visibleText:{blockId}</summary>
            public static string Block(string blockId) => VisibleText + ":" + blockId;

            /// <summary>메시지 본문: message:{messageId}:text (소유는 대화 ID)</summary>
            public static string MessageText(string messageId) => "message:" + messageId + ":text";

            /// <summary>메시지 첨부 표시 문구: message:{messageId}:attachmentLabel (소유는 대화 ID)</summary>
            public static string MessageAttachmentLabel(string messageId) => "message:" + messageId + ":attachmentLabel";

            /// <summary>주소록 저장명: contact:{personId}:savedName (소유는 의뢰 ID)</summary>
            public static string ContactName(string personId) => "contact:" + personId + ":savedName";
        }

        static ContentLocaleTable current;

        /// <summary>지금 쓰는 콘텐츠 테이블. null이면 기본 언어(에셋 원문).</summary>
        public static ContentLocaleTable Current => current;

        /// <summary>현재 언어를 바꾼다. 기본 언어(원문)로 돌아가려면 null.</summary>
        public static void Use(ContentLocaleTable table) => current = table;

        public static string Key(string ownerId, string field) => ownerId + ":" + field;

        /// <summary>현재 언어의 값. 번역이 없거나 비어 있으면 원문(null이면 null) 그대로.</summary>
        public static string Get(string ownerId, string field, string original)
        {
            if (current == null || string.IsNullOrEmpty(ownerId))
                return original;
            return current.TryGet(Key(ownerId, field), out var value) && !string.IsNullOrEmpty(value) ? value : original;
        }

        /// <summary>
        /// 원문이 비어 있지 않은 필드만 목록에 넣는다(검증·누락 현황용). 빈 칸은 화면에서 “정해지지 않음”의 뜻이라
        /// 번역 대상이 아니며, 검증 도구가 빈 원문에 대한 번역 키를 오류로 잡는다.
        /// </summary>
        public static void Add(List<ContentField> list, string ownerId, string field, string original)
        {
            if (!string.IsNullOrEmpty(original))
                list.Add(new ContentField(ownerId, field, original));
        }
    }
}
