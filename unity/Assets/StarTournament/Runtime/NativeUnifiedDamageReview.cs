#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace StarTournament.ProvingGround
{
    /// <summary>Explicit native CLI evidence journey; synthetic fixtures are not human/device acceptance.</summary>
    public sealed class NativeUnifiedDamageReview:MonoBehaviour
    {
        ProvingGround ground;string directory;
        Button B(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        InputField Search()=>ground.GetComponentsInChildren<InputField>(true).Single(f=>f.name=="lab-search");
        void Click(string name)=>B(name).onClick.Invoke();
        [Serializable] sealed class Evidence
        { public string classification="AUTOMATED_NATIVE_PLAYER_NOT_HUMAN_ACCEPTANCE",state,labIdentity;public bool muted,focused;public NativeMatchSnapshot match;public CombatLifeState[] lives; }
        IEnumerator Capture(string label)
        {
            yield return new WaitForEndOfFrame();
            File.WriteAllText(Path.Combine(directory,label+".json"),JsonUtility.ToJson(new Evidence{state=label,labIdentity=ground.LabSavedIdentity,muted=AudioListener.volume==0,focused=Application.isFocused,
                match=ground.Session?.Match?.Read(),lives=ground.Session==null?null:Enumerable.Range(0,ground.Session.ParticipantCount).Select(ground.Session.Life).ToArray()},true));
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,label+".png"));yield return new WaitForSecondsRealtime(.15f);
        }
        void Require(bool value,string detail){if(!value)throw new InvalidOperationException("UNIFIED_DAMAGE_REVIEW "+detail);}
        IEnumerator Start()
        {
            ground=GetComponent<ProvingGround>();var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-unifiedDamageReview");if(flag<0||flag+1>=args.Length||!Path.IsPathFullyQualified(args[flag+1]))throw new ArgumentException("Absolute unified damage evidence directory required");directory=args[flag+1];Directory.CreateDirectory(directory);
            while(!Application.isFocused||!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            Require(AudioListener.volume==0,"audio must be muted");
            Click("main-action-2");yield return null;Click("group-rifle");yield return null;Search().text="rifle.damage";yield return null;yield return Capture("01-lab-rifle-damage");
            Require(ground.GetComponentsInChildren<InputField>().Any(f=>f.name=="input-rifle.damage"),"single rifle damage field absent");
            Search().text="";yield return null;Click("group-shotgun");yield return null;Search().text="shot.damage";yield return null;yield return Capture("02-lab-shotgun-damage");
            Require(ground.GetComponentsInChildren<InputField>().Any(f=>f.name=="input-shot.damage"),"single shotgun damage field absent");
            Search().text="";yield return null;Click("group-damage-policy");yield return null;yield return Capture("03-lab-friendly-self");
            Require(ground.GetComponentsInChildren<InputField>().Count(f=>f.name=="input-damage.friendlyMultiplier"||f.name=="input-damage.selfMultiplier")==2,"coefficient inputs absent");Click("lab-back");yield return null;
            foreach(bool teams in new[]{false,true})foreach(int views in new[]{1,3,4})
            {
                ground.StartParticipantReview(NativeParticipantReview.Mixed(views,teams));yield return null;
                var session=ground.Session;var saved=session.Capture();saved.Lives[1].Armor=50;session.Restore(saved);
                int enemy=0; // Participant 1 is Team B; participant 0 is Team A.
                var shield=session.ApplyDamage(1,session.Life(1).Life,20,enemy,session.Life(enemy).Life);Require(shield.ArmorLost==20&&shield.HealthLost==0,"shield-only contract");
                if(teams)session.ApplyDamage(3,session.Life(3).Life,40,1,session.Life(1).Life);
                session.ApplyDamage(0,session.Life(0).Life,20,0,session.Life(0).Life);
                ground.SetParticipantReviewStandings(true);yield return Capture((teams?"teams":"ffa")+"-"+views+"-live");
                if(teams)
                {
                    session.ApplyDamage(2,session.Life(2).Life,500,0,session.Life(0).Life); // Ally lethal; no reward.
                    var source=session.Match.Read().Standings.Single(r=>r.Seat==0);Require(source.Kills==0,"teamkill awarded enemy kill");
                }
                session.ApplyDamage(5,session.Life(5).Life,500,0,session.Life(0).Life); // Enemy kill makes unique leader.
                var idle=new LocalAction[8];float tickSeconds=1/ground.Profile.Get("simulation.fixedTickHz");int ticks=(int)Math.Ceiling(session.Match.RemainingSeconds/tickSeconds)+2;
                for(int tick=0;tick<ticks&&session.Match.Phase!=NativeMatchPhase.Finished;tick++)session.Tick(idle,tickSeconds);
                Require(session.Match.Phase==NativeMatchPhase.Finished,"result did not finish");yield return new WaitForFixedUpdate();yield return null;yield return Capture((teams?"teams":"ffa")+"-"+views+"-final");
                Click("Повторить матч");yield return null;Require(ground.Session.Match.Read().Standings.All(r=>r.DamageDealt==0&&r.AllyDamageDealt==0&&r.DamageReceived==0),"Repeat retained damage");
                Click("В главное меню");yield return null;
            }
            File.WriteAllText(Path.Combine(directory,"complete.txt"),"muted native Lab and FFA/teams live/final 1/3/4 views; actual HP+shield; ally/self split; Repeat; NOT_HUMAN_ACCEPTANCE");
            Debug.Log("NATIVE_UNIFIED_DAMAGE_REVIEW_COMPLETE");Application.Quit();
        }
    }
}
#endif

#endif
