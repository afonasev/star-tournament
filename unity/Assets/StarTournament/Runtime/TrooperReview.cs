#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    /// <summary>Explicit CLI synthetic evidence only, never a physical or artistic acceptance.</summary>
    public sealed class TrooperReview : MonoBehaviour
    {
        ProvingGround ground; string directory; readonly Gamepad[] pads=new Gamepad[4];
        [Serializable] sealed class PoseEvidence { public string owner,clip;public double time;public Vector3 root,hand; }
        [Serializable] sealed class Evidence
        {
            public string status="DIAGNOSTIC_NOT_ARTISTIC_PHYSICAL_OR_PERFORMANCE_ACCEPTANCE",state,profile;
            public int width,height,seats,cameras,skinInstances,uniqueSkinnedMeshes,uniqueMaterials,uniqueTextures;
            public bool focused,muted,running; public double clock;public PoseEvidence[] poses;
        }
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        IEnumerator Start()
        {
            ground=GetComponent<ProvingGround>(); var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-trooperEvidence");
            directory=i>=0&&i+1<args.Length?args[i+1]:Path.Combine(Application.persistentDataPath,"trooper-review");Directory.CreateDirectory(directory);
            while(!Application.isFocused)yield return null;
            yield return new WaitForSecondsRealtime(1);
            for(i=0;i<4;i++)pads[i]=InputSystem.AddDevice<Gamepad>();
            foreach(int count in new[]{2,3,4})
            {
                ground.StartCombatReview(pads.Take(count).ToArray());yield return new WaitForSeconds(.2f);
                ground.PlaceCombatReviewSeat(0,new Vector3(-8,0,-3),0);
                ground.PlaceCombatReviewSeat(1,new Vector3(-8,0,1),0,180);
                yield return new WaitForSeconds(.4f);yield return Capture(count+"-idle-hands");
                InputSystem.QueueStateEvent(pads[1],new GamepadState{leftStick=new Vector2(.25f,0)});
                yield return new WaitForSeconds(.35f);yield return Capture(count+"-walk");
                InputSystem.QueueStateEvent(pads[1],new GamepadState{leftStick=Vector2.left});
                yield return new WaitForSeconds(.3f);yield return Capture(count+"-run");
                InputSystem.QueueStateEvent(pads[1],new GamepadState());yield return new WaitForSeconds(.3f);
                // Point both shooters away from one another, so accepted fire is visible without accidental death.
                ground.PlaceCombatReviewSeat(0,new Vector3(-8,0,-3),-25);
                InputSystem.QueueStateEvent(pads[0],new GamepadState{rightTrigger=1});
                InputSystem.QueueStateEvent(pads[1],new GamepadState{rightTrigger=1});
                yield return new WaitForSeconds(.12f);yield return Capture(count+"-fire");
                InputSystem.QueueStateEvent(pads[0],new GamepadState());InputSystem.QueueStateEvent(pads[1],new GamepadState());
                yield return new WaitForSeconds(.45f);yield return Capture(count+"-aim");
                ground.PlaceCombatReviewSeat(0,new Vector3(-8,0,-3),0);
                ground.Session.ApplyDamage(1,ground.Session.Life(1).Life,20,0,ground.Session.Life(0).Life);
                yield return new WaitForSeconds(.18f);yield return Capture(count+"-hit");
                ground.SendMessage("Pause","Проверка паузы анимации");yield return Capture(count+"-pause");
                yield return new WaitForSecondsRealtime(.3f);yield return Capture(count+"-paused-clock");
                Button("Продолжить").onClick.Invoke();yield return new WaitForSeconds(.3f);
                ground.Session.ApplyDamage(1,ground.Session.Life(1).Life,100,0,ground.Session.Life(0).Life);
                yield return new WaitForSeconds(.7f);yield return Capture(count+"-death");
                yield return new WaitForSeconds(1.1f);yield return Capture(count+"-death-final");
                yield return new WaitForSeconds(2);yield return Capture(count+"-respawn");
                ground.SendMessage("Pause","Повторить проверку");Button("Повторить матч").onClick.Invoke();
                yield return new WaitForSeconds(.2f);yield return Capture(count+"-repeat");
            }
            File.WriteAllText(Path.Combine(directory,"complete.json"),"{\"complete\":true,\"physicalAcceptance\":false}");
            yield return new WaitForSecondsRealtime(1);Application.Quit();
        }
        IEnumerator Capture(string state)
        {
            yield return new WaitForEndOfFrame();
            var skins=ground.GetComponentsInChildren<SkinnedMeshRenderer>();var renderers=ground.GetComponentsInChildren<Renderer>();
            var materials=renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct().ToArray();
            var textures=materials.SelectMany(m=>m.GetTexturePropertyNames().Select(n=>m.GetTexture(n))).Where(t=>t).Distinct().ToArray();
            var report=new Evidence{state=state,profile=ground.TrooperProfile.Id+"@"+ground.TrooperProfile.Version,width=Screen.width,height=Screen.height,
                seats=ground.Session.ParticipantCount,cameras=ground.GetComponentsInChildren<Camera>().Count(c=>c.enabled),focused=Application.isFocused,muted=AudioListener.volume==0,running=ground.Running,
                clock=ground.Session.Time,skinInstances=skins.Length,uniqueSkinnedMeshes=skins.Select(s=>s.sharedMesh).Distinct().Count(),uniqueMaterials=materials.Length,uniqueTextures=textures.Length,
                poses=ground.GetComponentsInChildren<TrooperVisual>().Select(v=>new PoseEvidence{owner=v.name,clip=v.State,time=v.SampleTime,root=v.transform.position,
                    hand=v.GetComponentsInChildren<Transform>().First(t=>t.name=="RightHand").position}).ToArray()};
            File.WriteAllText(Path.Combine(directory,state+".json"),JsonUtility.ToJson(report,true));
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,state+".png"));yield return null;
        }
        void OnDestroy(){foreach(var p in pads)if(p!=null&&p.added)InputSystem.RemoveDevice(p);}
    }
}
#endif

#endif
