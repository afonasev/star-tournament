using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeRivalryPresentationTests
    {
        const int ParticipantCount=8;
        static readonly Color Win=new Color32(117,221,169,255),Loss=new Color32(255,102,120,255);

        static NativeMatchComposition Composition(bool teams)
        {
            var roster=teams
                ?new NativeMatchRoster(NativeMatchMode.Teams,Enumerable.Range(0,ParticipantCount).Select(i=>i%2==0?NativeTeam.TeamA:NativeTeam.TeamB).ToArray())
                :NativeMatchRoster.Ffa(ParticipantCount);
            string[] names={"Саша","Вега","Нова","Атлас","Эхо","Орион","Рифт","Зенит"};
            var participants=Enumerable.Range(0,ParticipantCount).Select(i=>
            {
                var kind=i<4?NativeParticipantKind.LocalHuman:NativeParticipantKind.Bot;
                var color=teams?NativeStandingsView.TeamColor(roster.TeamOf(i),false):NativeStandingsView.Palette[i];
                return new NativeParticipantInfo(kind,names[i],color,kind==NativeParticipantKind.Bot?(i==7?2:i%3):-1);
            }).ToArray();
            return new NativeMatchComposition(roster,participants,new[]{0,1,2,3});
        }

        static NativeMatchSnapshot Snapshot(NativeMatchComposition composition)
        {
            var roster=composition.Roster.Read();
            var rows=Enumerable.Range(0,ParticipantCount).Select(i=>new NativeStanding
            {
                Seat=i,Kills=7-i,Assists=i%3,Deaths=i+1,Score=1500-i*75,DamageDealt=1400-i*60,
                DamageReceived=800+i*45,EnemyDamageReceived=800+i*45,
                AccumulatedPenalty=i==6?200:0,AllyDamageDealt=composition.Roster.Mode==NativeMatchMode.Teams?i*4:0
            }).ToArray();
            int[] pairs=new int[ParticipantCount*ParticipantCount];
            pairs[0*ParticipantCount+1]=3;pairs[1*ParticipantCount+0]=1;
            pairs[0*ParticipantCount+2]=1;pairs[2*ParticipantCount+0]=4;
            pairs[0*ParticipantCount+3]=2;pairs[3*ParticipantCount+0]=2;
            pairs[7*ParticipantCount+0]=5;pairs[0*ParticipantCount+7]=2;
            return new NativeMatchSnapshot
            {
                Version=2,Phase=NativeMatchPhase.Finished,Winner=0,WinnerTeam=NativeTeam.TeamA,Roster=roster,
                Standings=rows,Teams=new[]{new NativeTeamStanding{Team=NativeTeam.TeamA,Score=1480},new NativeTeamStanding{Team=NativeTeam.TeamB,Score=1125}},
                DirectKillsByPair=pairs,KillChains=new int[ParticipantCount],
                Achievements=new[]{
                    new NativeAchievement{RulesVersion=NativeAchievementCatalog.Version,Participant=0,Id="jumper",Name="Попрыгун",Fact="Прыжков: 78 — максимум в матче",Tier=NativeAchievementTier.Bronze},
                    new NativeAchievement{RulesVersion=NativeAchievementCatalog.Version,Participant=1,Id="warning-fire",Name="Предупредительный огонь",Fact="Выстрелов: 220 · убийств: 0",Tier=NativeAchievementTier.Silver},
                    new NativeAchievement{RulesVersion=NativeAchievementCatalog.Version,Participant=2,Id="why-ammo",Name="Зачем тебе патроны?",Fact="Выстрелов: 220 · Точность: 12.5% · урон: 310",Tier=NativeAchievementTier.Gold},
                    new NativeAchievement{RulesVersion=NativeAchievementCatalog.Version,Participant=3,Id="collector",Name="Коллекционер без побед",Fact="Бонусов: 9 · Убийств: 1 · путь: 123.4 м",Tier=NativeAchievementTier.Gold}
                }
            };
        }

        static GameObject CreateAction(Transform parent,string name,string label)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);
            go.GetComponent<Image>().color=name.StartsWith("repeat")?new Color32(214,231,242,255):new Color32(27,43,59,255);
            var rect=(RectTransform)go.transform;rect.sizeDelta=new Vector2(240,44);
            var text=new GameObject("label",typeof(RectTransform),typeof(Text));text.transform.SetParent(go.transform,false);
            var t=text.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=label;t.fontSize=16;t.color=name.StartsWith("repeat")?new Color32(18,35,54,255):Color.white;t.alignment=TextAnchor.MiddleCenter;
            var tr=(RectTransform)text.transform;tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=tr.offsetMax=Vector2.zero;
            return go;
        }

        static Transform PlayerRow(NativeStandingsView table,string name)
            =>Enumerable.Range(1,10).Select(i=>table.Root.transform.Find("row-"+i)).First(r=>r!=null&&r.Find("cell-0").GetComponent<Text>().text==name);

        [UnityTest]
        public IEnumerator RivalryResultsRenderAndSelectWithoutChangingFrozenData()
        {
            foreach(bool teams in new[]{false,true})
                foreach(var size in new[]{new Vector2(1280,720),new Vector2(1920,1080)})
                {
                    var host=new GameObject("rivalry-presentation-test",typeof(RectTransform),typeof(Canvas));
                    var canvas=host.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
                    var parent=(RectTransform)host.transform;parent.sizeDelta=size;
                    var eventSystemObject=new GameObject("rivalry-event-system",typeof(EventSystem));
                    var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");var composition=Composition(teams);var snapshot=Snapshot(composition);
                    string frozenSnapshot=JsonUtility.ToJson(snapshot),frozenComposition=JsonUtility.ToJson(composition.Read());
                    var table=new NativeStandingsView(host.transform,font,20,"results-table",new Vector2(.06f,.535f),new Vector2(.94f,.94f))
                    {AllowSelection=true,PerspectiveParticipant=0};
                    var awards=new NativeAchievementsView(host.transform,font,20);
                    var actionRoot=new GameObject("results-actions",typeof(RectTransform));actionRoot.transform.SetParent(host.transform,false);
                    var actionRect=(RectTransform)actionRoot.transform;actionRect.sizeDelta=new Vector2(520,48);NativeAchievementsView.PlaceResultsActionsAtBottom(actionRect);
                    var repeat=CreateAction(actionRoot.transform,"repeat-action","Повторить матч").GetComponent<Button>();
                    var menu=CreateAction(actionRoot.transform,"menu-action","В главное меню").GetComponent<Button>();
                    ((RectTransform)repeat.transform).anchoredPosition=new Vector2(-130,0);((RectTransform)menu.transform).anchoredPosition=new Vector2(130,0);
                    int repeatCalls=0,menuCalls=0;repeat.onClick.AddListener(()=>repeatCalls++);menu.onClick.AddListener(()=>menuCalls++);
                    try
                    {
                        table.Show(true,snapshot,false,composition);awards.Show(true,snapshot,composition);awards.PositionResultsTable(table);
                        table.ConfigureResultNavigation(repeat,menu);
                        awards.AdaptToActions(actionRect,table);
                        Canvas.ForceUpdateCanvases();yield return null;Canvas.ForceUpdateCanvases();
                        AssertVerticalGap(table.Root.GetComponent<RectTransform>(),awards.Root.GetComponent<RectTransform>(),"table and four-card band");
                        AssertVerticalGap(awards.Root.GetComponent<RectTransform>(),actionRect,"four-card band and both result actions");
                        AssertVerticalGap(awards.Root.GetComponent<RectTransform>(),repeat.GetComponent<RectTransform>(),"actual repeat button clears awards");
                        AssertVerticalGap(awards.Root.GetComponent<RectTransform>(),menu.GetComponent<RectTransform>(),"actual menu button clears awards");
                        AssertAwardLayout(awards);

                        Assert.That(table.SelectedParticipant,Is.EqualTo(0));
                        if(teams)Assert.That(table.Root.transform.Find("row-1").GetComponent<Button>().enabled,Is.False,"team totals are not selectable participants");
                        Assert.That(NativeStandingsView.Pair(snapshot,0,1).text,Is.EqualTo("3 : 1"));
                        Assert.That(NativeStandingsView.Pair(snapshot,0,1).color,Is.EqualTo(Win));
                        Assert.That(NativeStandingsView.Pair(snapshot,1,0).text,Is.EqualTo("1 : 3"));
                        Assert.That(NativeStandingsView.Pair(snapshot,1,0).color,Is.EqualTo(Loss));
                        if(teams)Assert.That(NativeStandingsView.Pair(snapshot,0,2).color,Is.EqualTo((Color)new Color32(146,165,189,255)));
                        else Assert.That(NativeStandingsView.Pair(snapshot,0,2).color,Is.EqualTo(Loss));
                        Assert.That(NativeStandingsView.Pair(snapshot,0,3).text,Is.EqualTo("2 : 2"));
                        Assert.That(NativeStandingsView.Pair(snapshot,0,3).color,Is.EqualTo(Color.white));
                        Assert.That(NativeStandingsView.Pair(snapshot,0,0).text,Is.EqualTo("—"));
                        if(teams)Assert.That(NativeStandingsView.Pair(snapshot,0,2).text,Is.EqualTo("—"),"allies do not get a personal score");

                        AssertRenderedPair(table,"Вега","3 : 1",Win);
                        AssertRenderedPair(table,"Нова",teams?"—":"1 : 4",teams?(Color)new Color32(146,165,189,255):Loss);
                        AssertRenderedPair(table,"Атлас","2 : 2",Color.white);
                        Assert.That(table.SelectParticipant(1),Is.True);
                        AssertRenderedPair(table,"Саша","1 : 3",Loss);
                        Assert.That(table.SelectParticipant(0),Is.True);
                        var bot=PlayerRow(table,"Зенит");
                        Assert.That(bot.Find("kind").GetComponent<StandingsIcon>().Kind,Is.EqualTo(StandingsIcon.Symbol.Bot));
                        Assert.That(bot.Find("detail").GetComponent<Text>().text,Is.EqualTo("Ветеран"));
                        string botLabels=string.Join(" ",bot.GetComponentsInChildren<Text>().Select(t=>t.text));
                        Assert.That(botLabels,Does.Not.Contain("ИГРОК").And.Not.Contain("БОТ"));
                        var human=PlayerRow(table,"Саша");
                        Assert.That(human.Find("kind").gameObject.activeSelf,Is.False);
                        Assert.That(human.Find("detail").gameObject.activeSelf,Is.False);
                        AssertNumericHeaderAxes(table,human);
                        Assert.That(repeat.navigation.selectOnUp,Is.SameAs(PlayerRow(table,"Зенит").GetComponent<Button>()));
                        Assert.That(menu.navigation.selectOnUp,Is.SameAs(PlayerRow(table,"Зенит").GetComponent<Button>()));

                        var rowButton=bot.GetComponent<Button>();Assert.That(rowButton,Is.Not.Null);Assert.That(rowButton.enabled,Is.True);
                        var eventData=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
                        Assert.That(ExecuteEvents.Execute(rowButton.gameObject,eventData,ExecuteEvents.pointerClickHandler),Is.True,"bot row handles a real UI click event");
                        Assert.That(table.SelectedParticipant,Is.EqualTo(7));
                        var selectData=new BaseEventData(EventSystem.current);
                        Assert.That(ExecuteEvents.Execute(PlayerRow(table,"Эхо").gameObject,selectData,ExecuteEvents.selectHandler),Is.True);
                        Assert.That(table.SelectedParticipant,Is.EqualTo(4));
                        Assert.That(PlayerRow(table,"Эхо").Find("duel").GetComponent<Text>().text,Is.EqualTo("—"));
                        Assert.That(PlayerRow(table,"Зенит").Find("duel").GetComponent<Text>().text,Is.EqualTo("0 : 0"));
                        Assert.That(JsonUtility.ToJson(snapshot),Is.EqualTo(frozenSnapshot),"presentation must not mutate the frozen match snapshot");
                        Assert.That(JsonUtility.ToJson(composition.Read()),Is.EqualTo(frozenComposition),"selection must not alter participant identity or teams");
                        AssertAwardRecipients(awards,composition);
                        Assert.That(repeatCalls,Is.Zero);Assert.That(menuCalls,Is.Zero,"row selection does not activate result actions");

                        if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)
                        {
                            table.SelectParticipant(0); // Capture a perspective containing win, loss and tied scores.
                            yield return Capture(host,canvas,size,teams);
                            if(!teams&&size.x==1280)
                            {
                                for(int count=1;count<=3;count++)
                                {
                                    var fewer=JsonUtility.FromJson<NativeMatchSnapshot>(frozenSnapshot);fewer.Achievements=fewer.Achievements.Take(count).ToArray();
                                    awards.Show(true,fewer,composition);awards.AdaptToActions(actionRect,table);table.Show(true,snapshot,false,composition);
                                    Canvas.ForceUpdateCanvases();yield return null;
                                    yield return Capture(host,canvas,size,teams,"awards-"+count+"-");
                                }
                            }
                            table.Root.SetActive(false);awards.Root.SetActive(false);actionRoot.SetActive(false);
                            var liveSnapshot=JsonUtility.FromJson<NativeMatchSnapshot>(frozenSnapshot);liveSnapshot.Phase=NativeMatchPhase.Running;
                            var held=new NativeStandingsView(host.transform,font,20,"held-table",new Vector2(.10f,.10f),new Vector2(.90f,.90f))
                            {PerspectiveParticipant=0,FitToContent=true};
                            held.Show(true,liveSnapshot,false,composition,new CombatLifeState[ParticipantCount]);
                            Canvas.ForceUpdateCanvases();yield return null;
                            yield return Capture(host,canvas,size,teams,"tab-");
                        }
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(eventSystemObject);UnityEngine.Object.DestroyImmediate(host);
                    }
                }
        }

        static void AssertRenderedPair(NativeStandingsView table,string name,string expected,Color color)
        {
            var duel=PlayerRow(table,name).Find("duel").GetComponent<Text>();Assert.That(duel.text,Is.EqualTo(expected),name);Assert.That(duel.color,Is.EqualTo(color),name);
        }
        static void AssertAwardRecipients(NativeAchievementsView awards,NativeMatchComposition composition)
        {
            for(int i=0;i<4;i++)
            {
                var recipient=awards.Root.transform.Find("achievement-tile-"+i+"/participant").GetComponent<Text>();
                Assert.That(recipient.text,Is.EqualTo(composition.Participant(i).Name),"results perspective does not move or replace award recipients");
                Assert.That(recipient.color,Is.EqualTo(composition.Participant(i).Color));
            }
        }
        static void AssertAwardLayout(NativeAchievementsView awards)
        {
            var tiles=awards.Root.GetComponentsInChildren<RectTransform>(true).Where(r=>r.name.StartsWith("achievement-tile-")&&r.gameObject.activeSelf).ToArray();
            Assert.That(tiles,Has.Length.EqualTo(4));
            foreach(var tile in tiles)
                foreach(var label in tile.GetComponentsInChildren<Text>())
                {
                    if(label.name=="reason"||label.name=="title"||label.name=="fact")AssertFullyVisibleAfterWrapping(label,tile.name+" "+label.name);
                    else
                    {
                        Assert.That(label.preferredWidth,Is.LessThanOrEqualTo(label.rectTransform.rect.width+1),tile.name+" / "+label.name+" width");
                        Assert.That(label.preferredHeight,Is.LessThanOrEqualTo(label.rectTransform.rect.height+1),tile.name+" / "+label.name+" height");
                    }
                }
            Assert.That(awards.Root.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("earned-stamp-frame")),Is.EqualTo(4));
        }
        static void AssertFullyVisibleAfterWrapping(Text text,string message)
        {
            float width=Mathf.Max(1,text.rectTransform.rect.width);
            var actual=new TextGenerator();var actualSettings=text.GetGenerationSettings(new Vector2(width,Mathf.Max(1,text.rectTransform.rect.height)));
            Assert.That(actual.Populate(text.text,actualSettings),Is.True,message+" generated");
            var fullHeightReference=new TextGenerator();var referenceSettings=text.GetGenerationSettings(new Vector2(width,4096));
            Assert.That(fullHeightReference.Populate(text.text,referenceSettings),Is.True,message+" full-height reference generated");
            Assert.That(actual.characterCountVisible,Is.EqualTo(fullHeightReference.characterCountVisible),message+" all wrapped glyphs remain visible");
            Assert.That(fullHeightReference.lineCount,Is.GreaterThan(0),message+" has measured wrapped lines");
        }
        static void AssertNumericHeaderAxes(NativeStandingsView table,Transform player)
        {
            for(int c=1;c<8;c++)
            {
                var header=table.Root.transform.Find("row-0/header-"+c).GetComponent<RectTransform>();var cell=player.Find("cell-"+c).GetComponent<Text>();
                Vector3 center=cell.rectTransform.TransformPoint(cell.rectTransform.rect.center);
                Assert.That(header.position.x,Is.EqualTo(center.x).Within(.01f),"metric column "+c+" preserves its numeric header axis");
                Assert.That(cell.alignment,Is.EqualTo(TextAnchor.MiddleCenter));
            }
        }

        static void AssertVerticalGap(RectTransform upper,RectTransform lower,string message)
        {
            var a=new Vector3[4];var b=new Vector3[4];upper.GetWorldCorners(a);lower.GetWorldCorners(b);
            Assert.That(a[0].y,Is.GreaterThanOrEqualTo(b[1].y-1),message);
        }

        static IEnumerator Capture(GameObject host,Canvas canvas,Vector2 size,bool teams,string prefix="")
        {
            var cameraObject=new GameObject("rivalry-render-camera");cameraObject.transform.SetParent(host.transform,false);
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=size.y*.5f;camera.aspect=size.x/size.y;camera.nearClipPlane=.1f;camera.farClipPlane=10;camera.transform.position=new Vector3(0,0,-5);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(8,15,27,255);
            canvas.worldCamera=camera;canvas.sortingOrder=2;
            var target=new RenderTexture((int)size.x,(int)size.y,24);target.Create();camera.targetTexture=target;
            string directory=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_UI_EVIDENCE");
            if(string.IsNullOrWhiteSpace(directory))directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.local/rivalry-presentation"));
            Directory.CreateDirectory(directory);
            string label=prefix+(teams?"teams":"ffa")+"-"+(int)size.x+"x"+(int)size.y+".png";
            var prior=RenderTexture.active;var pixels=new Texture2D((int)size.x,(int)size.y,TextureFormat.RGB24,false);
            try
            {
                Canvas.ForceUpdateCanvases();
                // A new off-screen camera needs its own cull/rebuild pass before a render request.
                foreach(var graphic in host.GetComponentsInChildren<Graphic>())graphic.Rebuild(CanvasUpdate.PreRender);
                var diagnostic=new System.Text.StringBuilder();
                foreach(var graphic in host.GetComponentsInChildren<Graphic>())
                {
                    var mesh=graphic.canvasRenderer.GetMesh();
                    diagnostic.AppendLine(graphic.name+" "+graphic.GetType().Name+" rect="+graphic.rectTransform.rect+" culled="+graphic.canvasRenderer.cull+" verts="+(mesh?mesh.vertexCount:0)+" color="+graphic.color);
                }
                File.WriteAllText(Path.Combine(directory,label+".layout.txt"),diagnostic.ToString());
                if(GraphicsSettings.currentRenderPipeline!=null)
                {
                    var request=new RenderPipeline.StandardRequest{destination=target};Assert.That(RenderPipeline.SupportsRenderRequest(camera,request),Is.True);
                    RenderPipeline.SubmitRenderRequest(camera,request);
                }
                else camera.Render();
                RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,size.x,size.y),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(directory,label),pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=prior;camera.targetTexture=null;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(cameraObject);
            }
            yield return null;
        }
    }
}
