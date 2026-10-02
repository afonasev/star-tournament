using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class DesignLabWorkspaceTests
    {
        Scene scene;ProvingGround ground;Gamepad pad;string directory,oldEnvironment;
        Button Button(string name)=>ground.GetComponentsInChildren<Button>().Single(b=>b.name==name);
        InputField Input(string name)=>ground.GetComponentsInChildren<InputField>().Single(b=>b.name==name);
        void Click(string name)=>Button(name).onClick.Invoke();
        [UnityTest] public IEnumerator DraftValidationFocusAndSavedSelectionRemainIndependentOfLiveMatch()
        {
            directory=Path.Combine(Path.GetTempPath(),"st-lab-ui-"+Guid.NewGuid());Directory.CreateDirectory(directory);
            oldEnvironment=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY");Environment.SetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY",Path.Combine(directory,"history.json"));
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();pad=InputSystem.AddDevice<Gamepad>();
            Click("main-action-2");yield return null;
            Assert.That(ground.GetComponentsInChildren<Button>().Any(b=>b.name.StartsWith("group-map-")||b.name=="group-layout"||b.name=="group-ring-layout"||b.name=="group-details"||b.name=="group-ring"||b.name=="group-world-query"||b.name=="group-navigation"||b.name=="group-simulation"),Is.False);
            foreach(var hidden in new[]{"layout.fill-0.x","wayfinding.sign-0.yaw","broadcast.screenWidth","ring.spaceCenterY"}.Concat(LabBundle.AuditedExcludedPaths))
            {Input("lab-search").text=hidden;yield return null;Assert.That(ground.GetComponentsInChildren<InputField>().Any(f=>f.name=="input-"+hidden),Is.False);}
            Input("lab-search").text="";yield return null;
            float original=ground.CombatProfile.Get("rifle.damage");int seats=ground.LocalSeatCount;
            Input("input-rifle.damage").text="31";yield return null;Assert.That(ground.CombatProfile.Get("rifle.damage"),Is.EqualTo(original));Assert.That(Button("lab-save").interactable,Is.True);
            var scroll=ground.GetComponentsInChildren<ScrollRect>().Single(s=>s.name=="lab-fields");scroll.verticalNormalizedPosition=.5f;var selected=Input("input-rifle.damage").gameObject;EventSystem.current.SetSelectedGameObject(selected);float position=scroll.verticalNormalizedPosition;
            Input("input-rifle.damage").text="32";yield return null;Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(selected));Assert.That(scroll.verticalNormalizedPosition,Is.EqualTo(position).Within(.001));
            Input("input-rifle.damage").text="no number";yield return null;Assert.That(Button("lab-save").interactable,Is.False);Assert.That(Input("input-rifle.damage").text,Is.EqualTo("no number"));
            Click("lab-back");yield return null;Assert.That(Button("cancel"),Is.Not.Null);Assert.That(Button("lab-back").interactable,Is.False);
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.East));yield return null;yield return null;InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
            Assert.That(Input("input-rifle.damage").text,Is.EqualTo("no number"));Assert.That(ground.LocalSeatCount,Is.EqualTo(seats));
            Click("reset-rifle.damage");yield return null;Assert.That(Button("lab-save").interactable,Is.False);
            EventSystem.current.SetSelectedGameObject(Button("plus-rifle.damage").gameObject);
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));yield return null;yield return null;InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
            Assert.That(Input("input-rifle.damage").text,Is.EqualTo("11"));Assert.That(ground.GetComponentsInChildren<Text>().Single(t=>t.name=="lab-hint").text,Does.Contain("rifle.damage"));
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadRight));yield return null;yield return null;InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("reset-rifle.damage"));
            Input("input-rifle.damage").text="33";Click("lab-save");yield return null;Assert.That(ground.LabSavedIdentity,Does.Contain("v2-"));Assert.That(ground.CombatProfile.Get("rifle.damage"),Is.EqualTo(original));
            Click("lab-back");Click("main-action-0");yield return null;Assert.That(ground.CombatProfile.Get("rifle.damage"),Is.EqualTo(33));
            var identity=ground.GetComponentsInChildren<Text>().Single(t=>t.name=="setup-lab-identity");Canvas.ForceUpdateCanvases();Assert.That(identity.text,Does.Contain("SHA256"));Assert.That(identity.preferredHeight,Is.LessThanOrEqualTo(identity.rectTransform.rect.height+1),"Saved revision/hash must fit in setup summary");
            ground.StartCombatReview(new[]{pad},true,true);yield return null;Assert.That(ground.Running,Is.True);
            var oldSession=ground.Session;var frozen=oldSession.Capture().DesignProfile;Assert.That(frozen,Is.Not.Null);
            typeof(ProvingGround).GetMethod("OpenDesignLab",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ground,null);Assert.That(ground.Running,Is.True,"Lab cannot open while match is running");
            var copy=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(ground.CombatProfile));
            typeof(ProvingGround).GetMethod("ToMainMenu",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ground,null);Click("main-action-2");yield return null;Input("input-rifle.damage").text="34";Click("lab-save");yield return null;
            Assert.That(oldSession.Capture().DesignProfile.Hash,Is.EqualTo(frozen.Hash));
            var wrong=oldSession.Capture();wrong.DesignProfile.Hash="wrong";Assert.Throws<ArgumentException>(()=>oldSession.Restore(wrong));
            Assert.That(copy.Get("rifle.damage"),Is.EqualTo(33));Assert.That(ground.CombatProfile.Get("rifle.damage"),Is.EqualTo(33));
            Input("lab-search").text="jumpSpeed";yield return null;Assert.That(Input("input-player.movement.jumpSpeed"),Is.Not.Null);
            EventSystem.current.SetSelectedGameObject(Input("input-player.movement.jumpSpeed").gameObject);
            Input("lab-search").text="rifle.damage";yield return null;Input("lab-search").text="";yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.Not.Null,"Search restores focus when the previous row is removed");
            Click("lab-clear");yield return null;Assert.That(Button("lab-save").interactable,Is.False);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);Environment.SetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY",oldEnvironment);if(directory!=null&&Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
