using UnityEngine;

namespace Intersection.UI
{
    public static class UIPool
    {
        /// <summary>
        /// 다시 그릴 때 이전 항목을 지운다. Destroy는 프레임 끝에 실행되므로
        /// 먼저 비활성화해 같은 프레임의 레이아웃 계산에서 빠지게 한다.
        /// </summary>
        public static void Discard(GameObject go)
        {
            go.SetActive(false);
            Object.Destroy(go);
        }
    }
}
