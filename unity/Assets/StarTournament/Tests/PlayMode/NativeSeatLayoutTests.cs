using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeSeatLayoutTests
    {
        Scene scene;
        ProvingGround ground;
        readonly Gamepad[] pads=new Gamepad[4];
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
            for(int i=0;i<4;i++)pads[i]=InputSystem.AddDevice<Gamepad>();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach(var p in pads)if(p!=null&&p.added)InputSystem.RemoveDevice(p);
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
        }
        void AssertLayout(int count)
        {
            Assert.That(ground.LocalSeatCount,Is.EqualTo(count));
            Assert.That(ground.Session.ParticipantCount,Is.EqualTo(count));
            Assert.That(ground.Session.LiveTargets().Length,Is.EqualTo(count));
            Assert.That(ground.Session.Match.Read().Standings.Length,Is.EqualTo(count));
            Assert.That(ground.GetComponentsInChildren<CharacterController>().Count(c=>c.enabled),Is.EqualTo(count));
            Assert.That(ground.GetComponentsInChildren<Camera>().Count(c=>c.enabled),Is.EqualTo(count));
            for(int i=0;i<4;i++)
            {
                var cameraTransform=ground.transform.Find("seat-camera-"+(i+1));
                var cam=cameraTransform?cameraTransform.GetComponent<Camera>():null;
                var root=ground.transform.Find("native-ui/seat-viewport-"+i).GetComponent<RectTransform>();
                Assert.That(root.gameObject.activeSelf,Is.EqualTo(i<count));
                if(i>=count){Assert.That(cam,Is.Null,"Unused local cameras are disposed");continue;}
                var expected=LocalSeatLayout.Viewport(count,i);Assert.That(cam.rect,Is.EqualTo(expected));
                Assert.That(root.anchorMin,Is.EqualTo(new Vector2(expected.xMin,expected.yMin)));
                Assert.That(root.anchorMax,Is.EqualTo(new Vector2(expected.xMax,expected.yMax)));
            }
            Assert.That(ground.transform.Find("native-ui/persistent-standings").gameObject.activeSelf,Is.EqualTo(count==3));
            Assert.That(ground.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true).Length,Is.EqualTo(1));
            Assert.That(ground.GetComponentsInChildren<AudioListener>(true).Length,Is.EqualTo(1));
            Assert.That(ground.GetComponentsInChildren<ProvingArena>(true).Length,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator SharedTimerAndPerSeatHudFollowOneToFourViewportLayouts()
        {
            yield return Load();
            ground.AddBot(); // A single viewport still needs an opponent in the match roster.
            foreach(int count in new[]{1,2,3,4})
            {
                if(count==2)ground.RemoveBot(0);
                ground.StartCombatReview(pads.Take(count).ToArray());
                yield return null;yield return new WaitForFixedUpdate();yield return null;
                Assert.That(ground.Running,Is.True);
                var panel=ground.transform.Find("native-ui/match-timer-panel").GetComponent<RectTransform>();
                Assert.That(panel.gameObject.activeSelf,Is.True);
                Assert.That(panel.Find("match-timer").GetComponent<Text>().text,Does.Match(@"^\d\d:\d\d$"));
                Assert.That(panel.anchorMin,Is.EqualTo(count<3?new Vector2(.5f,1):new Vector2(.5f,.5f)));
                Assert.That(panel.pivot,Is.EqualTo(new Vector2(.5f,1f)));
                if(count>=3) Assert.That(panel.anchoredPosition.y,Is.EqualTo(-ground.Profile.Get("ui.centerTimerSeamGap")).Within(.01f));
                if(count==3)
                {
                    Canvas.ForceUpdateCanvases();
                    var table=ground.transform.Find("native-ui/persistent-standings/row-0").GetComponent<RectTransform>();
                    var timerCorners=new Vector3[4];var tableCorners=new Vector3[4];
                    panel.GetWorldCorners(timerCorners);table.GetWorldCorners(tableCorners);
                    Assert.That(tableCorners[1].y,Is.LessThan(timerCorners[0].y));
                }
                Assert.That(ground.GetComponentsInChildren<Text>(true).Count(t=>t.name=="match-timer"),Is.EqualTo(1));
                for(int seat=0;seat<count;seat++)
                {
                    var root=ground.transform.Find("native-ui/seat-viewport-"+seat);
                    Assert.That(root.Find("timer-"+seat),Is.Null);
                    var name=root.Find("seat-name-"+seat).GetComponent<Text>();
                    Assert.That(name.text,Does.Contain("Игрок"));
                    Assert.That(name.alignment,Is.EqualTo(TextAnchor.LowerCenter));
                    var icons=root.GetComponentsInChildren<HudIndicatorIcon>(true);
                    Assert.That(icons.Select(i=>i.Kind),Is.EquivalentTo(new[]{HudIndicatorIcon.IconKind.Heart,HudIndicatorIcon.IconKind.Shield,HudIndicatorIcon.IconKind.Cartridges}));
                    Assert.That(root.GetComponentsInChildren<Text>(true).Single(t=>t.name=="ammo-"+seat).text,Does.Not.Contain("ДВУСТВОЛКА"));
                }
                Button("В главное меню").onClick.Invoke();yield return null;
            }
        }
        [UnityTest] public IEnumerator ChangingRosterAndRepeatingCannotLeaveHiddenParticipantsOrStaleRows()
        {
            yield return Load();
            foreach(int count in new[]{4,2,3,4})
            {
                ground.StartCombatReview(pads.Take(count).ToArray());yield return null;yield return new WaitForFixedUpdate();yield return null;
                AssertLayout(count);
                var old=ground.Session;old.ApplyDamage(1,1,100,0,1);
                InputSystem.QueueStateEvent(pads[1],new GamepadState().WithButton(GamepadButton.Select));
                yield return null;yield return new WaitForFixedUpdate();yield return null;
                var table=ground.transform.Find("native-ui/standings-1");Assert.That(table.gameObject.activeSelf,Is.True);
                Assert.That(ground.transform.Find("native-ui/standings-0").gameObject.activeSelf,Is.False);
                Assert.That(table.Find("row-4").gameObject.activeSelf,Is.EqualTo(count==4));
                Assert.That(table.Find("row-1/cell-1").GetComponent<Text>().text,Is.EqualTo("1"));
                if(count==3)
                {
                    var persistent=ground.transform.Find("native-ui/persistent-standings");
                    Assert.That(persistent.Find("row-1/cell-1").GetComponent<Text>().text,Is.EqualTo("1"));
                    Assert.That(persistent.GetComponentsInChildren<Graphic>().All(g=>!g.raycastTarget),Is.True);
                }
                Button("seats-minus").onClick.Invoke();Assert.That(ground.LocalSeatCount,Is.EqualTo(count),"Cannot resize a running match");
                InputSystem.QueueStateEvent(pads[1],new GamepadState());
                InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.Start));yield return null;yield return null;
                Assert.That(ground.Running,Is.False);double clock=old.Time;
                yield return new WaitForSecondsRealtime(.1f);Assert.That(old.Time,Is.EqualTo(clock));
                InputSystem.QueueStateEvent(pads[0],new GamepadState{rightTrigger=1});yield return null;
                Button("Повторить матч").onClick.Invoke();yield return null;yield return new WaitForFixedUpdate();yield return null;
                AssertLayout(count);Assert.That(ground.Session.ShotCount,Is.Zero);
                Assert.That(ground.Session.Match.Read().Standings.All(r=>r.Score==0&&r.Deaths==0),Is.True);
                Assert.That(old.ApplyDamage(0,1,100).Applied,Is.Zero);
                Assert.That(ground.GetComponentsInChildren<Transform>(true).Any(t=>t.name.StartsWith("corpse-")),Is.False);
                InputSystem.QueueStateEvent(pads[0],new GamepadState());
                // Result via existing domain clock with an explicit gameplay fixture; not a timing measurement.
                ground.Session.ApplyDamage(1,1,100,0,1);
                while(ground.Session.Match.Phase==NativeMatchPhase.Running){ground.Session.Match.BeginTick();ground.Session.Match.EndTick();}
                yield return new WaitForFixedUpdate();yield return null;
                var result=ground.transform.Find("native-ui/setup-pause/results-table");
                Assert.That(result.gameObject.activeSelf,Is.True);Assert.That(result.Find("row-4").gameObject.activeSelf,Is.EqualTo(count==4));
                Button("Повторить матч").onClick.Invoke();yield return null;yield return new WaitForFixedUpdate();yield return null;AssertLayout(count);
                Button("В главное меню").onClick.Invoke();yield return null;
                Assert.That(ground.transform.Find("native-ui/persistent-standings").gameObject.activeSelf,Is.False);
            }
        }
        [UnityTest] public IEnumerator CliProbeOverrideDoesNotLeakIntoNextDiagnosticRoster()
        {
            yield return Load();
            var field=typeof(ProvingGround).GetField("probeCameraCount",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            foreach(var pair in new[]{new[]{2,3},new[]{4,2}})
            {
                field.SetValue(ground,pair[0]);
                Button("Диагностика четырёх камер · без управления").onClick.Invoke();yield return null;
                Assert.That(ground.GetComponentsInChildren<Camera>().Count(c=>c.enabled),Is.EqualTo(System.Math.Min(pair[0],ground.LocalSeatCount)));
                Button("В главное меню").onClick.Invoke();yield return null;
                while(ground.LocalSeatCount>pair[1])Button("seats-minus").onClick.Invoke();
                while(ground.LocalSeatCount<pair[1])Button("seats-plus").onClick.Invoke();
                Button("Диагностика четырёх камер · без управления").onClick.Invoke();yield return null;AssertLayout(pair[1]);
                Button("В главное меню").onClick.Invoke();yield return null;
            }
        }
        [UnityTest] public IEnumerator TwoGamepadsJoinThroughSetupAndThirdDeviceCannotControlOrPauseGame()
        {
            yield return Load();Button("seats-minus").onClick.Invoke();Button("seats-minus").onClick.Invoke();
            Assert.That(Button("Начать — четыре игрока").interactable,Is.False);
            for(int i=0;i<2;i++)
            {
                InputSystem.QueueStateEvent(pads[i],new GamepadState().WithButton(GamepadButton.Start));yield return null;yield return null;
                InputSystem.QueueStateEvent(pads[i],new GamepadState());yield return null;
            }
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.South));yield return null;yield return null;
            Assert.That(ground.Running,Is.True);yield return null;AssertLayout(2);
            InputSystem.QueueStateEvent(pads[0],new GamepadState());
            InputSystem.QueueStateEvent(pads[2],new GamepadState{rightTrigger=1,leftStick=Vector2.one}.WithButton(GamepadButton.Start));
            yield return null;yield return new WaitForFixedUpdate();Assert.That(ground.Running,Is.True);Assert.That(ground.Session.ShotCount,Is.Zero);
            InputSystem.RemoveDevice(pads[2]);yield return null;Assert.That(ground.Running,Is.True);
            InputSystem.RemoveDevice(pads[1]);yield return null;yield return null;
            Assert.That(ground.Running,Is.False);Assert.That(Button("Продолжить").interactable,Is.False);
            var text=ground.transform.Find("native-ui/setup-pause/menu/status").GetComponent<Text>();Assert.That(text.text,Does.Contain("P2"));
            InputSystem.AddDevice(pads[1]);yield return null;Assert.That(ground.Running,Is.False);
            Assert.That(Button("Продолжить").interactable,Is.True);
            Button("Продолжить").onClick.Invoke();yield return null;Assert.That(ground.Running,Is.True);
            ground.SendMessage("OnApplicationFocus",false);Assert.That(ground.Running,Is.False);
        }
    }
}
