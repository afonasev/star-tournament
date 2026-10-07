#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    /// <summary>Opt-in controlled result fixtures plus natural match. Not human/device acceptance.</summary>
    public sealed class NativeAchievementReview : MonoBehaviour
    {
        ProvingGround ground; string directory; readonly Gamepad[] pads=new Gamepad[4];
        [Serializable] sealed class Evidence
        {
            public string state,scope="NATIVE_PLAYER_CONTROLLED_AWARD_FIXTURE_NOT_HUMAN_ACCEPTANCE";
            public bool muted,focused;public int width,height;
            public NativeCompositionSnapshot composition;public NativeCombatSessionSnapshot session;
        }
        void Check(bool condition,string reason){if(!condition)throw new InvalidOperationException("ACHIEVEMENT_REVIEW "+reason);}
        IEnumerator Configure(int humans,bool teams=false,bool natural=false)
        {
            Time.timeScale=1;
            var roster=teams?new NativeMatchRoster(NativeMatchMode.Teams,Enumerable.Range(0,8).Select(p=>p%2==0?NativeTeam.TeamA:NativeTeam.TeamB).ToArray()):NativeMatchRoster.Ffa(8);
            string[] names={"AFONASEV","VEGA","NOVA","ATLAS","ECHO","ORION","RIFT","ZENITH"};
            var info=Enumerable.Range(0,8).Select(p=>new NativeParticipantInfo(p<humans?NativeParticipantKind.LocalHuman:NativeParticipantKind.Bot,names[p],NativeStandingsView.Identity(roster.Read(),p,false),p<humans?-1:1)).ToArray();
            var mapping=humans==0?new[]{0}:Enumerable.Range(0,humans).ToArray();
            var composition=new NativeMatchComposition(roster,info,mapping);
            var config=NativeMatchConfiguration.Default(ground.MatchProfile);config.DurationMinutes=1;
            ground.ConfigureBotTacticsReview(config);
            ground.StartBotReview(composition,17029,humans==0?null:pads.Take(humans).Cast<InputDevice>().ToArray());
            yield return new WaitForSecondsRealtime(.2f);
            if(!natural)Time.timeScale=0;
            Check(ground.Running,"match did not start");
        }
        void FinishControlled(int humans,int tier)
        {
            var match=ground.Session.Match;
            match.BeginTick();
            // A bot leader makes a finite result without overtime; fixture damage is explicitly synthetic.
            for(int p=humans;p<8;p++)
            {
                match.RecordMovement(p,100+p,false);
                match.RecordDamage((p+1)%8,p,new DamageResult(100,true));
            }
            match.RecordDamage((humans+1)%8,humans,new DamageResult(100,true));
            if(tier>=1)for(int p=0;p<humans;p++)for(int j=0;j<20+p;j++)match.RecordMovement(p,0,true);
            if(tier>=2)for(int p=0;p<humans;p++)for(int k=0;k<3+p;k++)match.RecordDamage(p,p,new DamageResult(100,true));
            match.EndTick();
            for(int i=0;i<3100 && match.Phase!=NativeMatchPhase.Finished;i++){match.BeginTick();match.EndTick();}
            Check(match.Phase==NativeMatchPhase.Finished,"controlled result overtime");
        }
        IEnumerator ShowFinished()
        {
            Time.timeScale=1;
            for(int frame=0;frame<30&&ground.Running;frame++)yield return null;
            Time.timeScale=0;yield return null;
            Check(!ground.Running,"results phase missing");
        }
        IEnumerator Capture(string label,bool natural=false)
        {
            while(!Application.isFocused)yield return null;
            yield return null;Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();
            Check(AudioListener.volume==0,"audio is not muted");
            if(ground.Session.Match.Phase==NativeMatchPhase.Finished&&ground.Session.Match.Read().Achievements.Length>0)
            {
                var tileRoots=ground.GetComponentsInChildren<RectTransform>().Where(r=>r.name.StartsWith("achievement-tile-",StringComparison.Ordinal)).ToArray();
                var table=ground.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="results-table");
                var status=(Text)typeof(ProvingGround).GetField("status",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
                var action=(RectTransform)status.transform.parent;
                var corners=new Vector3[4];table.GetWorldCorners(corners);float tableBottom=corners[0].y;
                action.GetWorldCorners(corners);float actionsTop=corners[1].y;
                foreach(var tile in tileRoots)
                {
                    tile.GetWorldCorners(corners);
                    Check(corners[1].y<=tableBottom+1,"tile overlaps table");
                    Check(corners[0].y>=actionsTop-1,"tile overlaps actions");
                    foreach(var labelText in tile.GetComponentsInChildren<Text>())
                        Check(labelText.preferredHeight<=labelText.rectTransform.rect.height+1,"clipped text "+labelText.text);
                }
            }
            var evidence=new Evidence{state=label,muted=AudioListener.volume==0,focused=Application.isFocused,width=Screen.width,height=Screen.height,composition=ground.Composition.Read(),session=ground.Session.Capture()};
            if(natural)evidence.scope="NATIVE_PLAYER_NATURAL_BOT_MATCH_NOT_HUMAN_ACCEPTANCE";
            File.WriteAllText(Path.Combine(directory,label+".json"),JsonUtility.ToJson(evidence,true));
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,label+".png"));
            yield return new WaitForSecondsRealtime(.25f);
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;ground=GetComponent<ProvingGround>();
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-achievementEvidence");
            directory=at>=0&&at+1<args.Length?args[at+1]:Path.Combine(Application.persistentDataPath,"achievement-review");Directory.CreateDirectory(directory);AudioListener.volume=0;
            while(!Application.isFocused||!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            for(int p=0;p<4;p++)pads[p]=InputSystem.AddDevice<Gamepad>();
            yield return Configure(1);FinishControlled(1,0);yield return ShowFinished();
            Check(ground.Session.Match.Read().Achievements.Length==1,"bronze recipient count");
            Check(ground.Session.Match.Read().Achievements[0].Tier==NativeAchievementTier.Bronze,"bronze fixture tier");yield return Capture("01-one-human-bronze");
            yield return Configure(1);FinishControlled(1,1);yield return ShowFinished();
            Check(ground.Session.Match.Read().Achievements[0].Tier==NativeAchievementTier.Silver,"silver fixture tier");yield return Capture("02-one-human-silver");
            yield return Configure(4,true);FinishControlled(4,2);yield return ShowFinished();
            Check(ground.Session.Match.Read().Achievements.Length==4,"four human awards");
            Check(ground.Session.Match.Read().Achievements.All(x=>x.Tier==NativeAchievementTier.Gold),"gold precedence");yield return Capture("03-four-humans-teams-gold");
            var saved=JsonUtility.FromJson<NativeCombatSessionSnapshot>(JsonUtility.ToJson(ground.Session.Capture()));
            string before=JsonUtility.ToJson(ground.Session.Match.Read());ground.Session.Restore(saved);ground.SendMessage("RefreshInterface");
            Check(JsonUtility.ToJson(ground.Session.Match.Read())==before,"finished restore changed result");yield return Capture("04-restored-same-awards");
            ground.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Повторить").onClick.Invoke();Time.timeScale=0;
            Check(ground.Session.Match.Read().Achievements.Length==0,"repeat retained awards");
            Check(ground.Session.Match.Read().Standings.All(x=>x.Shots==0&&x.Jumps==0&&x.BonusPickups==0&&x.SelfDamageDealt==0),"repeat retained statistics");yield return Capture("05-repeat-cleared");
            yield return Configure(0);FinishControlled(0,0);yield return ShowFinished();
            Check(ground.Session.Match.Read().Achievements.Length==0,"bots received awards");yield return Capture("06-all-bots-no-tiles");
            yield return Configure(1,natural:true);
            float deadline=UnityEngine.Time.realtimeSinceStartup+180;
            while(ground.Running&&UnityEngine.Time.realtimeSinceStartup<deadline)yield return null;
            Check(ground.Session.Match.Phase==NativeMatchPhase.Finished,"natural match did not finish");
            Check(ground.Session.Match.Read().Standings.Sum(x=>x.Kills)>0,"natural match no kills");
            Check(ground.Session.Match.Read().Standings.Sum(x=>x.Shots)>0,"natural match no shot counters");
            yield return Capture("07-natural-one-human-seven-bots",true);
            Debug.Log("NATIVE_ACHIEVEMENT_REVIEW_COMPLETE "+directory);Application.Quit();
        }
        void OnDestroy(){foreach(var pad in pads)if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);Time.timeScale=1;}
    }
}
#endif
