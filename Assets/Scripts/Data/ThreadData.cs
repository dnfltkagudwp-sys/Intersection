using System;
using System.Collections.Generic;
using UnityEngine;

namespace Intersection.Data
{
    /// <summary>기기별 증거 참조. 같은 원본을 A폰에서는 A03, B폰에서는 B03처럼 다르게 참조한다.</summary>
    [Serializable]
    public class DeviceRef
    {
        public CaseData device;

        [Tooltip("증거 마스터표 ID (예: A03, A12). 휴대전화 안에는 표시하지 않는다.")]
        public string evidenceRef;
    }

    [Serializable]
    public class MessageData
    {
        [SerializeField, Tooltip("불변 메시지 ID. 자동 발급되며 손으로 수정하지 않는다.")]
        string id;

        public PersonData sender;

        [Tooltip("사망일 기준 상대 날짜와 분 단위 시각. 메시지마다 직접 저장한다.")]
        public RelativeTime time;

        [Tooltip("같은 시각 메시지 사이의 표시 순서. 작을수록 먼저 표시된다.")]
        public int sortKey;

        [TextArea(1, 6)]
        public string body;

        public AttachmentKind attachment;

        [Tooltip("첨부 자산의 불변 ID. 자산이 정해지지 않았으면 비워 둔다.")]
        public string mediaAssetId;

        [Tooltip("위치 공유 장소명 등 첨부에 표시할 문구. 미정이면 비워 둔다.")]
        public string attachmentLabel;

        public string Id => id;

        public bool EnsureId()
        {
            if (!string.IsNullOrEmpty(id))
                return false;
            id = ContentAsset.NewId();
            return true;
        }

        public void RegenerateId() => id = ContentAsset.NewId();
    }

    /// <summary>같은 시점에 묶인 대화 구간. 메시지는 구간 안에 둔다.</summary>
    [Serializable]
    public class SegmentData
    {
        [SerializeField, Tooltip("불변 구간 ID. 자동 발급되며 손으로 수정하지 않는다.")]
        string id;

        [Tooltip("작가용 메모. 화면에 표시하지 않는다.")]
        public string authorNote;

        [Tooltip("이 구간에만 붙는 증거 참조 (예: 사건 당일 구간의 A12/B15)")]
        public List<DeviceRef> evidenceRefs = new List<DeviceRef>();

        public List<MessageData> messages = new List<MessageData>();

        public string Id => id;

        public bool EnsureId()
        {
            if (!string.IsNullOrEmpty(id))
                return false;
            id = ContentAsset.NewId();
            return true;
        }

        public void RegenerateId() => id = ContentAsset.NewId();
    }

    /// <summary>이 대화가 특정 기기에서 어떻게 보이는지. 두 기기가 같은 원본 대화를 공유한다.</summary>
    [Serializable]
    public class DeviceThreadState
    {
        public CaseData device;

        [Tooltip("이 기기 기준 증거 마스터표 ID (예: A03 / B03). 일반 대화는 비운다.")]
        public string evidenceRef;

        public RecordSource source = RecordSource.Local;

        [Tooltip("서비스 표시 문자열 키 (예: service.sms)")]
        public string serviceKey;

        [Tooltip("이 기기에서 이 대화에 접근 가능한 첫 단계")]
        public ProgressStage availableFrom = ProgressStage.P0;

        [Tooltip("이 기기에서 알림을 끈 상태")]
        public bool muted;

        [Tooltip("읽지 않은 메시지 수. 실제 상태 데이터가 있을 때만 0보다 크게 둔다.")]
        public int unreadCount;

        [Tooltip("무결성·복구 상태 문자열 키 (예: integrity.original)")]
        public string integrityKey;
    }

    /// <summary>
    /// 대화 원본. A03/B03처럼 두 기기가 공유하는 대화도 한 에셋으로 만든다.
    /// 목록 미리보기·정렬·개수는 이 데이터에서 계산하며 따로 저장하지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Intersection/Message Thread", fileName = "Thread")]
    public class ThreadData : ContentAsset
    {
        [Tooltip("단체방 제목. 1:1 대화는 비운다(상대 이름은 각 기기 주소록에서 가져온다).")]
        public string groupTitle;

        public List<PersonData> participants = new List<PersonData>();

        [Tooltip("이 대화가 존재하는 기기와 기기별 상태")]
        public List<DeviceThreadState> devices = new List<DeviceThreadState>();

        public List<SegmentData> segments = new List<SegmentData>();

        public bool IsGroup => !string.IsNullOrEmpty(groupTitle);

        public IEnumerable<MessageData> AllMessages
        {
            get
            {
                foreach (var s in segments)
                {
                    if (s?.messages == null)
                        continue;
                    foreach (var m in s.messages)
                    {
                        if (m != null)
                            yield return m;
                    }
                }
            }
        }

        public DeviceThreadState StateFor(CaseData device)
        {
            foreach (var d in devices)
            {
                if (d != null && d.device == device)
                    return d;
            }
            return null;
        }

        public override bool EnsureIds()
        {
            bool changed = base.EnsureIds();
            var seen = new HashSet<string>();
            foreach (var s in segments)
            {
                if (s == null)
                    continue;
                changed |= s.EnsureId();
                // 인스펙터에서 항목을 복제하면 ID가 함께 복사된다. 뒤에 있는 복제본에만 새 ID를 준다.
                if (!seen.Add(s.Id))
                {
                    s.RegenerateId();
                    seen.Add(s.Id);
                    changed = true;
                }
                if (s.messages == null)
                    continue;
                foreach (var m in s.messages)
                {
                    if (m == null)
                        continue;
                    changed |= m.EnsureId();
                    if (!seen.Add(m.Id))
                    {
                        m.RegenerateId();
                        seen.Add(m.Id);
                        changed = true;
                    }
                }
            }
            return changed;
        }
    }
}
