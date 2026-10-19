using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class LabDiscardWarningTests
    {
        Scene scene;ProvingGround ground;string directory,previous,historyPath;
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        T Field<T>(string name)=>(T)typeof(ProvingGround).GetField(name,Private).GetValue(ground);
        Button B(string name)=>ground.GetComponentsInChildren<Button>().Single(b=>b.name==name);
        InputField I(string name)=>ground.GetComponentsInChildren<InputField>().Single(b=>b.name==name);
        void Click(string name)=>B(name).onClick.Invoke();
        [UnitySetUp] public IEnumerator Setup()
        {
            directory=Path.Combine(Path.GetTempPath(),"st-lab-discard-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            historyPath=Path.Combine(directory,"history.json");previous=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY");
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY",historyPath);
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            ground=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<ProvingGround>()).Single();
            yield return NativeLoadingTestScene.WaitLabHistory(ground);Click("main-action-2");yield return null;
            Click("lab-create");yield return null;I("lab-profile-name").text="Draft warning QA";Click("submit");yield return null;
        }
        [UnityTest] public IEnumerator HiddenAndInvalidChangesAreListedCancelKeepsDraftConfirmOnlyNavigates()
        {
            var bytes=File.ReadAllBytes(historyPath);string saved=ground.LabSavedIdentity;
            float damage=Field<LabBundle>("labBaseline").Get("rifle.damage");
            var descriptor=Field<LabBundle>("labDraft").Descriptors.Single(d=>d.Path=="rifle.damage");
            I("input-rifle.damage").text=(damage==descriptor.Maximum?damage-descriptor.Step:damage+descriptor.Step).ToString(CultureInfo.InvariantCulture);yield return null;
            I("lab-search").text="player.movement.jumpSpeed";yield return null;
            I("input-player.movement.jumpSpeed").text="bad input";yield return null;
            I("lab-search").text="no matching fields";yield return null;Click("lab-back");yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(B("cancel").gameObject));
            var rows=ground.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("discard-change-")).ToArray();
            Assert.That(rows.Select(b=>b.name),Is.EquivalentTo(new[]{"discard-change-rifle.damage","discard-change-player.movement.jumpSpeed"}));
            Assert.That(rows.Single(b=>b.name.EndsWith("jumpSpeed")).transform.Find("values").GetComponent<Text>().text,Does.Contain(" → bad input"));
            Assert.That(rows.Single(b=>b.name.EndsWith("rifle.damage")).transform.Find("values").GetComponent<Text>().text,Does.StartWith(damage.ToString(CultureInfo.InvariantCulture)+" → "));
            Assert.That(B("lab-back").interactable,Is.False);Assert.That(ground.LabSavedIdentity,Is.EqualTo(saved));
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(historyPath));
            var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/standard-design-lab/discard"));Directory.CreateDirectory(output);
            yield return EditorUiCapture.Capture(ground,Path.Combine(output,"06-draft-warning.png"),1600,900);
            Click("cancel");yield return null;
            Assert.That(Field<Dictionary<string,string>>("labRaw")["player.movement.jumpSpeed"],Is.EqualTo("bad input"));
            Assert.That(Field<LabBundle>("labDraft").Get("rifle.damage"),Is.Not.EqualTo(damage));
            Click("lab-back");yield return null;Click("confirm");yield return null;
            Assert.That(Field<object>("phase").ToString(),Is.EqualTo("MainMenu"));
            Assert.That(Field<Dictionary<string,string>>("labRaw"),Is.Empty);Assert.That(ground.LabSavedIdentity,Is.EqualTo(saved));
            Assert.That(ground.CombatProfile.Get("rifle.damage"),Is.EqualTo(damage));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(historyPath));
        }
        [UnityTest] public IEnumerator LongDiffRevealsFocusedRowsWithoutScrollingAwayActions()
        {
            var bytes=File.ReadAllBytes(historyPath);var draft=Field<LabBundle>("labDraft");
            var changed=draft.Descriptors.Where(d=>draft.IsEditable(d.Path)&&d.Maximum>d.Minimum).Take(25).ToArray();
            Assert.That(changed,Has.Length.EqualTo(25));
            foreach(var d in changed)draft.Set(d.Path,draft.Get(d.Path)==d.Minimum?d.Maximum:d.Minimum);
            Click("lab-back");yield return null;
            using(EditorUiCapture.FixedViewport(ground,1280,720))
            {
                yield return null;Canvas.ForceUpdateCanvases();
                var scroll=ground.GetComponentsInChildren<ScrollRect>().Single(s=>s.name=="discard-changes");
                Assert.That(scroll.content.childCount,Is.EqualTo(25));Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
                var last=scroll.content.GetChild(24).GetComponent<Button>();EventSystem.current.SetSelectedGameObject(last.gameObject);yield return null;
                Assert.That(scroll.verticalNormalizedPosition,Is.LessThan(.05f));
                var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,(RectTransform)last.transform);
                Assert.That(bounds.min.y,Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin-3));Assert.That(bounds.max.y,Is.LessThanOrEqualTo(scroll.viewport.rect.yMax+3));
                var card=(RectTransform)B("cancel").transform.parent;
                foreach(var name in new[]{"cancel","confirm"})
                {
                    var b=RectTransformUtility.CalculateRelativeRectTransformBounds(card,(RectTransform)B(name).transform);
                    Assert.That(b.min.y,Is.GreaterThanOrEqualTo(card.rect.yMin));Assert.That(b.max.y,Is.LessThanOrEqualTo(card.rect.yMax));
                }
                var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/standard-design-lab/discard"));Directory.CreateDirectory(output);
                yield return EditorUiCapture.Capture(ground,Path.Combine(output,"06-long-diff-720p.png"),1280,720);
                Click("cancel");yield return null;
            }
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(historyPath));
            Assert.That(changed.All(d=>Field<LabBundle>("labDraft").Get(d.Path)!=Field<LabBundle>("labBaseline").Get(d.Path)),Is.True);
        }
        [UnityTest] public IEnumerator ClearAndDeleteRequireDiscardConsentBeforeTheirOriginalActions()
        {
            var bytes=File.ReadAllBytes(historyPath);var history=Field<DesignLabHistory>("labHistory");string id=history.SelectedProfileId;
            float original=Field<LabBundle>("labBaseline").Get("rifle.damage");
            var descriptor=Field<LabBundle>("labDraft").Descriptors.Single(d=>d.Path=="rifle.damage");
            string changed=(original==descriptor.Maximum?original-descriptor.Step:original+descriptor.Step).ToString(CultureInfo.InvariantCulture);
            I("input-rifle.damage").text=changed;yield return null;Click("lab-clear");yield return null;
            Assert.That(B("discard-change-rifle.damage"),Is.Not.Null);Click("cancel");yield return null;
            Assert.That(I("input-rifle.damage").text,Is.EqualTo(changed));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(historyPath));
            Click("lab-clear");yield return null;Click("confirm");yield return null;
            Assert.That(Field<object>("phase").ToString(),Is.EqualTo("Lab"));Assert.That(B("lab-save").interactable,Is.False);
            Assert.That(Field<LabBundle>("labDraft").Get("rifle.damage"),Is.EqualTo(original));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(historyPath));
            I("input-rifle.damage").text=changed;yield return null;Click("lab-delete");yield return null;
            Assert.That(B("discard-change-rifle.damage"),Is.Not.Null);Assert.That(history.SelectedProfileId,Is.EqualTo(id));
            Click("confirm");yield return null;
            Assert.That(history.SelectedProfileId,Is.EqualTo(id),"Discarding is not permission to skip profile deletion confirmation");
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(historyPath));
            Click("confirm");yield return null;
            Assert.That(history.Profiles.Any(p=>p.Id==id),Is.False);Assert.That(history.SelectedProfileId,Is.EqualTo(DesignLabHistory.ReleaseId));
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
            Environment.SetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY",previous);
            if(Directory.Exists(directory))Directory.Delete(directory,true);
            yield return null;
        }
    }
}
