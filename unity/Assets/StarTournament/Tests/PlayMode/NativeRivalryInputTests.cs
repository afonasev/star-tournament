using System;
using System.Collections;
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
    public sealed class NativeRivalryInputTests
    {
        Scene scene;
        ProvingGround ground;
        readonly Gamepad[] pads=new Gamepad[2];
        Keyboard keyboard;
        bool lastMap;string priorMap;float priorVolume;
        [SetUp] public void MuteQa(){priorVolume=AudioListener.volume;AudioListener.volume=0;}
        T Field<T>(string name)=>(T)typeof(ProvingGround).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ground);
        Button Action(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        IEnumerator Load()
        {
            lastMap=PlayerPrefs.HasKey(ProvingGround.LastPlayedMapPreferenceKey);priorMap=PlayerPrefs.GetString(ProvingGround.LastPlayedMapPreferenceKey);
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<ProvingGround>()).Single();
            for(int i=0;i<2;i++)pads[i]=InputSystem.AddDevice<Gamepad>();
            var config=NativeMatchConfiguration.Default(ground.MatchProfile);config.TargetEnabled=true;config.TargetPoints=1000;ground.ConfigureBotTacticsReview(config);
            var roster=NativeMatchRoster.Ffa(4);
            var participants=Enumerable.Range(0,4).Select(i=>new NativeParticipantInfo(i<2?NativeParticipantKind.LocalHuman:NativeParticipantKind.Bot,"Участник "+i,NativeStandingsView.Palette[i],i<2?-1:1)).ToArray();
            ground.StartBotReview(new NativeMatchComposition(roster,participants,new[]{0,1}),917,pads.Cast<InputDevice>().ToArray());
            yield return null;
            ground.Session.Match.BeginTick();ground.Session.Match.RecordDamage(1,0,new DamageResult(100,true));ground.Session.Match.EndTick();
        }
        [UnityTest] public IEnumerator IndependentHeldTabsUseTheirSeatAndResetForARepeat()
        {
            yield return Load();
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.Select));
            InputSystem.QueueStateEvent(pads[1],new GamepadState().WithButton(GamepadButton.Select));yield return new WaitForFixedUpdate();yield return null;yield return null;
            var tables=Field<NativeStandingsView[]>("standings");
            Assert.That(tables[0].Root.activeSelf,Is.True);Assert.That(tables[1].Root.activeSelf,Is.True);
            Assert.That(tables[0].PerspectiveParticipant,Is.EqualTo(0));Assert.That(tables[1].PerspectiveParticipant,Is.EqualTo(1));
            Text Duel(NativeStandingsView view,string name)=>view.Root.GetComponentsInChildren<Text>().Single(t=>t.name=="cell-0"&&t.text==name).transform.parent.Find("duel").GetComponent<Text>();
            Assert.That(Duel(tables[0],"Участник 1").text,Is.EqualTo("1 : 0"));Assert.That(Duel(tables[1],"Участник 0").text,Is.EqualTo("0 : 1"));
            Assert.That(Duel(tables[0],"Участник 1").color.g,Is.GreaterThan(Duel(tables[0],"Участник 1").color.r));
            Assert.That(Duel(tables[1],"Участник 0").color.r,Is.GreaterThan(Duel(tables[1],"Участник 0").color.g));
            InputSystem.QueueStateEvent(pads[0],new GamepadState());yield return new WaitForFixedUpdate();yield return null;yield return null;
            Assert.That(tables[0].Root.activeSelf,Is.False);Assert.That(tables[1].Root.activeSelf,Is.True);
        }
        IEnumerator Finish()
        {
            ground.Session.Match.BeginTick();for(int i=0;i<40;i++)ground.Session.Match.RecordDamage(3,0,new DamageResult(100,true));ground.Session.Match.EndTick();
            Assert.That(ground.Session.Match.Phase,Is.EqualTo(NativeMatchPhase.Finished));
            yield return new WaitForFixedUpdate();yield return null;
        }
        IEnumerator Press(GamepadButton button)
        {
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(button));yield return null;yield return null;
            InputSystem.QueueStateEvent(pads[0],new GamepadState());yield return null;
        }
        [UnityTest] public IEnumerator ResultsNavigationSelectsRowsWithoutRepeatingAndReturnsToActions()
        {
            yield return Load();yield return Finish();
            var table=Field<NativeStandingsView>("results");var events=EventSystem.current;
            Assert.That(table.SelectedParticipant,Is.EqualTo(0));Assert.That(events.currentSelectedGameObject,Is.EqualTo(Action("Повторить матч").gameObject));
            yield return Press(GamepadButton.DpadUp);
            Assert.That(events.currentSelectedGameObject.transform.parent.gameObject,Is.EqualTo(table.Root));
            var selectedBefore=table.SelectedParticipant;yield return Press(GamepadButton.South);
            Assert.That(table.SelectedParticipant,Is.EqualTo(selectedBefore));Assert.That(ground.Running,Is.False);
            keyboard=InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.UpArrow));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            Assert.That(events.currentSelectedGameObject.transform.parent.gameObject,Is.EqualTo(table.Root));
            events.SetSelectedGameObject(Action("В главное меню").gameObject);yield return Press(GamepadButton.DpadUp);
            Assert.That(events.currentSelectedGameObject.transform.parent.gameObject,Is.EqualTo(table.Root));
            var repeatRect=Action("Повторить матч").GetComponent<RectTransform>();var menuRect=Action("В главное меню").GetComponent<RectTransform>();
            Assert.That(repeatRect.position.y,Is.EqualTo(menuRect.position.y).Within(1),"results actions use one bottom row");
            Action("Повторить матч").onClick.Invoke();yield return null;
            Assert.That(ground.Running,Is.True);Assert.That(table.SelectedParticipant,Is.EqualTo(-1),"a repeat clears the old selection");
            ground.Session.Match.BeginTick();ground.Session.Match.RecordDamage(1,0,new DamageResult(100,true));ground.Session.Match.EndTick();
            yield return Finish();Assert.That(table.SelectedParticipant,Is.EqualTo(0));
            Action("В главное меню").onClick.Invoke();yield return null;Assert.That(table.Root.activeSelf,Is.False);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach(var pad in pads)if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
            if(lastMap)PlayerPrefs.SetString(ProvingGround.LastPlayedMapPreferenceKey,priorMap);else PlayerPrefs.DeleteKey(ProvingGround.LastPlayedMapPreferenceKey);
            PlayerPrefs.Save();AudioListener.volume=priorVolume;
        }
    }
}
