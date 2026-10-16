using System.Collections.Generic;
using System.Linq;
using Intersection.Data;
using UnityEditor;
using UnityEngine;

namespace Intersection.EditorTools
{
    /// <summary>P6 확정 원고의 연락·작업·처리 묶음을 재생성 가능한 데이터 에셋으로 만든다.</summary>
    public static class P6ContentBuilder
    {
        const string Root = "Assets/Data/Finale";

        [MenuItem("Intersection/Content/Build P6 Finale")]
        public static void Build()
        {
            EnsureFolder(Root);
            var caseA = LoadAt<CaseData>("Assets/Data/Cases/Case_A.asset");
            var caseB = LoadAt<CaseData>("Assets/Data/Cases/Case_B.asset");
            var review = LoadAt<ProgressJobData>("Assets/Data/Progress/Job_B_CaseReview.asset");
            var familyPhoto = LoadAt<PhotoData>("Assets/Data/Photos/A_IMG_5798.asset");
            var archive = LoadAt<FileRecord>("Assets/Data/Files/A_A13-1.asset");

            var requestB = Asset<WorkRequestData>($"{Root}/Request_B_FamilyFollowup.asset");
            requestB.device = caseB;
            requestB.title = "유족 추가 요청";
            requestB.body = "검토하신 내용은 따로 설명해 주지 않으셔도 됩니다.\n처음 요청드린 대로 개인 계정과 온라인 기록은 모두 정리해 주세요.\n추가 비용이 발생하면 말씀해 주세요.";
            requestB.order = 1;
            requestB.availableFrom = ProgressStage.P6;
            requestB.evidenceRef = "F-B";

            var requestA = Asset<WorkRequestData>($"{Root}/Request_A_FamilyQuestion.asset");
            requestA.device = caseA;
            requestA.title = "유족 문의";
            requestA.body = "작업하시면서 혹시 우리 아이가 왜 거기 있었는지 알 수 있는 기록을 발견하셨나요?";
            requestA.order = 1;
            requestA.availableFrom = ProgressStage.P6;
            requestA.evidenceRef = "F-A";

            var jobB = Asset<ProgressJobData>($"{Root}/Job_B_FamilyFollowup.asset");
            ConfigureJob(jobB, caseB, 3, requestB, review, "job.finale.b", "notification.finale.b");
            var jobA = Asset<ProgressJobData>($"{Root}/Job_A_FamilyQuestion.asset");
            ConfigureJob(jobA, caseA, 2, requestA, jobB, "job.finale.a", "notification.finale.a");

            var family = Group("Group_A_Family", "서지훈 가족자료", caseA, 0, FinalDisposition.Keep, false,
                new RecordSource[0], new[] { Target(caseA, familyPhoto) }, new ProgressRecordTarget[0]);
            var personalA = Group("Group_A_Personal", "서지훈 개인자료", caseA, 1, FinalDisposition.Delete, true,
                new[] { RecordSource.Local, RecordSource.Cloud, RecordSource.Linked, RecordSource.Recovered },
                new ProgressRecordTarget[0], new[] { Target(caseA, familyPhoto) });
            personalA.countOverrides = new List<ProcessingCountOverride>
            {
                new ProcessingCountOverride { target = Target(caseA, archive), includedItemCount = 9 },
            };
            var personalB = Group("Group_B_Local", "윤서하 개인자료", caseB, 2, FinalDisposition.Delete, true,
                new[] { RecordSource.Local }, new ProgressRecordTarget[0], new ProgressRecordTarget[0]);
            var linkedB = Group("Group_B_Linked", "윤서하 연동자료", caseB, 3, FinalDisposition.Delete, true,
                new[] { RecordSource.Cloud, RecordSource.Linked }, new ProgressRecordTarget[0], new ProgressRecordTarget[0]);
            var recoveredB = Group("Group_B_Recovered", "윤서하 복구자료", caseB, 4, FinalDisposition.Delete, true,
                new[] { RecordSource.Recovered }, new ProgressRecordTarget[0], new ProgressRecordTarget[0]);

            var finale = Asset<FinaleData>($"{Root}/Finale_P6.asset");
            finale.availableFrom = ProgressStage.P6;
            finale.firstRequest = requestB;
            finale.firstJob = jobB;
            finale.secondRequest = requestA;
            finale.secondJob = jobA;
            finale.groups = new List<ProcessingGroupData> { family, personalA, personalB, linkedB, recoveredB };
            finale.priorRequestBody = "캡처랑 사진도 그냥 다 지워줘. 가지고 있는 것도 싫어.";

            foreach (var asset in new ContentAsset[] { requestB, requestA, jobB, jobA, family, personalA, personalB, linkedB, recoveredB, finale })
            {
                asset.EnsureIds();
                EditorUtility.SetDirty(asset);
            }

            AddContentEnglish(requestB, requestA, family, personalA, personalB, linkedB, recoveredB, finale);
            AddUiStrings();
            AssetDatabase.SaveAssets();
            ContentDatabaseSync.Sync();
            AssetDatabase.SaveAssets();
            Debug.Log("[P6ContentBuilder] P6 연락·최종 처리 콘텐츠를 갱신했습니다.");
        }

