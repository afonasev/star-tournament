using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeScoreboardLayoutTests
    {
        [UnityTest] public IEnumerator FfaLeaderKeepsCrownWithoutYellowRowOrStripe()
        {
            var root=new GameObject("ffa-scoreboard",typeof(RectTransform),typeof(Canvas));root.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;((RectTransform)root.transform).sizeDelta=new Vector2(640,540);
            var view=new NativeStandingsView(root.transform,Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),20,"table",Vector2.zero,Vector2.one);
            try
            {
                var p=ProvingProfile.CreateMatchDefault();var state=new NativeMatchState(NativeMatchRoster.Ffa(4),NativeMatchConfiguration.Default(p),p,50);
                state.RecordAccuracy(0,WeaponId.Shotgun,8,2);state.RecordAccuracy(0,WeaponId.Rifle,4,3);
                state.BeginTick();state.RecordDamage(1,0,new DamageResult(100,true));state.EndTick();
                view.Show(true,state.Read(),true);yield return null;Canvas.ForceUpdateCanvases();view.Show(true,state.Read(),true);
                var leader=view.Root.transform.Find("row-1");var other=view.Root.transform.Find("row-2");
                Assert.That(leader.GetComponent<Image>().color,Is.EqualTo(other.GetComponent<Image>().color));
                Assert.That(leader.Find("identity-accent").gameObject.activeSelf,Is.False);
                Assert.That(leader.Find("leader").gameObject.activeSelf,Is.True);
                Assert.That(other.Find("leader").gameObject.activeSelf,Is.False);
                Assert.That(leader.Find("cell-6").GetComponent<Text>().text,Is.EqualTo("50%"));
                Assert.That(leader.Find("cell-7").GetComponent<Text>().text,Is.EqualTo("100"));
                Assert.That(view.Root.transform.Find("row-0/header-6").GetComponent<StandingsIcon>().Kind,Is.EqualTo(StandingsIcon.Symbol.Percent));
                Assert.That(view.Root.transform.Find("row-0/header-7").GetComponent<StandingsIcon>().Kind,Is.EqualTo(StandingsIcon.Symbol.Star));
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [UnityTest] public IEnumerator CompleteGroupsShareIconAxisAndAllEightTeamRowsFitWithIndependentLifeAndLeaders()
        {
            var root=new GameObject("scoreboard-test",typeof(RectTransform),typeof(Canvas));root.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;var rt=(RectTransform)root.transform;rt.sizeDelta=new Vector2(960,540);
            var profile=ProvingProfile.CreateDefault();var view=new NativeStandingsView(root.transform,Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),20,"table",Vector2.zero,Vector2.one,profile){TopInset=52};
            try
            {
                var p=ProvingProfile.CreateMatchDefault();var roster=new NativeMatchRoster(NativeMatchMode.Teams,Enumerable.Range(0,8).Select(i=>i<5?NativeTeam.TeamA:NativeTeam.TeamB).ToArray());
                var state=new NativeMatchState(roster,NativeMatchConfiguration.Default(p),p,50);state.BeginTick();state.RecordDamage(5,0,new DamageResult(100,true));state.RecordDamage(1,-1,new DamageResult(100,true),originalSource:0);state.EndTick();
                var lives=new CombatLifeState[8];lives[0].Dead=true;
                view.Show(true,state.Read(),true,lives:lives);Canvas.ForceUpdateCanvases();yield return null;view.Show(true,state.Read(),true,lives:lives);Canvas.ForceUpdateCanvases();
                var table=view.Root.transform;
                Assert.That(table.Find("row-10").gameObject.activeSelf,Is.True);
                var bottom=table.Find("row-10").GetComponent<RectTransform>();Assert.That(bottom.anchoredPosition.y-bottom.rect.height/2,Is.GreaterThanOrEqualTo(-540));
                // Team B leads with zero; A has a negative net total. Player 0 remains dead regardless of score.
                var player=Enumerable.Range(1,10).Select(i=>table.Find("row-"+i)).Single(r=>r.Find("cell-0").GetComponent<Text>().text=="Игрок 1");
                Assert.That(player.Find("life").GetComponent<StandingsIcon>().Kind,Is.EqualTo(StandingsIcon.Symbol.Skull));
                Assert.That(player.Find("cell-7").GetComponent<Text>().text,Does.Contain("−200"));
                for(int c=1;c<8;c++)
                {
                    var icon=table.Find("row-0/header-"+c).GetComponent<RectTransform>();var value=player.Find("cell-"+c).GetComponent<Text>();
                    Vector3 center=value.rectTransform.TransformPoint(value.rectTransform.rect.center);
                    Assert.That(icon.position.x,Is.EqualTo(center.x).Within(.01f));Assert.That(value.preferredWidth,Is.LessThanOrEqualTo(value.rectTransform.rect.width+.1f));Assert.That(value.alignment,Is.EqualTo(TextAnchor.MiddleCenter));Assert.That(value.alignByGeometry,Is.True);
                }
                for(int i=0;i<16000&&state.Phase!=NativeMatchPhase.Finished;i++){state.BeginTick();state.EndTick();}
                view.Show(true,state.Read(),true,lives:lives);
                Assert.That(table.GetComponentsInChildren<StandingsIcon>().Where(i=>i.name=="life"&&i.Kind!=StandingsIcon.Symbol.Shield),Is.Empty,"Results hide operational life markers");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
