#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    /// <summary>Opt-in muted Player evidence. Scripted cameras are not human acceptance.</summary>
    public sealed class NativeIndustrialTunnelsReview:MonoBehaviour
    {
        ProvingGround ground;string directory;
        void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("TUNNELS_REVIEW: "+why);}
        IEnumerator Capture(string name)
        {
            ground.SendMessage("RefreshInterface");ground.SendMessage("LateUpdate");yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));yield return new WaitForSecondsRealtime(.15f);
        }
        NativeMatchComposition Bots(int count,bool fourViews=false)
        {
            var rows=Enumerable.Range(0,count).Select(i=>new NativeParticipantInfo(NativeParticipantKind.Bot,"Bot "+(i+1),NativeParticipantColors.For(i),(int)NativeBotDifficulty.Hard)).ToArray();
            return new NativeMatchComposition(NativeMatchRoster.Ffa(count),rows,fourViews?Enumerable.Range(0,count).ToArray():new[]{0});
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;ground=GetComponent<ProvingGround>();AudioListener.volume=0;var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-tunnelsEvidence");
            directory=i>=0?args[i+1]:Path.Combine(Application.persistentDataPath,"tunnels-review");Directory.CreateDirectory(directory);
            while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            Screen.SetResolution(1920,1080,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(.5f);
            ground.SendMessage("OpenSetup");ground.SelectAuthoredMap(IndustrialTunnelsCatalog.Id);ground.SendMessage("ShowSetupStep",(object)0);yield return Capture("00-map-selection");
            ground.StartBotReview(Bots(2),20261002);Require(ground.Session.ArenaIdentity==IndustrialTunnelsCatalog.Identity,ground.CombatReviewDiagnostic());
            if(args.Contains("-wayfindingReview"))
            {
                ground.FullHealReviewManualTick=true;
                for(int sector=0;sector<4;sector++)
                {
                    int sx=sector%2==0?-1:1,sz=sector<2?1:-1;
                    ground.PlaceCombatReviewSeat(0,new Vector3(sx*16.5f,-.6f,sz*16),-5,sx<0?270:90);
                    yield return Capture("room-0"+(sector+1));
                    ground.PlaceCombatReviewSeat(0,new Vector3(sx*10,0,sz*19),0,sx<0?270:90);
                    yield return Capture("approach-0"+(sector+1));
                    ground.PlaceCombatReviewSeat(0,new Vector3(sx*21,-.6f,sz*16),-12,sx<0?90:270);
                    yield return Capture("exits-0"+(sector+1));
                }
                ground.PlaceCombatReviewSeat(0,new Vector3(0,-.6f,5),-5,180);yield return Capture("room-05-centre");
                ground.PlaceCombatReviewSeat(0,new Vector3(-4,0,-19),0,90);yield return Capture("south-rocket");
                Require(ground.Session.SpeedPickups.Length==0&&ground.Session.WeaponPickups.Length==3,"pickup contract");
                ground.SendMessage("Menu");ground.StartBotReview(Bots(4,true),20261003);ground.FullHealReviewManualTick=true;
                for(int sector=0;sector<4;sector++){int sx=sector%2==0?-1:1,sz=sector<2?1:-1;ground.PlaceCombatReviewSeat(sector,new Vector3(sx*16.5f,-.6f,sz*16),-5,sx<0?270:90);}
                yield return Capture("four-rooms");
                File.WriteAllText(Path.Combine(directory,"complete.json"),"{\"status\":\"PASS\",\"muted\":true,\"scriptedCameras\":true,\"rooms\":5,\"humanAcceptance\":false}");
                Application.Quit(0);yield break;
            }
            ground.FullHealReviewManualTick=true;ground.PlaceCombatReviewSeat(0,new Vector3(-20,-.6f,19),-18,160);yield return Capture("01-junction");
            ground.PlaceCombatReviewSeat(0,new Vector3(-10,0,19),0,90);yield return Capture("02-tunnel");
            ground.PlaceCombatReviewSeat(0,new Vector3(0,-.6f,3),0,180);yield return Capture("03-central");
            ground.PlaceCombatReviewSeat(0,new Vector3(-4,0,-19),0,90);yield return Capture("04-south-rocket");
            Require(ground.Session.SpeedPickups.Length==0&&ground.Session.WeaponPickups.Length==3,"pickup contract");
            var before=ground.Session;ground.SendMessage("Repeat");Require(before!=ground.Session&&ground.Session.ArenaIdentity==IndustrialTunnelsCatalog.Identity,"Repeat identity");
            ground.SendMessage("Menu");ground.StartBotReview(Bots(4,true),20261002);Require(ground.BotDriver!=null&&ground.Composition.ParticipantCount==4,ground.CombatReviewDiagnostic());ground.FullHealReviewManualTick=true;yield return Capture("05-four-views");
            ground.FullHealReviewManualTick=false;ground.enabled=false;
            int collections=0,kills=0;ground.Session.PickupCollected+=(a,b,c)=>collections++;ground.Session.Died+=_=>kills++;
            var actions=new LocalAction[4];float dt=1/ground.Profile.Get("simulation.fixedTickHz");int ticks=0;
            while(ground.Session.Match.Phase!=NativeMatchPhase.Finished&&ground.Session.Time<620)
            {Array.Clear(actions,0,actions.Length);ground.BotDriver.ProduceActions(actions,dt);ground.Session.Tick(actions,dt);if(++ticks%240==0)yield return null;}
            Require(ground.Session.Match.Phase==NativeMatchPhase.Finished,"natural match timeout");Require(kills>0&&collections>0,"natural match no combat/pickups");
            File.WriteAllText(Path.Combine(directory,"natural-match.json"),JsonUtility.ToJson(ground.Session.Capture(),true));
            File.WriteAllText(Path.Combine(directory,"complete.json"),"{\"status\":\"PASS\",\"map\":\""+IndustrialTunnelsCatalog.Identity+"\",\"muted\":true,\"naturalKills\":"+kills+",\"collections\":"+collections+",\"humanAcceptance\":false}");
            Application.Quit(0);
        }
    }
}
#endif

#endif
