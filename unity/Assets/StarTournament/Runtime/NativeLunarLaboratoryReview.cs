#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
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
            if(name.StartsWith("sign-"))foreach(var camera in ground.GetComponentsInChildren<Camera>().Where(c=>c.enabled))camera.fieldOfView=110;
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
                new Vector3(-.6f,.02f,-11.3f),new Vector3(-6,.02f,10),new Vector3(10,.02f,-9),
                new Vector3(-10,4.5f,0),new Vector3(-4,4.5f,10),new Vector3(4,4.5f,10),new Vector3(0,4.5f,-10),
                new Vector3(-13.5f,4.5f,13.5f),new Vector3(22.5f,4.5f,-5),new Vector3(-18,0,18),new Vector3(18,0,-18),
                new Vector3(-12.7f,4.5f,1.5f),new Vector3(12.7f,4.5f,1.5f),
                new Vector3(-6.5f,4.5f,10.4f),new Vector3(23,13,-26),new Vector3(0,.02f,-3.2f),new Vector3(-24,0,18),new Vector3(17,0,-26),new Vector3(27.5f,0,8)};
            var names=new[]{"01-yard-earth-sun","02-yard-east","03-south-testing","04-ground-divider","05-ground-analysis","06-store-shotgun","07-upper-corridor","08-lab-a","09-lab-b","10-upper-hall","11-balcony-nw","12-balcony-east","13-west-stair","14-east-stair","15-west-comms","16-east-power","18-north-balcony-exit","19-roof-overview","20-cargo-passages","21-earth-and-fence","22-yard-cargo","23-service-pipes"};
            var yaw=new[]{0f,325,0,0,130,300,90,200,160,0,130,0,180,0,270,90,0,318,180,327,35,18};
            var pitch=new[]{-20f,-10,0,0,0,5,0,0,0,0,-5,-5,-12,-12,-10,-10,0,12,0,-32,5,6};
            for(int q=0;q<poses.Length;q++){ground.PlaceCombatReviewSeat(0,poses[q],pitch[q],yaw[q]);yield return Capture(names[q]);}
            if(args.Contains("-lunarF6"))
            {
                var f6=new[]{new Vector3(-18.8f,2.25f,-1.8f),new Vector3(-11,.02f,-1.75f),new Vector3(0,.02f,10),new Vector3(0,.02f,-10),new Vector3(-8,4.5f,8),new Vector3(8,4.5f,-8),new Vector3(-3,4.5f,6.25f),new Vector3(0,4.5f,-1),new Vector3(27,0,29)};
                var fy=new[]{270f,270,180,0,315,135,90,180,45};
                for(int q=0;q<f6.Length;q++){ground.PlaceCombatReviewSeat(0,f6[q],q==0?0:5,fy[q]);yield return Capture("f6-"+(q+1).ToString("00"));}
            }
            if(args.Contains("-lunarF9"))
            {
                foreach(int side in new[]{-1,1})foreach(bool upper in new[]{false,true})
                foreach(float x in !upper||side<0?new[]{0f}:new[]{-6.5f,6.5f})
                foreach(bool outside in new[]{false,true})
                {
                    float y=upper?LunarLaboratoryCatalog.Upper:.02f;
                    float z=side*(outside?14.4f:9.6f);
                    ground.PlaceCombatReviewSeat(0,new Vector3(x,y,z),-8,(side>0)^outside?0:180);
                    yield return Capture("f9-"+(side<0?"south":"north")+"-"+(upper?"upper":"ground")+"-"+x+"-"+(outside?"outside":"inside"));
                }
            }
            if(args.Contains("-lunarF10"))
            {
                foreach(int side in new[]{-1,1})foreach(int direction in new[]{-1,1})
                {
                    ground.PlaceCombatReviewSeat(0,new Vector3(side*10.6f,.02f,-direction*4.5f),0,direction>0?0:180);
                    yield return Capture("f10-"+(side<0?"west":"east")+"-"+(direction>0?"northbound":"southbound"));
                }
                foreach(int side in new[]{-1,1})
                {
                    ground.PlaceCombatReviewSeat(0,new Vector3(side*6.5f,.02f,side*4.5f),0,side*90);
                    yield return Capture("f10-"+(side<0?"west":"east")+"-turn");
                }
            }
            if(args.Contains("-lunarF7"))
                foreach(int side in new[]{-1,1}){ground.PlaceCombatReviewSeat(0,new Vector3(side*9,.02f,-2.5f),0,side*90);yield return Capture("f7-"+(side<0?"west":"east"));}
            if(args.Contains("-lunarSigns"))
            {
                int signIndex=0;
                foreach(var sign in ground.CameraReviewArena.GetComponentInChildren<LunarLaboratoryPresentation>().GetComponentsInChildren<TextMesh>())
                {
                    var bounds=sign.GetComponent<MeshRenderer>().bounds;var normal=-sign.transform.forward;
                    var eye=bounds.center+(normal.y>.9f?new Vector3(0,2,sign.text=="H"?1.6f:-1.6f):normal*2.3f);
                    var direction=bounds.center-eye;float lookYaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
                    float lookPitch=-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg;
                    ground.PlaceCombatReviewSeat(0,eye-Vector3.up*ground.Profile.Get("camera.eyeHeight"),lookPitch,lookYaw);
                    yield return Capture("sign-"+(++signIndex).ToString("00"));
                }
            }
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

#endif
