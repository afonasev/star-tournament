using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class BalanceLabWorkspaceTests
    {
        Scene scene;ProvingGround ground;string directory,oldEnvironment;
        [UnitySetUp] public IEnumerator Setup(){directory=Path.Combine(Path.GetTempPath(),"balance-tree-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);oldEnvironment=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY");Environment.SetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY",Path.Combine(directory,"history.json"));yield return null;}
        Button Button(string name)=>ground.GetComponentsInChildren<Button>().Single(b=>b.name==name);
        InputField Input(string name)=>ground.GetComponentsInChildren<InputField>().Single(b=>b.name==name);
        void Click(string name)=>Button(name).onClick.Invoke();
        [UnityTest] public IEnumerator NativeTreeSearchHintsAndIndependentArt()
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();yield return NativeLoadingTestScene.WaitLabHistory(ground);
            Assert.AreEqual(.75f,ground.CombatProfile.Get("damage.friendlyMultiplier"));Assert.AreEqual(1f,ground.CombatProfile.Get("damage.selfMultiplier"));
            float art=ground.Profile.Get("camera.fieldOfViewDegrees");var mapHash=new LabBundle{Profiles={ground.CombatBowlAuthoring}}.Hash();
            Click("main-action-2");yield return null;
            Assert.AreEqual(5,ground.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("parent-")));
            Assert.IsFalse(Input("input-rifle.damage").interactable);
            string output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_BALANCE_CAPTURE");
            if(!string.IsNullOrEmpty(output))
            {
                Directory.CreateDirectory(output);
                yield return EditorUiCapture.Capture(ground,Path.Combine(output,"balance-tree-1920x1080.png"),1920,1080);
                yield return EditorUiCapture.Capture(ground,Path.Combine(output,"balance-tree-1280x720.png"),1280,720);
            }
            Click("parent-Боты");yield return null;Assert.IsNotNull(Button("group-bot-easy"));
            Click("group-bot-easy");yield return null;Assert.IsNotNull(Input("input-bots.easy.reactionSeconds"));
            Click("lab-create");yield return null;Input("lab-profile-name").text="Баланс дерева";Click("submit");yield return null;
            Input("lab-search").text="rifle.damage";yield return null;
            Input("input-rifle.damage").text="31";yield return null;Assert.IsTrue(Button("lab-save").interactable);
            Assert.AreEqual(art,ground.Profile.Get("camera.fieldOfViewDegrees"));Assert.AreEqual(mapHash,new LabBundle{Profiles={ground.CombatBowlAuthoring}}.Hash());
            EventSystem.current.SetSelectedGameObject(Input("input-rifle.damage").gameObject);Input("input-rifle.damage").text="32";yield return null;Assert.AreEqual("input-rifle.damage",EventSystem.current.currentSelectedGameObject.name);
            Input("lab-search").text="camera.fieldOfViewDegrees";yield return null;Assert.IsFalse(ground.GetComponentsInChildren<InputField>().Any(f=>f.name=="input-camera.fieldOfViewDegrees"));
            Input("lab-search").text="";yield return null;
            Assert.AreEqual(art,ground.Profile.Get("camera.fieldOfViewDegrees"));
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);Environment.SetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY",oldEnvironment);if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