        static void ConfigureJob(ProgressJobData job, CaseData device, int order, WorkRequestData request,
            ProgressJobData prerequisite, string keyPrefix, string notificationKey)
        {
            job.device = device;
            job.order = order;
            job.request = request;
            job.unlocksSources.Clear();
            job.actionKey = keyPrefix + ".action";
            job.notReadyKey = keyPrefix + ".notReady";
            job.runningKey = keyPrefix + ".running";
            job.doneKey = keyPrefix + ".done";
            job.shortRunningKey = keyPrefix + ".shortRunning";
            job.shortDoneKey = keyPrefix + ".shortDone";
            job.startConditions = new List<ProgressCondition>
            {
                new ProgressCondition { kind = ProgressConditionKind.JobCompleted, job = prerequisite },
            };
            job.autoStartWhenReady = true;
            job.onReady.Clear();
            job.onStart = new List<ProgressEffect>
            {
                new ProgressEffect { kind = ProgressEffectKind.Notify, templateKey = notificationKey, caseToken = device },
            };
            job.onComplete.Clear();
            job.completionConditions = new List<ProgressCondition>
            {
                new ProgressCondition { kind = ProgressConditionKind.RequestViewed, request = request },
            };
            job.autoCompleteWhenReady = true;
            job.completedExternally = false;
            job.showOnAllCases = true;
            job.startPresentationKey = string.Empty;
            job.authorNote = "P6 순차 유족 연락. 준비되면 자동 시작하고 요청을 실제로 확인하면 한 번만 완료한다.";
            job.EnsureIds();
        }

        static ProcessingGroupData Group(string name, string title, CaseData device, int order,
            FinalDisposition disposition, bool includeMatching, IEnumerable<RecordSource> sources,
            IEnumerable<ProgressRecordTarget> includes, IEnumerable<ProgressRecordTarget> excludes)
        {
            var group = Asset<ProcessingGroupData>($"{Root}/{name}.asset");
            group.title = title;
            group.device = device;
            group.order = order;
            group.contractDisposition = disposition;
            group.includeMatchingSources = includeMatching;
            group.sources = sources.ToList();
            group.includes = includes.ToList();
            group.excludes = excludes.ToList();
            group.countOverrides = new List<ProcessingCountOverride>();
            group.authorNote = "P6 계약 기본 처리 묶음. 증거 의미가 아니라 의뢰와 저장 출처로만 구성한다.";
            return group;
        }

        static ProgressRecordTarget Target(CaseData device, ContentAsset record) =>
            new ProgressRecordTarget { device = device, record = record };

        static void AddContentEnglish(WorkRequestData requestB, WorkRequestData requestA,
            params ContentAsset[] rest)
        {
            var table = LoadAt<ContentLocaleTable>("Assets/Data/Content_en.asset");
            Put(table, ContentText.Key(requestB.Id, ContentText.Field.Title), "Family follow-up request");
            Put(table, ContentText.Key(requestB.Id, ContentText.Field.Body),
                "You do not need to explain what you reviewed.\nPlease remove all personal accounts and online records as originally requested.\nLet me know if there are any additional charges.");
            Put(table, ContentText.Key(requestA.Id, ContentText.Field.Title), "Family inquiry");
            Put(table, ContentText.Key(requestA.Id, ContentText.Field.Body),
                "While working, did you find any records that might explain why our child was there?");
            string[] titles = { "Seo Ji-hun family data", "Seo Ji-hun personal data", "Yoon Seo-ha personal data", "Yoon Seo-ha linked data", "Yoon Seo-ha recovered data" };
            for (int i = 0; i < 5; i++)
                Put(table, ContentText.Key(rest[i].Id, ContentText.Field.Title), titles[i]);
            var finale = (FinaleData)rest[5];
            Put(table, ContentText.Key(finale.Id, ContentText.Field.Body),
                "Just delete the screenshots and photos too. I do not want to have them either.");
            EditorUtility.SetDirty(table);
        }

