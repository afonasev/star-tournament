using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeAchievementsLayoutTests
    {
        static NativeMatchComposition Composition(bool teams)
        {
            var mode=teams?NativeMatchMode.Teams:NativeMatchMode.Ffa;
            var roster=new NativeMatchRoster(mode,teams?new[]{NativeTeam.TeamA,NativeTeam.TeamA,NativeTeam.TeamB,NativeTeam.TeamB}:new[]{NativeTeam.None,NativeTeam.None,NativeTeam.None,NativeTeam.None});
            var colors=teams?new[]{Color.red,Color.red,Color.blue,Color.blue}:new[]{Color.red,Color.blue,Color.green,Color.yellow};
            var people=Enumerable.Range(0,4).Select(i=>new NativeParticipantInfo(NativeParticipantKind.LocalHuman,"Player "+(i+1),colors[i])).ToArray();
            return new NativeMatchComposition(roster,people,new[]{0,1,2,3});
        }
        static NativeMatchSnapshot Snapshot(int count)
        {
            return new NativeMatchSnapshot{Achievements=Enumerable.Range(0,count).Select(i=>new NativeAchievement
            {Participant=i,Id="award-"+i,Name="Achievement "+i,Fact=(10+i)+" damage",Tier=(NativeAchievementTier)(i%3)}).ToArray()};
        }
        static readonly string[] CatalogNames={
            "Тормозок","Попрыгун","Плохой друг","Глаз-алмаз","Спонсор кладбища","Первый блин","Гуманист","Мишень с ногами","Аптечный магнат","Оружейный сомелье","Стажёр","Больно, но своё","Не дождался помощи","Всё моё",
            "Предупредительный огонь","Кардиотренировка","Почти опасный","Ассистент ассистента","Броня не помогла","Жадность до добра","Сам себе противник","Прыгал до последнего","Шумовой спецназ","Обмен невыгодный",
            "Пацифист года","Враг внутри","Зачем тебе патроны?","Главный свой враг","Командный вредитель","Коллекционер без побед"};
        static readonly string[] CatalogFacts={
            "Пройдено: 123.4 м","Прыжков: 23","Союзникам: 840.5 урона","Точность: 12.5%","Смертей: 23","Смерть №1 в матче","Врагам: 840.5 урона","От врагов: 840.5 урона","Аптечек: 23","Смен оружия: 23","Убийств: 23","Себе: 840.5 урона","Самоустранений: 23","Бонусов: 23",
            "Выстрелов: 80 · убийств: 0","Пройдено: 123.4 м · убийств: 0","Врагам: 840.5 урона · убийств: 0","Ассистов: 23 · убийств: 0","Брони: 23 · смертей: 23","Бонусов: 23 · очков: 123","Себе: 840.5 · врагам: 620.5","Прыжков: 23 · убийств: 0","Выстрелов: 80 · точность: 12.5%","От врагов: 840.5 · Врагам: 620.5 урона",
            "Убийств: 0 · Врагам: 840.5 урона","Союзникам: 840.5 · врагам: 620.5","Выстрелов: 80 · попаданий во врагов: 0","Самоустранений: 23 · убийств: 0","Союзников убито: 23 · убийств: 0","Бонусов: 23 · убийств: 0"};
        static NativeMatchSnapshot CatalogSnapshot(int first,int count)
        {
            return new NativeMatchSnapshot{Achievements=Enumerable.Range(first, count).Select(i=>new NativeAchievement
            {Participant=(i-first)%4,Id="award-"+i,Name=CatalogNames[i],Fact=CatalogFacts[i],Tier=(NativeAchievementTier)(i<14?0:i<24?1:2)}).ToArray()};
        }
        [UnityTest] public IEnumerator CatalogCopyFitsAtResponsiveSizesAndKeepsResultsBoundsAndFrozenIdentity()
        {
            var parent=new GameObject("achievement-layout",typeof(RectTransform),typeof(Canvas));
            var canvas=parent.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            var rect=(RectTransform)parent.transform;rect.sizeDelta=new Vector2(960,540);
            var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");var view=new NativeAchievementsView(parent.transform,font,20);
            try
            {
                var table=new NativeStandingsView(parent.transform,font,20,"results-table",new Vector2(.06f,.42f),new Vector2(.94f,.94f));
                var actionObject=new GameObject("measured-results-actions",typeof(RectTransform));actionObject.transform.SetParent(parent.transform,false);
                var actionRect=(RectTransform)actionObject.transform;actionRect.sizeDelta=new Vector2(480,128);
                foreach(int count in new[]{0,1,4})
                {
                    var snapshot=Snapshot(count);string frozen=JsonUtility.ToJson(snapshot);
                    view.Show(false,snapshot,Composition(false));Assert.That(view.Root.activeSelf,Is.False,"awards are hidden outside Results");
                    view.Show(true,snapshot,Composition(false));Canvas.ForceUpdateCanvases();yield return null;Canvas.ForceUpdateCanvases();
                    view.PositionResultsTable(table);
                    Assert.That(view.Root.activeSelf,Is.EqualTo(count>0));
                    var visible=view.Root.GetComponentsInChildren<RectTransform>(true).Count(r=>r.name.StartsWith("achievement-tile-")&&r.gameObject.activeSelf);
                    Assert.That(visible,Is.EqualTo(count));Assert.That(JsonUtility.ToJson(snapshot),Is.EqualTo(frozen),"view cannot mutate or reselect snapshot awards");
                    if(count==0)continue;
                    var composition=Composition(count==4);
                    view.Show(true,snapshot,composition);Canvas.ForceUpdateCanvases();
                    for(int i=0;i<count;i++)
                    {
                        var tile=view.Root.transform.Find("achievement-tile-"+i);var tileRect=(RectTransform)tile;
                        Assert.That(tileRect.rect.width,Is.GreaterThan(100));Assert.That(tileRect.rect.height,Is.GreaterThan(60));
                        var name=tile.Find("participant").GetComponent<Text>();
                        Assert.That(name.text,Is.EqualTo(composition.Participant(i).Name));Assert.That(name.color,Is.EqualTo(composition.Participant(i).Color));
                        string tierLabel=snapshot.Achievements[i].Tier==NativeAchievementTier.Gold?"ЗОЛОТО":snapshot.Achievements[i].Tier==NativeAchievementTier.Silver?"СЕРЕБРО":"БРОНЗА";
                        Assert.That(tile.Find("title").GetComponent<Text>().text,Is.EqualTo("● "+tierLabel+" · "+snapshot.Achievements[i].Name));
                        Assert.That(tile.Find("fact").GetComponent<Text>().text,Is.EqualTo(snapshot.Achievements[i].Fact));
                        Assert.That(tile.Find("title").GetComponent<Text>().text,Does.Contain(snapshot.Achievements[i].Tier==NativeAchievementTier.Gold?"ЗОЛОТО":snapshot.Achievements[i].Tier==NativeAchievementTier.Silver?"СЕРЕБРО":"БРОНЗА"));
                    }
                }
                foreach(var size in new[]{new Vector2(960,540),new Vector2(1920,1080),new Vector2(3840,2160)})
                {
                    rect.sizeDelta=size;
                    for(int first=0;first<CatalogNames.Length;first+=4)
                    {
                        var snapshot=CatalogSnapshot(first,Mathf.Min(4,CatalogNames.Length-first));string frozen=JsonUtility.ToJson(snapshot);
                        bool teams=first==0;var composition=Composition(teams);
                        view.Show(true,snapshot,composition);Canvas.ForceUpdateCanvases();yield return null;Canvas.ForceUpdateCanvases();
                        Assert.That(JsonUtility.ToJson(snapshot),Is.EqualTo(frozen));
                        var overlay=(RectTransform)view.Root.transform;var tableRect=table.Root.GetComponent<RectTransform>();
                        foreach(float stackHeight in new[]{128f,168f,220f})
                        {
                            actionRect.sizeDelta=new Vector2(480,stackHeight);
                            NativeAchievementsView.PlaceResultsActionsAtBottom(actionRect);view.AdaptToActions(actionRect,table);
                            Canvas.ForceUpdateCanvases();
                            var overlayCorners=new Vector3[4];var tableCorners=new Vector3[4];var actionCorners=new Vector3[4];
                            overlay.GetWorldCorners(overlayCorners);tableRect.GetWorldCorners(tableCorners);actionRect.GetWorldCorners(actionCorners);
                            Assert.That(actionCorners[1].y+2.9f,Is.LessThanOrEqualTo(overlayCorners[0].y+.1f),$"{stackHeight}px action stack clears compact/wide awards");
                            Assert.That(overlayCorners[1].y+2.9f,Is.LessThanOrEqualTo(tableCorners[0].y+.1f),"award band remains below results table");
                            for(int i=0;i<snapshot.Achievements.Length;i++)
                            {
                                var tile=view.Root.transform.Find("achievement-tile-"+i);Assert.That(tile,Is.Not.Null);
                                foreach(string child in new[]{"title","participant","fact"})
                                {
                                    var label=tile.Find(child).GetComponent<Text>();
                                    Assert.That(label.preferredWidth,Is.LessThanOrEqualTo(label.rectTransform.rect.width+1f),$"{CatalogNames[first+i]} {child} width at {size.x}x{size.y}");
                                    Assert.That(label.preferredHeight,Is.LessThanOrEqualTo(label.rectTransform.rect.height+1f),$"{CatalogNames[first+i]} {child} height at {size.x}x{size.y}");
                                }
                            }
                        }
                    }
                }
            }
            finally{Object.DestroyImmediate(parent);}
        }
    }
}
