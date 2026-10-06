using System;
using System.Collections.Generic;
using UnityEngine;

namespace Intersection.Data
{
    [Serializable]
    public class AppDefinition
    {
        public AppKind kind;

        [Tooltip("앱 표시명 문자열 키")]
        public string nameKey;

        [Tooltip("좌측 앱 목록 표시 순서")]
        public int order;

        public Sprite icon;

        [Tooltip("화면 구현이 끝난 앱만 켠다. 꺼진 앱은 비활성으로 표시된다.")]
        public bool implemented;
    }

    /// <summary>좌측 앱 목록의 표시명·순서·아이콘·활성 여부.</summary>
    [CreateAssetMenu(menuName = "Intersection/UI/App Registry", fileName = "Apps")]
    public class AppRegistry : ScriptableObject
    {
        public List<AppDefinition> apps = new List<AppDefinition>();
    }
}
