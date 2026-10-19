using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class LabAuthoringRuntimeTests
    {
        Scene scene;ProvingGround ground;string directory,project,historyPath,qaHistory,oldHistory,oldProject;
        LabBundle baseline;LabReleaseCatalog catalog;int marked,unmarked;byte[] packageBytes;
        ManualResetEventSlim entered,gate;Task worker;object oldLoader;
        static readonly FieldInfo Loader=typeof(ProvingGround).GetField("LabHistoryLoaderForTests",BindingFlags.NonPublic|BindingFlags.Static);
        T Field<T>(string name)=>(T)typeof(ProvingGround).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ground);
        Button B(string name)=>ground.GetComponentsInChildren<Button>().Single(b=>b.name==name);
        InputField I(string name)=>ground.GetComponentsInChildren<InputField>().Single(b=>b.name==name);
        void Click(string name)=>B(name).onClick.Invoke();
        string PackagePath=>Path.Combine(Application.dataPath,"StarTournament/Resources/LabReleaseCatalog.json");
        DesignLabHistory Open(bool reset=true)=>new DesignLabHistory(historyPath,baseline,releases:catalog,authoring:true,resetToLatestDefault:reset,persistMigration:false);
        static void SaveDamage(DesignLabHistory h,float value){var draft=h.Selected.Snapshot;draft.Set("rifle.damage",value);h.Save(draft);}
        [UnitySetUp] public IEnumerator Setup()
        {
            directory=Path.Combine(Path.GetTempPath(),"st-lab-runtime-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            project=Path.Combine(directory,"project");Directory.CreateDirectory(Path.Combine(project,"Assets","StarTournament"));File.WriteAllText(Path.Combine(directory,".git"),"gitdir: fixture");
            oldHistory=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY");oldProject=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_PROJECT");oldLoader=Loader.GetValue(null);
            qaHistory=Path.Combine(directory,"player-history.json");Environment.SetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY",qaHistory);Environment.SetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_PROJECT",null);
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            ground=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProvingGround>()).Single();yield return NativeLoadingTestScene.WaitLabHistory(ground);
            Assert.That(Field<string>("labProjectRoot"),Is.Null,"QA history keeps the production path isolated");
            baseline=Field<LabBundle>("historyStartupBaseline").Clone();catalog=LabReleaseCatalog.Load();packageBytes=File.ReadAllBytes(PackagePath);
            yield return SceneManager.UnloadSceneAsync(scene);scene=default;ground=null;
            historyPath=LabProjectStorage.ResolvePath(project);var h=Open();SaveDamage(h,31);marked=h.Selected.Number;h.MarkSelectedForRelease(true);SaveDamage(h,32);unmarked=h.Selected.Number;
        }
        IEnumerator LoadAuthor()
        {
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_PROJECT",project);
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            ground=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProvingGround>()).Single();
        }
        IEnumerator SelectRevision(int number)
        {
            Click("lab-revision-select");yield return null;Click("revision-"+number);yield return null;Click("revision-select");
            float deadline=Time.realtimeSinceStartup+120;
            while(ground.LabSelectionPending){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            yield return null;
        }
        [UnityTest] public IEnumerator ConsumerHintsExplainPersonalDefaultsWithoutChangingLabState()
        {
            yield return LoadAuthor();Click("main-action-2");yield return null;I("lab-search").text=GamepadLookSettings.HorizontalPath;yield return null;
            var active=ground.ActiveLabReference.Clone();var bytes=File.ReadAllBytes(historyPath);var before=Field<LabBundle>("labDraft").Hash();
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(I("input-"+GamepadLookSettings.HorizontalPath).gameObject);yield return null;
            var hint=ground.GetComponentsInChildren<Text>().Single(t=>t.name=="lab-hint");Assert.That(hint.text,Does.Contain("личные настройки имеют приоритет"));
            var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/standard-design-lab/consumers"));Directory.CreateDirectory(output);
            yield return EditorUiCapture.Capture(ground,Path.Combine(output,"preference-default-hint-720p.png"),1280,720);
            I("lab-search").text="rifle.damage";yield return null;UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(I("input-rifle.damage").gameObject);yield return null;
            Assert.That(hint.text,Does.Not.Contain("личные настройки имеют приоритет"));Assert.That(Field<LabBundle>("labDraft").Hash(),Is.EqualTo(before));
            Assert.That(ground.ActiveLabReference.Matches(active),Is.True);CollectionAssert.AreEqual(bytes,File.ReadAllBytes(historyPath));
        }
        [Serializable]sealed class CatalogueReport {public CatalogueField[] Fields;public string[] Excluded;}
        [Serializable]sealed class CatalogueField
        {
            public string Key,Profile,Group,Label,Description,Unit,Kind,Application;
            public float Minimum,Maximum,Step,FactoryDefault,ReleaseDefault;
        }
        [UnityTest] public IEnumerator CatalogueHintsUseMarkedDefaultAndDeclareNextGameWithoutPublishingDraft()
        {
            yield return LoadAuthor();Click("main-action-2");yield return null;
            var history=Field<DesignLabHistory>("labHistory");var defaults=history.ReleaseParameterDefaults();
            var visible=baseline.Descriptors.Where(d=>baseline.IsVisible(d.Path)).ToArray();
            foreach(var d in visible)
            {
                Assert.That(d.Validate(out var reason),Is.True,d.Path+": "+reason);
                Assert.That(d.Application,Is.EqualTo(LabParameterApplication.NextGame),d.Path);
                Assert.That(d.Contains(defaults.Get(d.Path)),Is.True,d.Path+": release Default must respect current range");
            }
            var report=new CatalogueReport{Fields=visible.Select(d=>new CatalogueField{Key=d.Path,Profile=baseline.Profile(d.Path).Id,Group=d.Group,Label=d.Label,Description=d.Description,Unit=d.Unit,Kind=d.Kind.ToString(),Application=d.Application.ToString(),Minimum=d.Minimum,Maximum=d.Maximum,Step=d.Step,FactoryDefault=d.DefaultValue,ReleaseDefault=defaults.Get(d.Path)}).ToArray(),Excluded=baseline.Descriptors.Where(d=>!baseline.IsVisible(d.Path)).Select(d=>d.Path).ToArray()};
            var reportOutput=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/standard-design-lab/catalogue"));Directory.CreateDirectory(reportOutput);
            File.WriteAllText(Path.Combine(reportOutput,"actual-catalogue.json"),JsonUtility.ToJson(report,true));
            I("lab-search").text="rifle.damage";yield return null;
            I("input-rifle.damage").text="33";yield return null;
            var active=ground.ActiveLabReference.Clone();var bytes=File.ReadAllBytes(historyPath);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(I("input-rifle.damage").gameObject);yield return null;
            var hint=ground.GetComponentsInChildren<Text>().Single(t=>t.name=="lab-hint");
            Assert.That(hint.text,Does.Contain("Целое число").And.Contain("Релизный Default: 31").And.Contain("Применение: следующая игра"));
            Assert.That(hint.supportRichText,Is.False);CollectionAssert.AreEqual(bytes,File.ReadAllBytes(historyPath));Assert.That(ground.ActiveLabReference.Matches(active),Is.True);
            var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/standard-design-lab/catalogue"));Directory.CreateDirectory(output);
            yield return EditorUiCapture.Capture(ground,Path.Combine(output,"catalogue-hint-720p.png"),1280,720);
            Click("lab-revision-select");yield return null;Click("revision-release");yield return null;Click("cancel");yield return null;
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(I("input-rifle.damage").gameObject);yield return null;
            Assert.That(hint.text,Does.Contain("Релизный Default: "+baseline.Get("rifle.damage").ToString(System.Globalization.CultureInfo.InvariantCulture)));
            Assert.That(I("input-rifle.damage").text,Is.EqualTo("33"));Assert.That(ground.ActiveLabReference.Matches(active),Is.True);
            Assert.That(B("lab-save").interactable,Is.True);
        }
        [UnityTest] public IEnumerator HistoricalSourceIdentityAndPerRowIncompatibilityStayVisibleWithoutActivation()
        {
            File.Delete(historyPath);
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            var before=typeof(ProvingProfile).GetMethod("BeforeBotBanterInterval",flags);
            var previous=new LabBundle{Profiles=baseline.Profiles.Select(p=>(ProvingProfile)before.Invoke(p,null)).ToList()};
            var old=new DesignLabHistory(historyPath,previous,releases:LabReleaseCatalog.Factory(previous),authoring:true);
            old.Create("Сохранённый баланс");string local=old.SelectedProfileId;
            var file=(LabHistoryFile)typeof(DesignLabHistory).GetMethod("ReadStorage",flags).Invoke(old,new object[]{File.ReadAllText(historyPath)});
            var original=file.Profiles.Single(p=>p.Id==local).Revisions.Single();original.Date="20000102";
            file.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId).Revisions.Add(new LabRevision{Number=9,Date="20261017",Hash=baseline.Hash(),Snapshot=baseline.Clone(),ReleaseCandidate=true});
            var well=typeof(ProvingProfile).GetMethod("BeforeWellDetail",flags);
            var incompatible=new LabBundle{Profiles=baseline.Profiles.Select(p=>(ProvingProfile)well.Invoke(p,null)).ToList()};
            var bad=new LabRevision{Number=1,Date="20000103",Hash=incompatible.Hash(),Snapshot=incompatible,ReleaseCandidate=true};
            file.Profiles.Add(new LabHistoryProfile{Id="legacy-light",Name="Прежний свет",Revisions={bad,new LabRevision{Number=9,Date="20261017",Hash=baseline.Hash(),Snapshot=baseline.Clone()}}});
            file.Profiles.Add(new LabHistoryProfile{Id="legacy-only",Name="Только прежняя версия",Revisions={bad.Clone()}});
            File.WriteAllText(historyPath,JsonUtility.ToJson(file));
            // Probe a future incompatible grid; today's finer Well grid accepts the old .8 value.
            Loader.SetValue(null,new Func<string,LabBundle,LabReleaseCatalog,CancellationToken,DesignLabHistory>((source,current,releases,token)=>{
                var future=current.Clone();future.Profile("well.fill").Descriptor("well.fill").Step=.05f;
                return new DesignLabHistory(source,future,releases:releases,authoring:true,persistMigration:false,cancellationToken:token);
            }));
            yield return LoadAuthor();Click("main-action-2");yield return null;
            Click("lab-profile-select");yield return null;Click("profile-"+local);
            float deadline=Time.realtimeSinceStartup+120;while(ground.LabSelectionPending){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}yield return null;
            var h=Field<DesignLabHistory>("labHistory");var view=h.Selected;
            Assert.That(view.CompatibilitySourceNumber,Is.EqualTo(1));Assert.That(view.Number,Is.Not.EqualTo(1));
            Assert.That(ground.ActiveLabReference.Revision,Is.EqualTo(1));Assert.That(ground.ActiveLabReference.Hash,Is.EqualTo(view.Hash));
            Assert.That(ground.LabSavedIdentity,Does.Contain("v1-20000102"));Assert.That(ground.LabSavedIdentity,Does.Contain(original.Hash));
            Assert.That(B("lab-revision-select").GetComponentInChildren<Text>().text,Does.Contain("v1-20000102"));
            var bytes=File.ReadAllBytes(historyPath);Click("lab-revision-select");yield return null;
            Assert.That(B("revision-"+view.Number).GetComponentInChildren<Text>().text,Does.Contain("для текущей игры"));
            Assert.That(ground.GetComponentsInChildren<Text>().Single(t=>t.name=="revision-hash").text,Is.EqualTo("SHA256 "+original.Hash));
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(historyPath));
            var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/standard-design-lab/mixed-history"));Directory.CreateDirectory(output);
            yield return EditorUiCapture.Capture(ground,Path.Combine(output,"saved-source-720p.png"),1280,720);
            Click("cancel");yield return null;Click("lab-profile-select");yield return null;Click("profile-legacy-light");
            deadline=Time.realtimeSinceStartup+120;while(ground.LabSelectionPending){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}yield return null;
            var active=ground.ActiveLabReference;var selected=h.Selected;bytes=File.ReadAllBytes(historyPath);
            Click("lab-revision-select");yield return null;Click("revision-1");yield return null;
            Assert.That(B("revision-select").interactable,Is.False);Assert.That(B("revision-release").interactable,Is.True);
            var reason=ground.GetComponentsInChildren<Text>().Single(t=>t.name=="revision-status");
            Assert.That(reason.text,Does.Contain("well.fill"));Assert.That(reason.supportRichText,Is.False);
            Assert.That(h.Selected.Hash,Is.EqualTo(selected.Hash));Assert.That(ground.ActiveLabReference.Matches(active),Is.True);
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(historyPath));
            yield return EditorUiCapture.Capture(ground,Path.Combine(output,"incompatible-row-720p.png"),1280,720);
            Click("revision-release");yield return null;
            Assert.That(ground.ActiveLabReference.Matches(active),Is.True);Assert.That(h.Selected.Hash,Is.EqualTo(selected.Hash));
            var retained=h.Profiles.Single(p=>p.Id=="legacy-light").Revisions.Single(r=>r.Number==1);
            Assert.That(retained.ReleaseCandidate,Is.False);Assert.That(retained.Hash,Is.EqualTo(bad.Hash));Assert.That(retained.Date,Is.EqualTo(bad.Date));
            Assert.That(JsonUtility.ToJson(retained.Snapshot),Is.EqualTo(JsonUtility.ToJson(bad.Snapshot)));
            Click("cancel");yield return null;I("input-rifle.damage").text="33";yield return null;
            selected=h.Selected;active=ground.ActiveLabReference;bytes=File.ReadAllBytes(historyPath);
            Click("lab-profile-select");yield return null;Click("profile-legacy-only");yield return null;
            Assert.That(B("revision-select").interactable,Is.False);Assert.That(B("revision-release").interactable,Is.True);
            Assert.That(ground.GetComponentsInChildren<Text>().Single(t=>t.name=="revision-identity").text,Does.Contain("Только прежняя версия"));
            Assert.That(ground.GetComponentsInChildren<Text>().Single(t=>t.name=="revision-status").text,Does.Contain("well.fill"));
            Assert.That(h.SelectedProfileId,Is.EqualTo("legacy-light"));Assert.That(h.Selected.Hash,Is.EqualTo(selected.Hash));
            Assert.That(ground.ActiveLabReference.Matches(active),Is.True);Assert.That(Field<LabBundle>("labDraft").Get("rifle.damage"),Is.EqualTo(33));
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(historyPath));
            yield return EditorUiCapture.Capture(ground,Path.Combine(output,"old-only-profile-720p.png"),1280,720);
            Click("revision-release");yield return null;
            var only=h.Profiles.Single(p=>p.Id=="legacy-only").Revisions.Single();Assert.That(only.ReleaseCandidate,Is.False);
            Assert.That(only.Hash,Is.EqualTo(bad.Hash));Assert.That(only.Date,Is.EqualTo(bad.Date));
            Assert.That(ground.ActiveLabReference.Matches(active),Is.True);Assert.That(h.Selected.Hash,Is.EqualTo(selected.Hash));
            Click("cancel");yield return null;Assert.That(I("input-rifle.damage").text,Is.EqualTo("33"));Assert.That(B("lab-save").interactable,Is.True);
        }
        [UnityTest] public IEnumerator HistoricalInspectionAndUnmarkPreserveActiveBaseDraftAndOriginal()
        {
            File.Delete(historyPath);var previous=baseline.Clone();previous.Profiles=previous.Profiles.Select(p=>p.BeforeBloodIntensityIncrease()).ToList();
            var old=new DesignLabHistory(historyPath,previous,releases:LabReleaseCatalog.Factory(previous),authoring:true,resetToLatestDefault:true,persistMigration:false);
            old.Rename("Исторический Default");var original=old.Selected;int number=original.Number;
            var current=Open(false);SaveDamage(current,31);current.MarkSelectedForRelease(true);marked=current.Selected.Number;
            yield return LoadAuthor();Click("main-action-2");yield return null;
            I("input-rifle.damage").text="33";yield return null;
            var active=ground.ActiveLabReference.Clone();var h=Field<DesignLabHistory>("labHistory");var selected=h.Selected;var bytes=File.ReadAllBytes(historyPath);
            Click("lab-revision-select");yield return null;Click("revision-"+number);yield return null;
            Assert.That(B("revision-select").interactable,Is.False);Assert.That(B("revision-release").interactable,Is.True);
            Assert.That(B("revision-"+marked).GetComponentInChildren<Text>().text,Does.Contain("выбранная база"));
            Assert.That(ground.GetComponentsInChildren<Text>().Single(t=>t.name=="revision-active").text,Does.Contain("v"+marked+"-"));
            Assert.That(h.Selected.Hash,Is.EqualTo(selected.Hash));Assert.That(ground.ActiveLabReference.Matches(active),Is.True);
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(historyPath));Assert.That(Field<LabBundle>("labDraft").Get("rifle.damage"),Is.EqualTo(33));
            var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/standard-design-lab/approved-ui"));Directory.CreateDirectory(output);
            yield return EditorUiCapture.Capture(ground,Path.Combine(output,"19-historical-720p.png"),1280,720);
            Click("revision-release");yield return null;
            Assert.That(B("revision-release").interactable,Is.False);Assert.That(B("revision-select").interactable,Is.False);
            Assert.That(B("revision-release").transform.Find("state-box/state-check").gameObject.activeSelf,Is.False);
            Assert.That(ground.GetComponentsInChildren<Text>().Single(t=>t.name=="revision-status").text,Does.Contain("Включение недоступно"));
            string actionImages=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_ACTION_QA_IMAGES");
            if(!string.IsNullOrEmpty(actionImages)){Directory.CreateDirectory(actionImages);yield return EditorUiCapture.Capture(ground,Path.Combine(actionImages,"23-incompatible-unmarked-720.png"),1280,720);}
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject,Is.EqualTo(B("cancel").gameObject));
            var stored=Open(false);var retained=stored.SelectedProfile.Revisions.Single(r=>r.Number==number);
            Assert.That(retained.ReleaseCandidate,Is.False);Assert.That(retained.Hash,Is.EqualTo(original.Hash));Assert.That(retained.Date,Is.EqualTo(original.Date));
            Assert.That(JsonUtility.ToJson(retained.Snapshot),Is.EqualTo(JsonUtility.ToJson(original.Snapshot)));
            Assert.That(h.Selected.Hash,Is.EqualTo(selected.Hash));Assert.That(ground.ActiveLabReference.Matches(active),Is.True);
            Assert.That(Field<LabBundle>("labDraft").Get("rifle.damage"),Is.EqualTo(33));
            Click("cancel");yield return null;Assert.That(I("input-rifle.damage").text,Is.EqualTo("33"));Assert.That(B("lab-save").interactable,Is.True);
            Click("lab-revision-select");yield return null;Click("revision-"+marked);yield return null;Click("revision-select");yield return null;
            Assert.That(B("confirm"),Is.Not.Null,"Choosing a compatible base still guards an edited draft");Click("cancel");yield return null;
            Assert.That(I("input-rifle.damage").text,Is.EqualTo("33"));Assert.That(ground.ActiveLabReference.Matches(active),Is.True);
        }
        [UnityTest] public IEnumerator ProductionProfileChooserReturnsToPackagedDefaultInsteadOfLegacyHighNumber()
        {
            var local=new DesignLabHistory(qaHistory,baseline);
            for(int i=2;i<=9;i++){var draft=local.Selected.Snapshot;draft.Set("rifle.damage",30+i);local.Save(draft);}
            var legacy=local.Selected;local.Select(DesignLabHistory.ReleaseId,1);local.Create("My independent profile");string own=local.SelectedProfileId;
            var bytes=File.ReadAllBytes(qaHistory);
            var expected=DesignLabHistory.ResolvePublishedDefault(baseline,catalog).Selected;
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            ground=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProvingGround>()).Single();
            yield return NativeLoadingTestScene.WaitLabHistory(ground);
            Assert.That(Field<string>("labProjectRoot"),Is.Null);Assert.That(ground.ActiveLabReference.Hash,Is.EqualTo(expected.Hash));
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(qaHistory));
            Click("main-action-2");yield return null;
            Click("lab-profile-select");yield return null;Click("profile-"+own);
            float deadline=Time.realtimeSinceStartup+120;
            while(ground.LabSelectionPending){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}yield return null;
            var h=Field<DesignLabHistory>("labHistory");Assert.That(h.SelectedProfileId,Is.EqualTo(own));
            Click("lab-profile-select");yield return null;Click("profile-"+DesignLabHistory.ReleaseId);
            deadline=Time.realtimeSinceStartup+120;
            while(ground.LabSelectionPending){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}yield return null;
            Assert.That(h.Selected.ReleaseNumber,Is.EqualTo(expected.ReleaseNumber));Assert.That(h.IsShipped(h.Selected),Is.True);
            Assert.That(h.Selected.Hash,Is.EqualTo(expected.Hash));Assert.That(ground.ActiveLabReference.Hash,Is.EqualTo(expected.Hash));
            Assert.That(h.SelectedProfileReadOnly,Is.True);Assert.That(B("lab-save").interactable,Is.False);
            var retained=h.SelectedProfile.Revisions.Single(r=>r.Number==legacy.Number);
            Assert.That(retained.Hash,Is.EqualTo(legacy.Hash));Assert.That(retained.Date,Is.EqualTo(legacy.Date));
            Assert.That(JsonUtility.ToJson(retained.Snapshot),Is.EqualTo(JsonUtility.ToJson(legacy.Snapshot)));
            CollectionAssert.AreEqual(packageBytes,File.ReadAllBytes(PackagePath));
        }
        [UnityTest] public IEnumerator HistoricalDefaultStartupFlagsAndExplicitFullSavePreserveBalanceAndSource()
        {
            File.Delete(historyPath);
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            var before=typeof(ProvingProfile).GetMethod("BeforeBotBanterInterval",flags);
            var previous=new LabBundle{Profiles=baseline.Clone().Profiles.Select(p=>(ProvingProfile)before.Invoke(p,null)).ToList()};
            var old=new DesignLabHistory(historyPath,previous,releases:LabReleaseCatalog.Factory(previous),authoring:true);
            SaveDamage(old,31);old.MarkSelectedForRelease(true);var source=old.Selected;
            var bytes=File.ReadAllBytes(historyPath);
            yield return LoadAuthor();
            Assert.That(ground.IsReady,Is.True);Assert.That(ground.IsLabHistoryReady,Is.True);
            Assert.That(ground.ActiveLabReference.Revision,Is.EqualTo(source.Number));
            Assert.That(ground.CombatProfile.Get("rifle.damage"),Is.EqualTo(31));
            Assert.That(ground.LabSavedIdentity,Does.Contain(source.Hash));
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(historyPath),"Startup must not rewrite original saved balance");
            Click("main-action-2");yield return null;
            var h=Field<DesignLabHistory>("labHistory");var view=h.Selected;
            Assert.That(view.CompatibilitySourceNumber,Is.EqualTo(source.Number));
            Assert.That(h.SelectedReleaseMarked,Is.True);Assert.That(h.CanMaterializeSelected,Is.True);
            Assert.That(B("lab-save").interactable,Is.True,"Explicit complete save needs no numeric edit");
            var active=ground.ActiveLabReference.Clone();int count=h.SelectedProfile.Revisions.Count;
            Click("lab-release");yield return null;Assert.That(h.SelectedReleaseMarked,Is.False);
            Click("lab-release");yield return null;Assert.That(h.SelectedReleaseMarked,Is.True);
            Assert.That(h.SelectedProfile.Revisions.Count,Is.EqualTo(count));
            Assert.That(h.Selected.Hash,Is.EqualTo(view.Hash));Assert.That(h.CanMaterializeSelected,Is.True);
            Assert.That(ground.ActiveLabReference.Matches(active),Is.True);
            var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/standard-design-lab/default-source"));Directory.CreateDirectory(output);
            yield return EditorUiCapture.Capture(ground,Path.Combine(output,"author-default-source-720p.png"),1280,720);
            Click("lab-save");yield return null;
            Assert.That(h.Selected.IsCompatibilityView,Is.False);Assert.That(h.SelectedReleaseMarked,Is.False);
            Assert.That(h.Selected.Number,Is.GreaterThan(view.Number));Assert.That(h.Selected.Hash,Is.EqualTo(view.Hash));
            Assert.That(ground.ActiveLabReference.Revision,Is.EqualTo(h.Selected.Number));
            Assert.That(B("lab-save").interactable,Is.False);
            var retained=Open(false).SelectedProfile.Revisions.Single(r=>r.Number==source.Number);
            Assert.That(retained.Hash,Is.EqualTo(source.Hash));Assert.That(retained.Date,Is.EqualTo(source.Date));
            Assert.That(retained.ReleaseCandidate,Is.True);
            Assert.That(JsonUtility.ToJson(retained.Snapshot),Is.EqualTo(JsonUtility.ToJson(source.Snapshot)));
            CollectionAssert.AreEqual(packageBytes,File.ReadAllBytes(PackagePath));
        }
        [UnityTest] public IEnumerator StartupUsesProjectDefaultAndSaveWritesOnlyProjectForNextGame()
        {
            var bytes=File.ReadAllBytes(historyPath);yield return LoadAuthor();
            Assert.That(ground.IsReady,Is.True);Assert.That(ground.IsLabHistoryReady,Is.True);Assert.That(ground.IsLabHistoryLoading,Is.False);
            Assert.That(ground.ActiveLabReference.Revision,Is.EqualTo(marked));Assert.That(ground.CombatProfile.Get("rifle.damage"),Is.EqualTo(31));
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(historyPath),"Startup must not persist a different last selection");
            Click("main-action-2");yield return null;
            Assert.That(I("input-rifle.damage").interactable,Is.True);Assert.That(B("lab-delete").interactable,Is.False);Assert.That(B("lab-release").interactable,Is.True);
            I("input-rifle.damage").text="33";yield return null;Click("lab-save");yield return null;
            var stored=Open(false);Assert.That(stored.Selected.Number,Is.EqualTo(unmarked+1));Assert.That(stored.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(33));
            Assert.That(stored.Selected.ReleaseCandidate,Is.False);Assert.That(ground.ActiveLabReference.Revision,Is.EqualTo(stored.Selected.Number));
            Assert.That(ground.CombatProfile.Get("rifle.damage"),Is.EqualTo(31),"Saved version applies to next game, not the current owners mid-edit");
            Assert.That(ground.GetComponentsInChildren<Text>().Single(t=>t.name=="lab-identity").text,Does.Contain("DesignLab/authoring-history-v1.json"));
            Assert.That(ground.GetComponentsInChildren<Text>().Single(t=>t.name=="lab-status").text,Does.Contain("Git commit/push не выполнялся"));
            var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/standard-design-lab/runtime"));Directory.CreateDirectory(output);
            yield return EditorUiCapture.Capture(ground,Path.Combine(output,"05-author-save.png"),1600,900);
            Click("lab-back");yield return null;Click("main-action-0");yield return null;
            Assert.That(ground.CombatProfile.Get("rifle.damage"),Is.EqualTo(33));Assert.That(Open().Selected.Number,Is.EqualTo(marked));
            Assert.That(File.Exists(qaHistory),Is.False);CollectionAssert.AreEqual(packageBytes,File.ReadAllBytes(PackagePath));
        }
        [UnityTest] public IEnumerator SeededProjectDisplaysOriginalSavedVersionAndDoesNotWriteOnStartup()
        {
            File.Delete(historyPath);var expected=DesignLabHistory.ResolvePublishedDefault(baseline,catalog).Selected;
            yield return LoadAuthor();var selected=Field<DesignLabHistory>("labHistory").Selected;
            Assert.That(ground.ActiveLabReference.Revision,Is.EqualTo(expected.ReleaseNumber));
            Assert.That(ground.ActiveLabReference.Hash,Is.EqualTo(expected.Hash));Assert.That(File.Exists(historyPath),Is.False);
            Click("main-action-2");yield return null;
            Assert.That(B("lab-revision-select").GetComponentInChildren<Text>().text,Does.StartWith("v"+expected.ReleaseNumber+"-"));
            Assert.That(ground.ActiveLabReference.Revision,Is.EqualTo(selected.ReleaseNumber));
            var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/standard-design-lab/runtime"));Directory.CreateDirectory(output);
            yield return EditorUiCapture.Capture(ground,Path.Combine(output,"05-project-startup-saved-identity.png"),1600,900);
            Assert.That(File.Exists(historyPath),Is.False);
            CollectionAssert.AreEqual(packageBytes,File.ReadAllBytes(PackagePath));
        }
        [UnityTest] public IEnumerator ReleaseToggleAndCopyUseSavedSourceWithoutActivatingDraft()
        {
            yield return LoadAuthor();Click("main-action-2");yield return null;
            var h=Field<DesignLabHistory>("labHistory");var selected=h.Selected;var active=ground.ActiveLabReference.Clone();
            var originals=h.SelectedProfile.Revisions.Select(r=>new[]{r.Number.ToString(),r.Hash,r.Date,JsonUtility.ToJson(r.Snapshot)}).ToArray();
            I("lab-search").text="rifle.damage";yield return null;I("input-rifle.damage").text="33";yield return null;
            Assert.That(B("lab-create").GetComponentInChildren<Text>().text,Is.EqualTo("Создать копию"));
            Assert.That(B("lab-clear").GetComponentInChildren<Text>().text,Is.EqualTo("Сбросить правки"));
            Assert.That(B("lab-reset-release").GetComponentInChildren<Text>().text,Is.EqualTo("Версия по умолчанию"));
            Assert.That(B("reset-rifle.damage").GetComponentInChildren<Text>().text,Is.EqualTo("Вернуть"));
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(B("reset-rifle.damage").gameObject);yield return null;
            Assert.That(ground.GetComponentsInChildren<Text>().Single(t=>t.name=="lab-hint").text,Does.Contain("Вернуть значение выбранной сохранённой версии: 31"));
            Assert.That(B("lab-release").interactable,Is.False);Assert.That(ground.GetComponentsInChildren<Text>().Single(t=>t.name=="lab-release-reason").text,Is.EqualTo("Сначала сохраните правки"));
            Click("lab-revision-select");yield return null;Click("revision-"+unmarked);yield return null;
            Assert.That(B("revision-release").interactable,Is.True);Assert.That(B("revision-release").transform.Find("state-box/state-check").gameObject.activeSelf,Is.False);
            string output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_ACTION_QA_IMAGES");
            if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);yield return EditorUiCapture.Capture(ground,Path.Combine(output,"21-compatible-release-720.png"),1280,720);}
            Click("revision-release");yield return null;Assert.That(h.IsMarkedForRelease(h.SelectedProfileId,unmarked),Is.True);
            Assert.That(B("revision-release").transform.Find("state-box/state-check").gameObject.activeSelf,Is.True);
            Assert.That(h.Selected.Number,Is.EqualTo(selected.Number));Assert.That(ground.ActiveLabReference.Matches(active),Is.True);Assert.That(Field<LabBundle>("labDraft").Get("rifle.damage"),Is.EqualTo(33));
            Click("revision-release");yield return null;Assert.That(h.IsMarkedForRelease(h.SelectedProfileId,unmarked),Is.False);
            Assert.That(B("revision-release").interactable,Is.True);Assert.That(B("revision-release").transform.Find("state-box/state-check").gameObject.activeSelf,Is.False);
            CollectionAssert.AreEqual(originals,h.SelectedProfile.Revisions.Select(r=>new[]{r.Number.ToString(),r.Hash,r.Date,JsonUtility.ToJson(r.Snapshot)}).ToArray());
            Click("cancel");yield return null;Click("lab-create");yield return null;
            Assert.That(ground.GetComponentsInChildren<Text>().Single(t=>t.name=="copy-source").text,Does.Contain(h.SelectedProfileName).And.Contain("v"+selected.Number));
            Assert.That(I("lab-profile-name").text,Is.EqualTo("Новый профиль"));
            I("lab-profile-name").text="Saved source copy";yield return null;
            if(!string.IsNullOrEmpty(output))yield return EditorUiCapture.Capture(ground,Path.Combine(output,"25-copy-profile-720.png"),1280,720);
            Click("submit");yield return null;Assert.That(B("confirm"),Is.Not.Null);Click("cancel");yield return null;
            Assert.That(h.Selected.Number,Is.EqualTo(selected.Number));Assert.That(Field<LabBundle>("labDraft").Get("rifle.damage"),Is.EqualTo(33));Assert.That(ground.ActiveLabReference.Matches(active),Is.True);
            Click("lab-create");yield return null;I("lab-profile-name").text="Saved source copy";Click("submit");yield return null;Click("confirm");yield return null;
            Assert.That(h.SelectedProfileName,Is.EqualTo("Saved source copy"));Assert.That(h.SelectedProfile.Revisions.Count,Is.EqualTo(1));Assert.That(h.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(31));Assert.That(h.SelectedReleaseMarked,Is.False);
            var source=h.Profiles.Single(p=>p.Id==active.ProfileId);CollectionAssert.AreEqual(originals,source.Revisions.Select(r=>new[]{r.Number.ToString(),r.Hash,r.Date,JsonUtility.ToJson(r.Snapshot)}).ToArray());
            CollectionAssert.AreEqual(packageBytes,File.ReadAllBytes(PackagePath));
        }
        [UnityTest] public IEnumerator BrokenAuthorHistoryRequiresExplicitSessionDefaultAndPreservesOriginalAcrossRestart()
        {
            File.WriteAllText(historyPath,"invalid-json <b>original author source</b>");var bytes=File.ReadAllBytes(historyPath);
            var expected=DesignLabHistory.ResolvePublishedDefault(baseline,catalog).Selected;
            yield return LoadAuthor();Assert.That(ground.LabHistoryLoadError,Is.Not.Null);Assert.That(ground.IsLabHistoryReady,Is.False);
            Assert.That(Field<bool>("authorStartupResolved"),Is.False);Click("main-action-0");yield return null;
            Assert.That(Field<object>("phase").ToString(),Is.EqualTo("Lab"));Assert.That(B("author-history-default").interactable,Is.True);
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject,Is.EqualTo(B("author-history-back").gameObject));
            Assert.That(B("author-history-back").navigation.selectOnRight,Is.EqualTo(B("author-history-default")));
            Assert.That(B("lab-save").interactable,Is.False);Assert.That(B("lab-create").interactable,Is.False);Assert.That(B("lab-back").interactable,Is.False);
            var reason=ground.GetComponentsInChildren<Text>().Single(t=>t.name=="author-history-reason");Assert.That(reason.supportRichText,Is.False);
            Assert.That(reason.text,Does.Contain(ground.LabHistoryLoadError));Assert.That(reason.text,Does.Contain("DesignLab/authoring-history-v1.json"));
            string output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_AUTHOR_FALLBACK_QA_IMAGES");
            if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);yield return EditorUiCapture.Capture(ground,Path.Combine(output,"author-history-error-720.png"),1280,720);}
            var fallbackPad=InputSystem.AddDevice<Gamepad>();try
            {
                InputSystem.QueueStateEvent(fallbackPad,new GamepadState().WithButton(GamepadButton.East));yield return null;yield return null;InputSystem.QueueStateEvent(fallbackPad,new GamepadState());yield return null;
            }
            finally{if(fallbackPad.added)InputSystem.RemoveDevice(fallbackPad);}
            Assert.That(Field<object>("phase").ToString(),Is.EqualTo("MainMenu"));Assert.That(Field<bool>("authorStartupResolved"),Is.False);CollectionAssert.AreEqual(bytes,File.ReadAllBytes(historyPath));
            var errorField=typeof(ProvingGround).GetField("historyLoadError",BindingFlags.NonPublic|BindingFlags.Instance);string actualError=ground.LabHistoryLoadError;
            errorField.SetValue(ground,actualError+"\n"+string.Join("\n",Enumerable.Repeat("Длинная диагностика <b>plain text</b> — исходная история сохранена.",32)));
            Click("main-action-0");yield return null;
            using(var viewport=EditorUiCapture.FixedViewport(ground,1280,720))
            {
                yield return null;Canvas.ForceUpdateCanvases();var scroll=ground.GetComponentsInChildren<ScrollRect>().Single(x=>x.name=="author-history-details");
                Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));scroll.verticalNormalizedPosition=1;
                var move=new UnityEngine.EventSystems.AxisEventData(UnityEngine.EventSystems.EventSystem.current){moveDir=UnityEngine.EventSystems.MoveDirection.Down,moveVector=Vector2.down};
                UnityEngine.EventSystems.ExecuteEvents.Execute(B("author-history-back").gameObject,move,UnityEngine.EventSystems.ExecuteEvents.moveHandler);
                Assert.That(scroll.verticalNormalizedPosition,Is.LessThan(1));Assert.That(reason.supportRichText,Is.False);
                Assert.That(reason.text,Does.Contain("<b>plain text</b>"));
            }
            if(!string.IsNullOrEmpty(output))yield return EditorUiCapture.Capture(ground,Path.Combine(output,"author-history-long-720.png"),1280,720);
            errorField.SetValue(ground,actualError);Click("author-history-back");yield return null;
            Click("main-action-0");yield return null;Click("author-history-default");yield return null;
            Assert.That(Field<object>("phase").ToString(),Is.EqualTo("MainMenu"));Assert.That(Field<bool>("authorStartupResolved"),Is.True);
            Assert.That(ground.ActiveLabReference.Hash,Is.EqualTo(expected.Hash));Assert.That(ground.CaptureLabBundle().Hash(),Is.EqualTo(expected.Snapshot.Hash()));
            Assert.That(ground.IsLabHistoryReady,Is.False);Assert.That(ground.LabHistoryLoadError,Is.Not.Null);
            Click("main-action-2");yield return null;Assert.That(B("author-history-default").interactable,Is.False);Assert.That(B("lab-save").interactable,Is.False);
            Assert.That(ground.GetComponentsInChildren<Text>().Single(t=>t.name=="author-history-session").text,Does.Contain("В этой сессии выбран"));
            if(!string.IsNullOrEmpty(output))yield return EditorUiCapture.Capture(ground,Path.Combine(output,"author-history-session-1080.png"),1920,1080);
            // Disabled controls also retain the data API guard if a callback is invoked directly.
            Click("lab-save");CollectionAssert.AreEqual(bytes,File.ReadAllBytes(historyPath));
            Click("author-history-back");yield return null;Click("main-action-0");yield return null;Assert.That(Field<object>("phase").ToString(),Is.EqualTo("Setup"));
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(historyPath));CollectionAssert.AreEqual(packageBytes,File.ReadAllBytes(PackagePath));Assert.That(File.Exists(qaHistory),Is.False);
            yield return SceneManager.UnloadSceneAsync(scene);scene=default;ground=null;
            yield return LoadAuthor();Assert.That(Field<bool>("authorStartupResolved"),Is.False);Assert.That(Field<bool>("authorSessionFallback"),Is.False);
            Click("main-action-0");yield return null;Assert.That(Field<object>("phase").ToString(),Is.EqualTo("Lab"));Assert.That(B("author-history-default").interactable,Is.True);
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(historyPath));CollectionAssert.AreEqual(packageBytes,File.ReadAllBytes(PackagePath));
        }
        [UnityTest] public IEnumerator MissingDefaultKeepsLabEditableAndFlagsOrRenameDoNotActivatePreviousExperiment()
        {
            var h=Open(false);
            foreach(var r in h.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId).Revisions.Where(r=>r.ReleaseCandidate))h.MarkForRelease(DesignLabHistory.ReleaseId,r.Number,false);
            h.Create("Previous experiment");string experiment=h.SelectedProfileId;var bytes=File.ReadAllBytes(historyPath);
            yield return LoadAuthor();var fallback=ground.ActiveLabReference;
            Assert.That(fallback.ProfileId,Is.EqualTo(DesignLabHistory.ReleaseId));Assert.That(Field<bool>("authorStartupResolved"),Is.False);
            Click("main-action-0");yield return null;Assert.That(Field<object>("phase").ToString(),Is.EqualTo("Lab"));
            Assert.That(Field<DesignLabHistory>("labHistory").SelectedProfileId,Is.EqualTo(experiment));
            Assert.That(I("input-rifle.damage").interactable,Is.True);CollectionAssert.AreEqual(bytes,File.ReadAllBytes(historyPath));
            Assert.That(ground.GetComponentsInChildren<Text>().Single(t=>t.name=="lab-status").text,Does.Contain("Отметьте"));
            Assert.That(ground.GetComponentsInChildren<Text>().Single(t=>t.name=="lab-identity").text,Does.Contain("Стартовая версия проекта не выбрана"));
            Click("lab-release");yield return null;Click("lab-rename");yield return null;I("lab-profile-name").text="Renamed experiment";Click("submit");yield return null;
            Assert.That(ground.ActiveLabReference.Matches(fallback),Is.True);Assert.That(Field<bool>("authorStartupResolved"),Is.False);
            var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/standard-design-lab/runtime"));Directory.CreateDirectory(output);
            yield return EditorUiCapture.Capture(ground,Path.Combine(output,"05-missing-default-720p.png"),1280,720);
            Click("lab-profile-select");yield return null;Click("profile-"+DesignLabHistory.ReleaseId);
            float deadline=Time.realtimeSinceStartup+120;while(ground.LabSelectionPending){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}yield return null;
            Click("lab-release");yield return null;Assert.That(Field<DesignLabHistory>("labHistory").ReleaseError,Is.Null);
            Click("lab-reset-release");yield return null;Assert.That(ground.ActiveLabReference.Revision,Is.EqualTo(unmarked));
            Assert.That(Field<bool>("authorStartupResolved"),Is.True);CollectionAssert.AreEqual(packageBytes,File.ReadAllBytes(PackagePath));
        }
        [UnityTest] public IEnumerator ReleaseResetGuardsDraftAndPreservesEverySavedRevision()
        {
            yield return LoadAuthor();Click("main-action-2");yield return null;
            yield return SelectRevision(unmarked);Click("lab-release");yield return null;yield return SelectRevision(marked);
            var before=Open(false);var hashes=before.Profiles.SelectMany(p=>p.Revisions.Select(r=>p.Id+"/"+r.Number+"/"+r.Hash)).ToArray();var bytes=File.ReadAllBytes(historyPath);
            I("input-rifle.damage").text="33";yield return null;Click("lab-reset-release");yield return null;
            Assert.That(B("discard-change-rifle.damage"),Is.Not.Null);Click("cancel");yield return null;Assert.That(I("input-rifle.damage").text,Is.EqualTo("33"));
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(historyPath));Click("lab-reset-release");yield return null;Click("confirm");yield return null;
            Assert.That(ground.ActiveLabReference.Revision,Is.EqualTo(unmarked));Assert.That(I("input-rifle.damage").text,Is.EqualTo("32"));
            Assert.That(Open(false).Profiles.SelectMany(p=>p.Revisions.Select(r=>p.Id+"/"+r.Number+"/"+r.Hash)),Is.EquivalentTo(hashes));
        }
        [UnityTest]public IEnumerator ProfileSwitchUsesLatestSavedVersionInsteadOfDerivedCache()
        {
            File.Delete(historyPath);
            var before=typeof(ProvingProfile).GetMethod("BeforeBotBanterInterval",BindingFlags.Instance|BindingFlags.NonPublic);
            var previous=new LabBundle{Profiles=baseline.Profiles.Select(p=>(ProvingProfile)before.Invoke(p,null)).ToList()};
            var old=new DesignLabHistory(historyPath,previous);old.Create("Saved experiment");string id=old.SelectedProfileId;
            var tuning=old.Selected.Snapshot;tuning.Set("rifle.damage",31);old.Save(tuning);
            var h=Open(false);Assert.That(h.StorageError,Is.Null);
            int release=h.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId).Revisions.Where(r=>h.Compatible(r)).Max(r=>r.Number);
            h.Select(DesignLabHistory.ReleaseId,release);h.MarkSelectedForRelease(true);
            h.Select(id,h.LatestAvailableRevision(id));h.Save(h.Selected.Snapshot);h.MarkSelectedForRelease(true);int saved=h.Selected.Number;
            float interval=h.Selected.Snapshot.Get("presentation.botBanterIntervalSeconds");
            h.Select(DesignLabHistory.ReleaseId,release);var draft=h.Selected.Snapshot;
            draft.Set("presentation.botBanterIntervalSeconds",interval+10);h.Save(draft);h.MarkSelectedForRelease(true);
            h.Select(id,saved);
            // Rebuild disposable caches after the complete saved source: their technical keys
            // must outrank it, while profile selection still chooses the saved public version.
            var stored=(LabHistoryFile)typeof(DesignLabHistory).GetMethod("ReadStorage",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(h,new object[]{File.ReadAllText(historyPath)});
            foreach(var profile in stored.Profiles)profile.Revisions.RemoveAll(r=>r.CompatibilitySourceNumber>0);
            File.WriteAllText(historyPath,JsonUtility.ToJson(stored));
            yield return LoadAuthor();Click("main-action-2");yield return null;
            var loaded=Field<DesignLabHistory>("labHistory");
            Assert.That(loaded.Profiles.Single(p=>p.Id==id).Revisions.Max(r=>r.Number),Is.GreaterThan(saved),"Fixture contains a newer technical view");
            Click("lab-profile-select");yield return null;Click("profile-"+id);
            float deadline=Time.realtimeSinceStartup+120;
            while(ground.LabSelectionPending){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            yield return null;
            Assert.That(ground.ActiveLabReference.ProfileId,Is.EqualTo(id));Assert.That(ground.ActiveLabReference.Revision,Is.EqualTo(saved));
            Assert.That(Field<LabBundle>("labDraft").Get("presentation.botBanterIntervalSeconds"),Is.EqualTo(interval));
            Assert.That(Open(false).Selected.Number,Is.EqualTo(saved));CollectionAssert.AreEqual(packageBytes,File.ReadAllBytes(PackagePath));
        }
        [UnityTest] public IEnumerator AuthorHistoryIsCriticalAndUnloadCancelsItsReadWithoutWriting()
        {
            entered=new ManualResetEventSlim();gate=new ManualResetEventSlim();
            Func<string,LabBundle,LabReleaseCatalog,CancellationToken,DesignLabHistory> load=(p,b,c,t)=>{entered.Set();gate.Wait(t);return new DesignLabHistory(p,b,releases:c,authoring:true,persistMigration:false);};
            Loader.SetValue(null,load);Environment.SetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_PROJECT",project);var bytes=File.ReadAllBytes(historyPath);
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);scene=SceneManager.GetSceneByName("ProvingGround");
            ground=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProvingGround>()).Single();
            float deadline=Time.realtimeSinceStartup+120;while(!entered.IsSet){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            Assert.That(ground.IsReady,Is.False);Assert.That(ground.IsLoading,Is.True);worker=Field<Task<DesignLabHistory>>("historyLoading");
            yield return SceneManager.UnloadSceneAsync(scene);scene=default;
            while(!worker.IsCompleted){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            Assert.That(worker.IsCanceled,Is.True);CollectionAssert.AreEqual(bytes,File.ReadAllBytes(historyPath));
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
            gate?.Set();Loader.SetValue(null,oldLoader);
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY",oldHistory);Environment.SetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_PROJECT",oldProject);
            if(worker!=null){float deadline=Time.realtimeSinceStartup+120;while(!worker.IsCompleted){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}}
            gate?.Dispose();entered?.Dispose();worker=null;gate=null;entered=null;
            if(Directory.Exists(directory))Directory.Delete(directory,true);yield return null;
        }
    }
}
