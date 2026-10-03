#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    public sealed class NativeLunarLaboratoryReview:MonoBehaviour
    {
        ProvingGround ground;string directory;bool offscreen;
        IEnumerator Capture(string name)
        {
            ground.SendMessage("RefreshInterface");ground.SendMessage("LateUpdate");
            if(offscreen)
            {
                // Autonomous Player render diagnostic: no UI/focus/device interaction, no frame-end wait in batch mode.
                var target=new RenderTexture(1920,1080,24);target.Create();var old=RenderTexture.active;var pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);
                try
                {
                    foreach(var camera in ground.GetComponentsInChildren<Camera>().Where(c=>c.enabled).OrderBy(c=>c.depth))
                    {var previous=camera.targetTexture;try{camera.targetTexture=target;camera.Render();}finally{camera.targetTexture=previous;}}
                    RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(directory,name+".png"),pixels.EncodeToPNG());
                }
                finally{RenderTexture.active=old;DestroyImmediate(pixels);target.Release();DestroyImmediate(target);}
                yield return null;yield break;
            }
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));yield return new WaitForSecondsRealtime(.2f);
        }
        NativeMatchComposition Bots(int n,bool split=false)=>new NativeMatchComposition(NativeMatchRoster.Ffa(n),Enumerable.Range(0,n).Select(i=>new NativeParticipantInfo(NativeParticipantKind.Bot,"Bot "+(i+1),NativeParticipantColors.For(i),(int)NativeBotDifficulty.Hard)).ToArray(),split?new[]{0,1,2,3}:new[]{0});
        IEnumerator Start()
        {
            Application.runInBackground=true;AudioListener.volume=0;ground=GetComponent<ProvingGround>();var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-lunarEvidence");offscreen=args.Contains("-lunarOffscreen");directory=i>=0?args[i+1]:Path.Combine(Application.persistentDataPath,"lunar-review");Directory.CreateDirectory(directory);
            while(!offscreen&&!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            Screen.SetResolution(1920,1080,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(.5f);
            ground.SendMessage("OpenSetup");ground.SelectAuthoredMap(LunarLaboratoryCatalog.Id);ground.SendMessage("ShowSetupStep",(object)0);if(!offscreen)yield return Capture("00-map-selection");
            ground.StartBotReview(Bots(8),20261003);ground.FullHealReviewManualTick=true;
            var poses=new[]{
                new Vector3(0,0,-28),new Vector3(25,0,-25),new Vector3(0,0,-22),
                new Vector3(0,.02f,-10),new Vector3(-6,.02f,10),new Vector3(10,.02f,-9),
                new Vector3(-10,4.5f,0),new Vector3(-4,4.5f,10),new Vector3(4,4.5f,10),new Vector3(0,4.5f,-10),
                new Vector3(-13.5f,4.5f,13.5f),new Vector3(22.5f,4.5f,-5),new Vector3(-18,0,18),new Vector3(18,0,-18),
                new Vector3(-12.7f,4.5f,1.5f),new Vector3(12.7f,4.5f,1.5f),
                new Vector3(-6.5f,4.5f,10.4f),new Vector3(23,13,-26),new Vector3(0,.02f,-3.2f),new Vector3(-24,0,18)};
            var names=new[]{"01-yard-earth-sun","02-yard-east","03-south-testing","04-ground-divider","05-ground-analysis","06-store-shotgun","07-upper-corridor","08-lab-a","09-lab-b","10-upper-hall","11-balcony-nw","12-balcony-east","13-west-stair","14-east-stair","15-west-comms","16-east-power","18-north-balcony-exit","19-roof-overview","20-cargo-passages","21-earth-and-fence"};
            var yaw=new[]{0f,325,0,0,130,300,90,200,160,0,130,0,180,0,270,90,0,318,180,327};
            var pitch=new[]{-20f,-10,0,0,0,5,0,0,0,0,-5,-5,-12,-12,-10,-10,0,12,0,-32};
            for(int q=0;q<poses.Length;q++){ground.PlaceCombatReviewSeat(0,poses[q],pitch[q],yaw[q]);yield return Capture(names[q]);}
            ground.SendMessage("Menu");ground.StartBotReview(Bots(8,true),20261003);ground.FullHealReviewManualTick=true;yield return Capture("17-four-views");
            if(args.Contains("-lunarVisualOnly")){File.WriteAllText(Path.Combine(directory,"visual-complete.json"),"{\"muted\":true,\"scriptedCameras\":true,\"humanAcceptance\":false}");Application.Quit(0);yield break;}
            ground.FullHealReviewManualTick=false;ground.enabled=false;int kills=0,collections=0,ticks=0;ground.Session.Died+=_=>kills++;ground.Session.PickupCollected+=(a,b,c)=>collections++;
            var actions=new LocalAction[8];float dt=1/ground.Profile.Get("simulation.fixedTickHz");
            while(ground.Session.Match.Phase!=NativeMatchPhase.Finished&&ground.Session.Time<620)
            {Array.Clear(actions,0,actions.Length);ground.BotDriver.ProduceActions(actions,dt);ground.Session.Tick(actions,dt);if(++ticks%120==0)yield return null;}
            File.WriteAllText(Path.Combine(directory,"natural-match.json"),JsonUtility.ToJson(ground.Session.Capture(),true));
            bool pass=ground.Session.Match.Phase==NativeMatchPhase.Finished&&kills>0&&collections>0;
            File.WriteAllText(Path.Combine(directory,"complete.json"),"{\"status\":\""+(pass?"PASS":"FAIL")+"\",\"map\":\""+LunarLaboratoryCatalog.Identity+"\",\"muted\":true,\"seed\":20261003,\"kills\":"+kills+",\"collections\":"+collections+",\"humanAcceptance\":false}");Application.Quit(pass?0:2);
        }
    }
}
#endif
