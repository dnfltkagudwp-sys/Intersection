using System.IO;
using System.Linq;
using Intersection.Core;
using Intersection.Data;
using Intersection.UI;
using UnityEditor;
using UnityEngine;

namespace Intersection.EditorTools
{
    /// <summary>
    /// 에디터 전용 진행 검수 창. 플레이 중에 접근 단계와 의뢰별 작업 상태를 바꿔 P3/P4 같은 이후 구간의 접근 범위를 확인한다.
    /// 단계·작업 상태만 바꾸는 버튼은 효과·알림을 실행하지 않고, `완료(효과)`는 이후 정식 콘텐츠가 할 작업 완료를 대신해 효과를 실행한다.
    /// 플레이 중이 아니면 진행 저장 파일만 지울 수 있다(새 게임). 플레이어 화면에는 단계 이름을 표시하지 않는다.
    /// </summary>
    public class ProgressDebugWindow : EditorWindow
    {
        Vector2 scroll;

        [MenuItem("Intersection/Progress/Progress Inspector")]
        static void Open() => GetWindow<ProgressDebugWindow>("진행 검수");

        [MenuItem("Intersection/Progress/Delete Progress Save (New Game)")]
        static void DeleteSave()
        {
            var config = AssetDatabase.FindAssets("t:GameConfig").Select(g => AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
            if (config == null)
                return;
            string path = Path.Combine(Application.persistentDataPath, config.progressSaveFileName);
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Progress] 플레이 중에는 진행 저장 파일을 지우지 않습니다. 진행 검수 창의 '진행 초기화'를 쓰세요.");
                return;
            }
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log($"[Progress] 진행 저장 파일을 지웠습니다: {path}");
            }
        }

        void OnInspectorUpdate() => Repaint();

        void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이 중에 사용합니다. 플레이 전에는 메뉴 Intersection/Progress/Delete Progress Save로 새 게임 상태를 만들 수 있습니다.", MessageType.Info);
                return;
            }
            var shell = Object.FindAnyObjectByType<AppShell>();
            if (shell == null || shell.Progress == null)
            {
                EditorGUILayout.HelpBox("AppShell을 찾지 못했습니다.", MessageType.Warning);
                return;
            }
            var progress = shell.Progress;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("접근 단계", progress.Stage.ToString(), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            foreach (ProgressStage s in System.Enum.GetValues(typeof(ProgressStage)))
            {
                GUI.enabled = s != progress.Stage;
                if (GUILayout.Button(s.ToString()))
                    shell.DebugSetStage(s);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("의뢰별 작업", EditorStyles.boldLabel);
            var db = AssetDatabase.FindAssets("t:ContentDatabase").Select(g => AssetDatabase.LoadAssetAtPath<ContentDatabase>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
            foreach (var job in db != null ? db.jobs.Where(j => j != null).OrderBy(j => j.device != null ? j.device.order : 0).ThenBy(j => j.order) : Enumerable.Empty<ProgressJobData>())
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{(job.device != null ? job.device.DisplayName : "?")} · {job.name}", GUILayout.MinWidth(160));
                EditorGUILayout.LabelField($"{progress.StateOf(job)} / 조건 {(progress.ConditionsMet(job) ? "충족" : "미충족")}", GUILayout.Width(150));
                if (GUILayout.Button("대기", GUILayout.Width(44)))
                    shell.DebugSetJob(job, JobState.NotStarted);
                if (GUILayout.Button("진행", GUILayout.Width(44)))
                    shell.DebugSetJob(job, JobState.Running);
                if (GUILayout.Button("완료(효과)", GUILayout.Width(80)))
                    shell.DebugCompleteJob(job);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("업무 알림", $"{progress.Store.Notifications.Count}건 · 읽지 않음 {progress.Store.UnreadCount}건");
            if (GUILayout.Button("진행 초기화 (시작 단계, 작업·알림 비움)"))
                shell.DebugResetProgress();
            EditorGUILayout.EndScrollView();
        }
    }
}