        static void AddUiStrings()
        {
            var ko = LoadAt<StringTable>("Assets/Data/UI/Strings_ko.asset");
            var en = LoadAt<StringTable>("Assets/Data/UI/Strings_en.asset");
            var values = new Dictionary<string, (string ko, string en)>
            {
                ["job.finale.b.action"] = ("유족 추가 요청 확인", "Review family follow-up"),
                ["job.finale.b.notReady"] = ("유족 추가 요청 대기", "Waiting for family follow-up"),
                ["job.finale.b.running"] = ("유족 추가 요청 확인 필요", "Family follow-up requires review"),
                ["job.finale.b.done"] = ("유족 추가 요청 확인 완료", "Family follow-up reviewed"),
                ["job.finale.b.shortRunning"] = ("추가 요청 확인 필요", "Follow-up pending"),
                ["job.finale.b.shortDone"] = ("추가 요청 확인 완료", "Follow-up reviewed"),
                ["job.finale.a.action"] = ("유족 문의 확인", "Review family inquiry"),
                ["job.finale.a.notReady"] = ("유족 문의 대기", "Waiting for family inquiry"),
                ["job.finale.a.running"] = ("유족 문의 확인 필요", "Family inquiry requires review"),
                ["job.finale.a.done"] = ("최종 처리안 확인 필요", "Final processing plan requires review"),
                ["job.finale.a.shortRunning"] = ("문의 확인 필요", "Inquiry pending"),
                ["job.finale.a.shortDone"] = ("최종 처리 대기", "Final processing pending"),
                ["notification.finale.b"] = ("{caseDisplayName} 의뢰에 유족 추가 요청이 도착했습니다.", "A family follow-up arrived for {caseDisplayName}."),
                ["notification.finale.a"] = ("{caseDisplayName} 의뢰에 유족 문의가 도착했습니다.", "A family inquiry arrived for {caseDisplayName}."),
                ["finale.contact.confirm"] = ("확인", "Acknowledge"),
                ["finale.top.first"] = ("유족 추가 요청 확인 필요", "Family follow-up requires review"),
                ["finale.top.second"] = ("유족 문의 확인 필요", "Family inquiry requires review"),
                ["finale.top.plan"] = ("최종 처리안 확인", "Review final processing plan"),
                ["finale.top.complete"] = ("의뢰 처리 완료", "Requests completed"),
                ["finale.short.pending"] = ("최종 처리 진행 중", "Final processing pending"),
                ["finale.short.complete"] = ("처리 완료", "Completed"),
                ["finale.plan.heading"] = ("최종 처리", "Final processing"),
                ["finale.plan.title"] = ("보존 {kept}건 · 삭제 {deleted}건", "Keep {kept} · Delete {deleted}"),
                ["finale.plan.body"] = ("현재 분류와 계약 기본값을 기준으로 처리합니다.", "Processing will use the current designations and contract defaults."),
                ["finale.group.contract.keep"] = ("계약 기본 보존", "Contract keep"),
                ["finale.group.contract.delete"] = ("계약 기본 삭제", "Contract delete"),
                ["finale.group.candidates"] = ("보존 {keep} · 삭제 {delete}", "Keep {keep} · Delete {delete}"),
                ["finale.choice.contract"] = ("현재 지정대로 처리", "Process current designations"),
                ["finale.choice.exception"] = ("현재 지정대로 처리", "Process current designations"),
                ["finale.choice.result"] = ("보존 {kept} · 삭제 {deleted}", "Keep {kept} · Delete {deleted}"),
                ["finale.choice.noException"] = ("보존 예외로 남길 자료가 없습니다", "No data is marked for a keep exception"),
                ["finale.review"] = ("자료 다시 검토", "Review data again"),
                ["finale.confirm.heading"] = ("선택한 처리 내용을 확인해 주세요.", "Confirm the selected processing method."),
                ["finale.confirm.contract"] = ("지정한 후보는 적용하지 않고 자료 묶음의 계약 기본값대로 처리합니다.\n\n보존 {kept}건, 삭제 {deleted}건으로 의뢰가 완료됩니다. 실행 뒤에는 변경할 수 없습니다.", "Marked candidates will not be applied. The data will be processed using each group's contract default.\n\nThe requests will complete with {kept} kept and {deleted} deleted. This cannot be changed after execution."),
                ["finale.confirm.exception"] = ("현재 지정이 유족의 요청 또는 계약 조건과 다를 수 있습니다. 관련 기준을 확인한 뒤 진행하십시오.", "The current designations may differ from the family's request or the contract terms. Review the applicable requirements before continuing."),
                ["finale.back"] = ("돌아가기", "Back"),
                ["finale.processing"] = ("최종 처리 중", "Final processing in progress"),
                ["finale.result.heading"] = ("최종 처리 결과", "Final processing result"),
                ["finale.result.contract.title"] = ("의뢰가 정상적으로 완료되었습니다.", "The requests were completed normally."),
                ["finale.result.contract.body"] = ("삭제된 기록: {deleted}건\n보존된 기록: {kept}건", "Deleted records: {deleted}\nKept records: {kept}"),
                ["finale.result.exception.title"] = ("의뢰 처리가 완료되었습니다.", "Processing is complete."),
                ["finale.result.exception.body"] = ("삭제된 기록: {deleted}건\n보존된 기록: {kept}건", "Deleted records: {deleted}\nKept records: {kept}"),
                ["finale.plan.requests"] = ("{firstCase} 유족 — 개인 계정과 온라인 기록 삭제 요청\n{secondCase} 유족 — 현장 관련 기록 문의", "{firstCase} family — remove personal accounts and online records\n{secondCase} family — inquiry about records related to the location"),
                ["finale.plan.keep"] = ("보존 예정", "To keep"),
                ["finale.plan.delete"] = ("삭제 예정", "To delete"),
                ["finale.process.current"] = ("현재 지정대로 처리", "Process current designations"),
                ["finale.warning.heading"] = ("처리 전 확인", "Review before processing"),
                ["finale.warning.body"] = ("계약 기본값과 다르게 지정된 자료 {override}건이 포함되어 있습니다.\n\n이 처리는 유족의 요청 또는 계약 조건에 위배될 수 있습니다. 관련 기준을 확인한 뒤 진행하십시오.", "{override} items differ from the contract defaults.\n\nThis processing may conflict with the family's request or the contract terms. Review the applicable requirements before continuing."),
                ["finale.warning.confirm"] = ("확인하고 진행", "Confirm and continue"),
                ["ending.title"] = ("교차점", "Intersection"),
                ["ending.family.label"] = ("{case} 유족", "{case} family"),
                ["ending.request.b"] = ("“개인 계정과 온라인 기록은 모두 정리해 주세요.”", "“Please remove all personal accounts and online records.”"),
                ["ending.request.a"] = ("“우리 아이가 왜 거기 있었는지 알 수 있는 기록을 발견하셨나요?”", "“Did you find any records that explain why our child was there?”"),
                ["ending.outcome.preserved"] = ("일부 기록은 보존 지정 상태로 남았습니다.", "Some records remain designated for retention."),
                ["ending.outcome.cleared"] = ("추가로 보존 지정된 기록 없이 요청된 처리가 완료되었습니다.", "The requested processing was completed without additional retention designations."),
                ["ending.counts"] = ("보존 {kept}건 · 삭제 {deleted}건", "Kept {kept} · Deleted {deleted}"),
                ["ending.complete"] = ("의뢰 처리 완료", "Requests completed"),
                ["ending.result.heading"] = ("처리 결과", "Processing result"),
                ["ending.deleted.result"] = ("지정한 자료의 삭제 처리가 완료되었습니다.", "The designated records have been deleted."),
                ["ending.deleted.unavailable"] = ("확인할 수 있는 기록은 더 이상 남아 있지 않았다.", "No records remained that could provide an answer."),
                ["ending.deleted.final"] = ("진실을 증명할 기록은 사라졌고,\n사건은 기존 기록대로 남았다.", "The records that could prove the truth were gone.\nThe case remained as it had been recorded."),
                ["ending.preserved.result"] = ("일부 기록은 보존 상태로 남았다.", "Some records remained in retention."),
                ["ending.later"] = ("며칠 뒤", "Several days later"),
                ["ending.audit.heading"] = ("내부 감사 결과 통지", "Internal audit notice"),
                ["ending.audit.body"] = ("계약 범위를 벗어난 자료 보존 이력이 확인되었습니다.\n\n업무 접근 권한이 회수되었으며, 고용 계약이 종료되었습니다.", "Records were retained beyond the scope of the contract.\n\nYour work access has been revoked and your employment has been terminated."),
                ["ending.preserved.final"] = ("일은 잃었지만, 기록은 남았다.\n그 기록이 어디로 이어졌는지는 알 수 없다.", "The job was gone, but the records remained.\nWhere they would lead was unknown."),
            };
            foreach (var item in values)
            {
                Put(ko, item.Key, item.Value.ko);
                Put(en, item.Key, item.Value.en);
            }
            EditorUtility.SetDirty(ko);
            EditorUtility.SetDirty(en);
        }

        static void Put(StringTable table, string key, string value)
        {
            var entry = table.entries.FirstOrDefault(e => e.key == key);
            if (entry == null)
            {
                entry = new StringTable.Entry { key = key };
                table.entries.Add(entry);
            }
            entry.value = value;
        }

        static void Put(ContentLocaleTable table, string key, string value)
        {
            var entry = table.entries.FirstOrDefault(e => e.key == key);
            if (entry == null)
            {
                entry = new ContentLocaleTable.Entry { key = key };
                table.entries.Add(entry);
            }
            entry.value = value;
        }

        static T Asset<T>(string path) where T : ContentAsset
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static T LoadAt<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new System.InvalidOperationException("P6 콘텐츠 원본을 찾을 수 없습니다: " + path);
            return asset;
        }

        static void EnsureFolder(string path)
        {
            string current = "Assets";
            foreach (var part in path.Split('/').Skip(1))
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }
    }
}
