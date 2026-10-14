using System;
using System.Collections.Generic;
using UnityEngine;

namespace Intersection.Data
{
    [Serializable]
    public class ContactEntry
    {
        public PersonData person;

        [Tooltip("이 기기의 주소록에 저장된 이름. 관계 분류(친구·가족)가 아니라 실제 저장명을 쓴다.")]
        public string savedName;
    }

    /// <summary>
    /// 하나의 의뢰 = 한 사망자의 기기. 주소록·접근 시점·표시명을 가진다.
    /// </summary>
    [CreateAssetMenu(menuName = "Intersection/Case (Device)", fileName = "Case")]
    public class CaseData : ContentAsset
    {
        [Tooltip("좌측 의뢰 목록의 표시 순서")]
        public int order;

        [Tooltip("기기 소유자(사망자)")]
        public PersonData owner;

        [Tooltip("의뢰 표시명. 비우면 소유자 이름을 쓴다.")]
        public string displayNameOverride;

        [Tooltip("이 의뢰의 기기 자료에 접근 가능한 첫 단계")]
        public ProgressStage availableFrom = ProgressStage.P0;

        [Tooltip("이 기기에서 주소록에 없는 상대를 표시하는 문구 (예: 저장되지 않은 번호 / 모르는 번호)")]
        public string unsavedContactLabel;

        [Tooltip("이 기기의 주소록")]
        public List<ContactEntry> contacts = new List<ContactEntry>();

        [Tooltip("로컬 자료 인덱싱이 끝난 단계. 이 단계 전에는 상단 바에 인덱싱 중으로 표시된다.")]
        public ProgressStage localIndexedFrom = ProgressStage.P0;

        /// <summary>의뢰 표시명 (현재 언어): 표시명이 있으면 그것, 없으면 소유자 이름.</summary>
        public string DisplayName =>
            !string.IsNullOrEmpty(displayNameOverride) ? ContentText.Get(Id, ContentText.Field.DisplayName, displayNameOverride)
            : owner != null ? owner.FullName : name;

        /// <summary>주소록에 없는 상대 표시 문구 (현재 언어).</summary>
        public string UnsavedContactLabel => ContentText.Get(Id, ContentText.Field.UnsavedContactLabel, unsavedContactLabel);

        /// <summary>이 기기 주소록의 저장명 (현재 언어). 없으면 null.</summary>
        public string SavedNameOf(PersonData person)
        {
            if (person == null)
                return null;
            foreach (var c in contacts)
            {
                if (c != null && c.person == person && !string.IsNullOrEmpty(c.savedName))
                    return ContentText.Get(Id, ContentText.Field.ContactName(person.Id), c.savedName);
            }
            return null;
        }

        public override IEnumerable<ContentField> ContentFields()
        {
            var list = new List<ContentField>();
            ContentText.Add(list, Id, ContentText.Field.DisplayName, displayNameOverride);
            ContentText.Add(list, Id, ContentText.Field.UnsavedContactLabel, unsavedContactLabel);
            foreach (var c in contacts)
            {
                if (c != null && c.person != null)
                    ContentText.Add(list, Id, ContentText.Field.ContactName(c.person.Id), c.savedName);
            }
            return list;
        }

        public bool IsAvailable(ProgressStage stage) => stage >= availableFrom;
    }
}
