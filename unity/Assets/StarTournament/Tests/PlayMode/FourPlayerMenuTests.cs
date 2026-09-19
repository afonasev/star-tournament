using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class FourPlayerMenuTests
    {
        readonly Gamepad[] pads=new Gamepad[4];
        Scene scene;
        [UnityTest]
        public IEnumerator FramesStayWithAssignedSeatAndEdgesAreConsumedOnce()
        {
            var seats=new SeatInputCoordinator();
            for(int i=0;i<4;i++) { pads[i]=InputSystem.AddDevice<Gamepad>();seats.Assign(i,pads[i]); }
            yield return null;
            seats.Capture(ProvingProfile.CreateDefault(),Time.fixedDeltaTime);
            InputSystem.QueueStateEvent(pads[2],new GamepadState{leftStick=Vector2.right,rightTrigger=1});
            yield return null;
            seats.Capture(ProvingProfile.CreateDefault(),Time.fixedDeltaTime);
            for(int i=0;i<4;i++)
            {
                var frame=seats.Consume(i);
                Assert.That(frame.Move.x,Is.EqualTo(i==2?1:0));Assert.That(frame.Fire,Is.EqualTo(i==2));
            }
            Assert.That(seats.Consume(2).Fire,Is.False);
            seats.Clear();Assert.That(seats.Consume(2).Move,Is.EqualTo(Vector2.zero));
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach(var pad in pads) if(pad!=null && pad.added)InputSystem.RemoveDevice(pad);
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTest]
        public IEnumerator FourGamepadsCanJoinStartPauseAndResumeWithoutMouse()
        {
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);
            scene=SceneManager.GetSceneByName("ProvingGround");
            yield return null;
            var ground=Object.FindFirstObjectByType<ProvingGround>(); Assert.That(ground,Is.Not.Null);
            Assert.That(ground.Running,Is.False);
            for(int i=0;i<4;i++)
            {
                pads[i]=InputSystem.AddDevice<Gamepad>();
                InputSystem.QueueStateEvent(pads[i],new GamepadState().WithButton(GamepadButton.Start));
                yield return null;yield return null;
                InputSystem.QueueStateEvent(pads[i],new GamepadState());yield return null;
            }
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.South));
            yield return null;yield return null;
            Assert.That(ground.Running,Is.True,"Gamepad Submit must activate the selected four-player Start button");
            InputSystem.QueueStateEvent(pads[0],new GamepadState());yield return null;
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.Start));
            yield return null;yield return null;Assert.That(ground.Running,Is.False);
            InputSystem.QueueStateEvent(pads[0],new GamepadState());yield return null;
            InputSystem.QueueStateEvent(pads[0],new GamepadState().WithButton(GamepadButton.South));
            yield return null;yield return null;Assert.That(ground.Running,Is.True,"Resume must be reachable without a mouse");
            InputSystem.RemoveDevice(pads[3]);yield return null;yield return null;
            Assert.That(ground.Running,Is.False,"Disconnect pauses the whole proving ground");
        }
    }
}
