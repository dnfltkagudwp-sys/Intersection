using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Intersection.Core;
using Intersection.Data;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Intersection.EditorTools
{
    /// <summary>진행 연동(UI-07) 검증: 작업 데이터·효과·도달 가능성·출처와 접근 단계·검색 인덱스·진행 저장 파일·삭제 대기 조회.</summary>
    public static partial class ContentValidator
    {
        static readonly ProgressStage[] Stages = (ProgressStage[])Enum.GetValues(typeof(ProgressStage));

        static void CheckProgress(GameConfig config, List<ProgressJobData> jobs, List<CaseData> cases, List<ThreadData> threads,
            List<PhotoData> photos, List<DeviceRecord> records, List<FolderData> folders, List<WorkRequestData> requests,
            Action<string, Object> error, Action<string, Object> warn, Action<string, string, Object> checkKey)
        {
            var db = config.database;
            var text = new UIText(config.strings);
            var time = new PhoneTime(config, text);

            // ── 작업 데이터: 참조·문구 키·효과 ID
            var effectIds = new Dictionary<string, string>();
            foreach (var job in jobs)
            {
                string where = Path(job);
                if (job.device == null)
                    error($"진행 작업의 의뢰가 비어 있습니다: {where}", job);
                else if (!cases.Contains(job.device))
                    error($"진행 작업이 존재하지 않는 의뢰를 가리킵니다: {where}", job);
                if (job.request == null)
                    error($"진행 작업의 시작 요청이 비어 있습니다: {where}", job);
                else if (!requests.Contains(job.request))
                    error($"진행 작업이 존재하지 않는 요청을 가리킵니다: {where}", job);
                else if (job.request.device != job.device)
                    error($"진행 작업의 요청이 다른 의뢰의 요청입니다: {where}", job);
                foreach (var (key, field) in new[]
                         {
                             (job.actionKey, "actionKey"), (job.notReadyKey, "notReadyKey"), (job.runningKey, "runningKey"),
                             (job.doneKey, "doneKey"), (job.shortRunningKey, "shortRunningKey"), (job.shortDoneKey, "shortDoneKey"),
                         })
                {
                    if (string.IsNullOrEmpty(key))
                        error($"진행 작업의 {field}가 비어 있습니다: {where}", job);
                    else
                        checkKey(key, $"진행 작업 {field} ({where})", job);
                }

                foreach (var c in job.startConditions)
                {
                    if (c == null)
                    {
                        error($"비어 있는 시작 조건이 있습니다: {where}", job);
                        continue;
                    }
                    switch (c.kind)
                    {
                        case ProgressConditionKind.TutorialCompleted:
                            if (c.tutorial == null)
                                error($"시작 조건의 튜토리얼이 비어 있습니다(삭제된 대상): {where}", job);
                            break;
                        case ProgressConditionKind.RequestViewed:
                            if (c.request == null)
                                error($"시작 조건의 요청이 비어 있습니다(삭제된 대상): {where}", job);
                            break;
                        case ProgressConditionKind.ClassifiedRecords:
                            if (c.minCount < 1)
                                error($"시작 조건의 최소 분류 개수는 1 이상이어야 합니다: {where}", job);
                            break;
                        case ProgressConditionKind.JobCompleted:
                            if (c.job == null)
                                error($"시작 조건의 작업이 비어 있습니다(삭제된 대상): {where}", job);
                            else if (c.job == job)
                                error($"작업이 자기 자신의 완료를 시작 조건으로 씁니다: {where}", job);
                            break;
                    }
                }

                foreach (var e in job.AllEffects)
                {
                    if (e == null)
                    {
                        error($"비어 있는 진행 효과가 있습니다: {where}", job);
                        continue;
                    }
                    CheckInnerId(e.Id, "진행 효과", where, job, effectIds, error);
                    switch (e.kind)
                    {
                        case ProgressEffectKind.RaiseStage:
                            if (!Enum.IsDefined(typeof(ProgressStage), e.stage))
                                error($"진행 효과의 접근 단계가 올바르지 않습니다: {where}", job);
                            break;
                        case ProgressEffectKind.CompleteJob:
                            if (e.job == null)
                                error($"진행 효과의 완료할 작업이 비어 있습니다(삭제된 대상): {where}", job);
                            else if (!jobs.Contains(e.job))
                                error($"진행 효과가 존재하지 않는 작업을 가리킵니다: {where}", job);
                            break;
                        case ProgressEffectKind.Notify:
                            CheckTemplate(config, e, where, job, error);
                            break;
                    }
                }
            }

            // ── 순환: 작업 완료가 서로를 완료시키는 고리
            foreach (var job in jobs)
            {
                if (CompletionReaches(job, job, new HashSet<ProgressJobData>()))
                    error($"작업 완료 효과가 순환합니다: {Path(job)}", job);
            }

            // ── 도달 가능성: 작업 완료 경로, 교착(서로의 시작·완료를 기다림), 데이터가 쓰는 접근 단계
            foreach (var job in jobs)
            {
                var completers = jobs.Where(o => o != job && o.AllEffects.Any(e => e != null && e.kind == ProgressEffectKind.CompleteJob && e.job == job)).ToList();
                bool needed = jobs.Any(o => o.startConditions.Any(c => c != null && c.kind == ProgressConditionKind.JobCompleted && c.job == job))
                              || job.onComplete.Any(e => e != null);
                if (needed && !job.completedExternally && completers.Count == 0)
                    error($"작업을 완료시키는 경로가 없습니다 (다른 작업의 완료 효과도, completedExternally도 없음): {Path(job)}", job);
                foreach (var c in job.startConditions.Where(c => c != null && c.kind == ProgressConditionKind.JobCompleted && c.job != null))
                {
                    var dep = c.job;
                    var depCompleters = jobs.Where(o => o != job && o.AllEffects.Any(e => e != null && e.kind == ProgressEffectKind.CompleteJob && e.job == dep)).ToList();
                    if (!dep.completedExternally && depCompleters.Count > 0 && depCompleters.All(o => o == job))
                        error($"교착: {Path(job)}은 {Path(dep)} 완료를 기다리지만, 그 작업은 이 작업이 시작돼야만 완료됩니다.", job);
                }
            }
            var reachable = new HashSet<ProgressStage> { config.startStage };
            foreach (var e in jobs.SelectMany(j => j.AllEffects).Where(e => e != null && e.kind == ProgressEffectKind.RaiseStage))
                reachable.Add(e.stage);
            ProgressStage maxReachable = reachable.Max();
            var usedStages = cases.Select(c => (c.availableFrom, (Object)c))
                .Concat(records.Select(r => (r.availableFrom, (Object)r)))
                .Concat(photos.Select(p => (p.availableFrom, (Object)p)))
                .Concat(folders.Select(f => (f.availableFrom, (Object)f)))
                .Concat(requests.Select(q => (q.availableFrom, (Object)q)))
                .Concat(threads.SelectMany(t => t.devices.Where(d => d != null).Select(d => (d.availableFrom, (Object)t))));
            foreach (var g in usedStages.Where(u => u.availableFrom > maxReachable).GroupBy(u => u.availableFrom))
                error($"{g.Key} 단계에 도달하는 진행 효과가 없어 {g.Count()}개 자료에 접근할 수 없습니다: {Path(g.First().Item2)}", g.First().Item2);

            // ── 출처와 접근 단계: 로컬은 기기 등록 때부터, 클라우드·연동·복구는 그 출처를 여는 작업이 끝난 뒤부터
            void CheckSource(CaseData device, RecordSource source, ProgressStage availableFrom, string what, Object ctx)
            {
                if (device == null)
                    return;
                if (source == RecordSource.Local)
                {
                    if (availableFrom != device.availableFrom)
                        error($"로컬 기록의 접근 단계({availableFrom})가 기기 등록 단계({device.availableFrom})와 다릅니다 — 로컬 자료는 기기 등록 때부터 존재해야 합니다: {what}", ctx);
                    return;
                }
                var openers = jobs.Where(j => j.device == device && j.unlocksSources.Contains(source)).ToList();
                if (openers.Count == 0)
                {
                    error($"{source} 출처 기록인데 이 의뢰에 그 출처를 여는 진행 작업이 없습니다: {what}", ctx);
                    return;
                }
                var opened = openers.SelectMany(j => j.onComplete).Where(e => e != null && e.kind == ProgressEffectKind.RaiseStage)
                    .Select(e => e.stage).DefaultIfEmpty(ProgressStage.P6).Min();
                if (availableFrom < opened)
                    error($"{source} 출처 기록의 접근 단계({availableFrom})가 그 출처를 여는 작업이 끝나는 단계({opened})보다 이릅니다: {what}", ctx);
            }
            foreach (var r in records)
                CheckSource(r.device, r.source, r.availableFrom, Path(r), r);
            foreach (var p in photos)
                CheckSource(p.device, p.source, p.availableFrom, Path(p), p);
            foreach (var f in folders)
                CheckSource(f.device, f.source, f.availableFrom, Path(f), f);
            foreach (var t in threads)
            {
                foreach (var d in t.devices.Where(d => d != null && d.device != null))
                    CheckSource(d.device, d.source, d.availableFrom, $"{Path(t)} ({d.device.name})", t);
            }

            // ── 검색 인덱스·앱 목록: 각 단계에서 접근 전 기록이 섞이지 않는지, 결과가 열 수 있는 앱·원본을 가리키는지
            foreach (var stage in Stages)
            {
                foreach (var hit in GlobalSearch.Searchable(db, stage, text, time))
                {
                    var access = RecordAccess.Check(hit.target, db, stage, text, out var res);
                    if (access != AccessState.Accessible)
                        error($"{stage} 검색 인덱스에 열 수 없는 기록이 있습니다({access}): {hit.target.Key}", null);
                    if (hit.app.HasValue && (config.apps == null || !config.apps.apps.Any(a => a != null && a.kind == hit.app.Value && a.implemented)))
                        error($"검색 결과가 등록되지 않았거나 구현되지 않은 앱({hit.app.Value})을 가리킵니다: {hit.target.Key}", null);
                    if (hit.target.kind == RecordKind.Photo)
                    {
                        var all = PhotoQuery.DevicePhotos(db, hit.device, stage);
                        var photo = all.FirstOrDefault(p => p.Id == hit.target.recordId);
                        if (photo != null && !PhotoQuery.Albums(db, hit.device).Any(a => PhotoQuery.AlbumPhotos(a, all).Contains(photo)))
                            error($"검색 결과 사진이 어떤 앨범에도 없어 원본을 열 수 없습니다: {Path(photo)}", photo);
                    }
                }
                foreach (var device in cases)
                {
                    bool leak = PhotoQuery.DevicePhotos(db, device, stage).Any(p => p.availableFrom > stage)
                                || RecordQuery.For(db.browser, device, stage).Any(r => r.availableFrom > stage)
                                || RecordQuery.For(db.maps, device, stage).Any(r => r.availableFrom > stage)
                                || RecordQuery.For(db.files, device, stage).Any(r => r.availableFrom > stage)
                                || RecordQuery.For(db.settings, device, stage).Any(r => r.availableFrom > stage)
                                || DeviceQuery.Threads(db, device, stage).Any(e => e.state.availableFrom > stage);
                    if (leak)
                        error($"{stage}에서 {device.name}의 앱 목록·개수·위치에 접근 전 기록이 포함됩니다.", device);
                }
            }

            // ── 진행 저장 파일: 사라진 작업·의뢰·알림 문구 참조
            string progressPath = System.IO.Path.Combine(Application.persistentDataPath, config.progressSaveFileName);
            if (System.IO.File.Exists(progressPath))
            {
                var progress = new ProgressStore(progressPath, config.startStage);
                foreach (var id in progress.JobIds.Where(id => !jobs.Any(j => j.Id == id)))
                    warn($"진행 저장 파일에 원본에 없는 작업 ID가 있습니다: {id} ({progressPath})", null);
                foreach (var n in progress.Notifications)
                {
                    if (!string.IsNullOrEmpty(n.caseId) && !cases.Any(c => c.Id == n.caseId))
                        warn($"진행 저장 파일의 알림이 원본에 없는 의뢰를 가리킵니다: {n.caseId} ({progressPath})", null);
                    if (config.strings != null && !config.strings.Contains(n.templateKey))
                        warn($"진행 저장 파일의 알림 문구 키가 문자열 테이블에 없습니다: {n.templateKey} ({progressPath})", null);
                }
            }

            // ── 삭제 대기 조회: 삭제 후보의 누락·중복 원본 (원본은 바꾸지 않는다)
            string workPath = System.IO.Path.Combine(Application.persistentDataPath, config.workSaveFileName);
            if (System.IO.File.Exists(workPath))
            {
                var work = new WorkStore(workPath);
                foreach (var e in DeletionQueue.Compute(work, db, ProgressStage.P6, text))
                {
                    if (e.state == AccessState.Missing || e.state == AccessState.Duplicate)
                        warn($"삭제 대기 조회에 원본을 {(e.state == AccessState.Duplicate ? "하나로 정할 수 없는(ID 중복)" : "찾을 수 없는")} 항목이 있습니다: {e.target.Key} ({workPath})", null);
                }
            }
        }

        static bool CompletionReaches(ProgressJobData from, ProgressJobData target, HashSet<ProgressJobData> visited)
        {
            foreach (var e in from.onComplete.Where(e => e != null && e.kind == ProgressEffectKind.CompleteJob && e.job != null))
            {
                if (e.job == target)
                    return true;
                if (visited.Add(e.job) && CompletionReaches(e.job, target, visited))
                    return true;
            }
            return false;
        }

        static readonly Regex Token = new Regex(@"\{([^{}]+)\}");

        /// <summary>알림 문구 키가 있고, 쓸 수 있는 토큰만 쓰며, 필요한 토큰 값(의뢰)이 지정됐는지.</summary>
        static void CheckTemplate(GameConfig config, ProgressEffect e, string where, Object ctx, Action<string, Object> error)
        {
            if (string.IsNullOrEmpty(e.templateKey))
            {
                error($"알림 효과의 문구 키가 비어 있습니다: {where}", ctx);
                return;
            }
            if (config.strings == null || !config.strings.TryGet(e.templateKey, out var template))
            {
                error($"알림 문구 키 '{e.templateKey}'가 문자열 테이블에 없습니다: {where}", ctx);
                return;
            }
            foreach (Match m in Token.Matches(template))
            {
                string token = m.Groups[1].Value;
                if (!UIKeys.NotificationTokens.Contains(token))
                    error($"알림 문구 '{e.templateKey}'에 지원하지 않는 토큰 {{{token}}}이 있습니다: {where}", ctx);
                else if ((token == "caseDisplayName" || token == "count") && e.caseToken == null)
                    error($"알림 문구 '{e.templateKey}'의 {{{token}}}에 쓸 의뢰(caseToken)가 비어 있습니다: {where}", ctx);
            }
        }
    }
}
