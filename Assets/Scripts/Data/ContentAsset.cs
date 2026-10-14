using System;
using System.Collections.Generic;
using UnityEngine;

namespace Intersection.Data
{
    /// <summary>
    /// 불변 원본 ID를 가진 콘텐츠 에셋의 기반 클래스.
    /// ID는 생성 시 한 번 발급되고 이름·순서·경로가 바뀌어도 유지된다.
    /// </summary>
    public abstract class ContentAsset : ScriptableObject
    {
        [SerializeField, Tooltip("불변 원본 ID. 자동 발급되며 손으로 수정하지 않는다.")]
        string id;

        public string Id => id;

        public static string NewId() => Guid.NewGuid().ToString("N");

        /// <summary>비어 있는 ID를 발급한다. 변경이 있었으면 true.</summary>
        public virtual bool EnsureIds()
        {
            if (!string.IsNullOrEmpty(id))
                return false;
            id = NewId();
            return true;
        }

        /// <summary>
        /// 복제된 에셋이 원본과 같은 ID를 갖게 됐을 때만 사용한다.
        /// 내부 항목(구간·메시지 등)을 가진 에셋은 그 ID도 함께 새로 발급한다.
        /// </summary>
        public virtual void RegenerateIds() => id = NewId();

        /// <summary>
        /// 이 에셋이 화면에 보여주는 현지화 대상 원문 (비어 있지 않은 것만). 키는 표시용 접근자가 쓰는 것과 같다.
        /// 검증 도구가 번역 테이블의 키가 실제 필드를 가리키는지, 언어별 누락이 얼마인지 계산할 때 쓴다.
        /// </summary>
        public virtual IEnumerable<ContentField> ContentFields() => System.Array.Empty<ContentField>();

        protected virtual void OnValidate()
        {
            if (EnsureIds())
                MarkDirty();
        }

        protected void MarkDirty()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
