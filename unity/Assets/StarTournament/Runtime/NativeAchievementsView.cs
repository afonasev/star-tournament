using System;
using UnityEngine;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    /// <summary>Read-only presentation of awards already frozen into a match snapshot.</summary>
    public sealed class NativeAchievementsView
    {
        public GameObject Root { get; }
        readonly Font font;
        readonly int baseFontSize;
        readonly RectTransform[] tiles=new RectTransform[4];
        readonly Text[] tiers=new Text[4],titles=new Text[4],names=new Text[4],reasons=new Text[4],facts=new Text[4],stamps=new Text[4];
        readonly Image[][] stampFrames=new Image[4][];
        readonly Vector3[] corners=new Vector3[4];
        readonly NativeAchievementMedalGraphic[] medals=new NativeAchievementMedalGraphic[4];
        public bool CompactLayout { get; private set; }
        public bool HasAwards { get; private set; }
        public bool ContentLayout { get; set; }
        static readonly Color Ink=new Color32(231,239,250,255);
        static readonly Color Muted=new Color32(148,171,194,255);
        static readonly Color Bronze=new Color32(205,139,83,255), Silver=new Color32(190,207,224,255), Gold=new Color32(255,206,88,255);

        public NativeAchievementsView(Transform parent,Font font,int fontSize)
        {
            this.font=font??throw new ArgumentNullException(nameof(font));baseFontSize=Mathf.Max(12,fontSize);
            Root=new GameObject("native-achievements",typeof(RectTransform));Root.transform.SetParent(parent,false);
            var root=(RectTransform)Root.transform;
            root.anchorMin=new Vector2(.06f,.25f);root.anchorMax=new Vector2(.94f,.43f);root.offsetMin=root.offsetMax=Vector2.zero;
            for(int i=0;i<4;i++)
            {
                var tile=new GameObject("achievement-tile-"+i,typeof(RectTransform),typeof(NativeAchievementCardGraphic));tile.transform.SetParent(Root.transform,false);
                tiles[i]=(RectTransform)tile.transform;tiles[i].anchorMin=new Vector2(i*.25f,0);tiles[i].anchorMax=new Vector2((i+1)*.25f,1);tiles[i].offsetMin=new Vector2(5,3);tiles[i].offsetMax=new Vector2(-5,-3);
                var panel=tile.GetComponent<NativeAchievementCardGraphic>();panel.color=new Color32(24,37,55,255);panel.raycastTarget=false;
                medals[i]=new GameObject("medal",typeof(RectTransform),typeof(NativeAchievementMedalGraphic)).GetComponent<NativeAchievementMedalGraphic>();
                medals[i].transform.SetParent(tile.transform,false);medals[i].raycastTarget=false;Place(medals[i].rectTransform,new Vector2(.035f,.70f),new Vector2(.27f,.99f));
                tiers[i]=Label(tile.transform,"tier",TextAnchor.MiddleLeft);
                titles[i]=Label(tile.transform,"title",TextAnchor.MiddleLeft);
                names[i]=Label(tile.transform,"participant",TextAnchor.MiddleLeft);
                reasons[i]=Label(tile.transform,"reason",TextAnchor.UpperLeft);
                facts[i]=Label(tile.transform,"fact",TextAnchor.UpperLeft);
                var divider=new GameObject("fact-divider",typeof(RectTransform),typeof(Image));divider.transform.SetParent(tile.transform,false);
                var dividerRect=(RectTransform)divider.transform;Place(dividerRect,new Vector2(.06f,.205f),new Vector2(.94f,.205f));dividerRect.sizeDelta=new Vector2(0,1);
                divider.GetComponent<Image>().color=new Color32(54,74,95,255);divider.GetComponent<Image>().raycastTarget=false;
                var frame=new GameObject("earned-stamp-frame",typeof(RectTransform));frame.transform.SetParent(tile.transform,false);
                var frameRect=(RectTransform)frame.transform;frameRect.localRotation=Quaternion.Euler(0,0,-10);stampFrames[i]=CreateStampFrame(frame.transform);
                stamps[i]=Label(tile.transform,"earned-stamp",TextAnchor.MiddleCenter);stamps[i].text="ЗАСЛУЖЕНО";stamps[i].fontStyle=FontStyle.Bold;stamps[i].color=new Color(1,1,1,.18f);stamps[i].transform.localRotation=Quaternion.Euler(0,0,-10);
                frame.transform.SetAsFirstSibling();stamps[i].transform.SetAsFirstSibling();
                tile.SetActive(false);
            }
            Root.SetActive(false);
        }
        Text Label(Transform parent,string name,TextAnchor anchor)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var label=go.GetComponent<Text>();label.font=font;label.color=Ink;label.alignment=anchor;label.alignByGeometry=true;
            label.raycastTarget=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;
            return label;
        }
        static Image[] CreateStampFrame(Transform parent)
        {
            var edges=new Image[4];
            for(int i=0;i<edges.Length;i++)
            {
                var go=new GameObject("edge-"+i,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
                edges[i]=go.GetComponent<Image>();edges[i].raycastTarget=false;
                var rect=(RectTransform)go.transform;
                if(i<2){Place(rect,new Vector2(0,i==0?1:0),new Vector2(1,i==0?1:0));rect.sizeDelta=new Vector2(0,1);}
                else {Place(rect,new Vector2(i==2?0:1,0),new Vector2(i==2?0:1,1));rect.sizeDelta=new Vector2(1,0);}
            }
            return edges;
        }
        static void Place(RectTransform rect,Vector2 min,Vector2 max){rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;}

        public void Show(bool visible,NativeMatchSnapshot snapshot,NativeMatchComposition composition)
        {
            var awards=visible?snapshot?.Achievements:null;bool shown=awards!=null&&awards.Length>0;HasAwards=shown;
            var root=(RectTransform)Root.transform;var canvas=Root.GetComponentInParent<Canvas>();
            float screenWidth=root.rect.width*(canvas?canvas.scaleFactor:1f);CompactLayout=screenWidth<1200;
            root.anchoredPosition=Vector2.zero;
            Root.SetActive(shown);if(!shown){foreach(var tile in tiles)tile.gameObject.SetActive(false);return;}
            int count=Math.Min(4,awards.Length);
            // One row keeps four complete explanations visible at 960x540. At wider sizes
            // cards gain height while preserving the same reading order and table boundary.
            float panelHeight=CompactLayout?.33f:.23f;
            if(ContentLayout)SetContentRect(root,1690,248,0);
            else {root.anchorMin=new Vector2(.06f,.245f);root.anchorMax=new Vector2(.94f,.245f+panelHeight);root.offsetMin=root.offsetMax=Vector2.zero;}
            for(int i=0;i<4;i++)
            {
                bool active=i<count;tiles[i].gameObject.SetActive(active);if(!active)continue;
                var award=awards[i];var info=composition?.Participant(award.Participant);
                float groupLeft=(4-count)*.125f;
                float left=groupLeft+(float)i/4,right=groupLeft+(float)(i+1)/4;
                tiles[i].anchorMin=new Vector2(left,0);tiles[i].anchorMax=new Vector2(right,1);tiles[i].offsetMin=new Vector2(5,3);tiles[i].offsetMax=new Vector2(-5,-3);
                Color metal=TierColor(award.Tier);var card=tiles[i].GetComponent<NativeAchievementCardGraphic>();card.Accent=metal;card.SetVerticesDirty();medals[i].color=metal;medals[i].Theme=ThemeFor(award.Id);
                tiers[i].text=TierName(award.Tier);tiers[i].color=metal;tiers[i].fontStyle=FontStyle.Normal;
                titles[i].text=award.Name??string.Empty;titles[i].color=Ink;titles[i].fontStyle=FontStyle.Bold;
                names[i].text=info?.Name??("Игрок "+(award.Participant+1));names[i].color=info?.Color??NativeStandingsView.Palette[award.Participant%NativeStandingsView.Palette.Length];names[i].fontStyle=FontStyle.Bold;
                reasons[i].text=ExplanationFor(award.Id,award.RulesVersion==0?1:award.RulesVersion);reasons[i].color=Ink;
                facts[i].text=award.Fact??string.Empty;facts[i].color=Muted;
                Place(tiers[i].rectTransform,new Vector2(.30f,.89f),new Vector2(.96f,.99f));
                Place(titles[i].rectTransform,new Vector2(.30f,.70f),new Vector2(.96f,.89f));
                Place(names[i].rectTransform,new Vector2(.06f,.58f),new Vector2(.94f,.70f));
                Place(reasons[i].rectTransform,new Vector2(.06f,.32f),new Vector2(.94f,.58f));
                Place(facts[i].rectTransform,new Vector2(.06f,.015f),new Vector2(.94f,.18f));
                var stampRect=stamps[i].rectTransform;Place(stampRect,new Vector2(.94f,.275f),new Vector2(.94f,.275f));stampRect.pivot=new Vector2(1,.5f);stampRect.sizeDelta=new Vector2(104,18);stamps[i].color=new Color(metal.r,metal.g,metal.b,.40f);
                var stampFrame=(RectTransform)stamps[i].transform.parent.Find("earned-stamp-frame");Place(stampFrame,stampRect.anchorMin,stampRect.anchorMax);stampFrame.pivot=stampRect.pivot;stampFrame.sizeDelta=stampRect.sizeDelta;
                foreach(var edge in stampFrames[i])edge.color=new Color(metal.r,metal.g,metal.b,.38f);
                int size=Mathf.Max(12,Mathf.RoundToInt(baseFontSize*Mathf.Lerp(.64f,1f,Mathf.Clamp01(root.rect.height/128f))));
                tiers[i].fontSize=Mathf.Max(12,Mathf.RoundToInt(size*.60f));titles[i].fontSize=Mathf.Max(12,Mathf.RoundToInt(size*.88f));names[i].fontSize=Mathf.Max(12,Mathf.RoundToInt(size*.70f));reasons[i].fontSize=Mathf.Max(12,Mathf.RoundToInt(size*.72f));facts[i].fontSize=Mathf.Max(12,Mathf.RoundToInt(size*.60f));stamps[i].fontSize=12;
                Fit(tiers[i]);FitWrapped(titles[i]);Fit(names[i]);FitWrapped(reasons[i]);FitWrapped(facts[i]);Fit(stamps[i]);
                medals[i].SetAllDirty();
            }
        }

        public void CenterResultsContent(RectTransform actions,NativeStandingsView table)
        {
            // Keep the established inner design width and award-card height. Only occupied
            // content contributes to the frame height; gutters belong to the screen.
            const float width=1690, awardHeight=248, gap=16;
            var frame=(RectTransform)Root.transform.parent;
            float cards=HasAwards?awardHeight+gap:0;
            float height=table.ContentHeight+cards+gap+actions.rect.height;
            NativeStandingsView.FitResultsFrame(frame,width,height);
            float bottom=-height*.5f;
            var actionSize=actions.sizeDelta;
            SetContentRect(actions,actionSize.x,actionSize.y,bottom+actionSize.y*.5f);
            float tableBottom=bottom+actionSize.y+gap+cards;
            SetContentRect((RectTransform)table.Root.transform,width,table.ContentHeight,tableBottom+table.ContentHeight*.5f);
            if(HasAwards)SetContentRect((RectTransform)Root.transform,width,awardHeight,bottom+actionSize.y+gap+awardHeight*.5f);
        }
        static void SetContentRect(RectTransform rect,float width,float height,float centerY)
        {
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.sizeDelta=new Vector2(width,height);rect.anchoredPosition=new Vector2(0,centerY);
        }

        public void PositionResultsTable(NativeStandingsView table)
        {
            var rect=(RectTransform)table.Root.transform;
            rect.anchorMin=new Vector2(.06f,HasAwards?.58f:.42f);rect.anchorMax=new Vector2(.94f,.94f);rect.offsetMin=rect.offsetMax=Vector2.zero;
        }
        public void AdaptToActions(RectTransform actions,NativeStandingsView table)
        {
            PositionResultsTable(table);if(!HasAwards)return;
            var parent=(RectTransform)Root.transform.parent;var root=(RectTransform)Root.transform;
            actions.GetWorldCorners(corners);float actionTop=parent.InverseTransformPoint(corners[1]).y;
            root.GetWorldCorners(corners);float awardBottom=parent.InverseTransformPoint(corners[0]).y;
            const float layoutGap=3f;float shift=actionTop+layoutGap-awardBottom;
            root.anchoredPosition+=Vector2.up*shift;
            root.GetWorldCorners(corners);float awardTop=parent.InverseTransformPoint(corners[1]).y;
            var tableRect=(RectTransform)table.Root.transform;tableRect.GetWorldCorners(corners);float tableBottom=parent.InverseTransformPoint(corners[0]).y;
            float requiredTableBottom=awardTop+layoutGap;
            {float h=parent.rect.height;float normalized=(requiredTableBottom-parent.rect.yMin)/Mathf.Max(1,h);tableRect.anchorMin=new Vector2(tableRect.anchorMin.x,normalized);tableRect.offsetMin=Vector2.zero;}
        }
        public static void PlaceResultsActionsAtBottom(RectTransform menuRect,float inset=.018f)
        {var size=menuRect.sizeDelta;float parentHeight=((RectTransform)menuRect.parent).rect.height;float center=inset+(parentHeight>0?size.y/parentHeight*.5f:0);Layout(menuRect,new Vector2(.5f,center),new Vector2(.5f,center));menuRect.sizeDelta=size;}

        public static string ExplanationFor(string id,int rulesVersion=NativeAchievementCatalog.Version)
        {
            if(rulesVersion<=1)
            {
                switch(id)
                {
                    case "own-opponent":return "Урона себе больше, чем врагам; оба значения больше нуля.";
                    case "pacifist":return "Ноль убийств при достаточной стрельбе и уроне врагам.";
                    case "enemy-within":return "Урона союзникам больше, чем врагам; оба значения больше нуля.";
                    case "why-ammo":return "Больше всех выстрелов при достаточной стрельбе, ноль попаданий во врага.";
                    case "worst-own-enemy":return "Самоустранений больше, чем убийств врагов.";
                    case "team-saboteur":return "Убийств союзников больше, чем убийств врагов.";
                    case "collector":return "Больше всех бонусов, ноль убийств и положительный урон врагам.";
                }
            }
            switch(id)
            {
                case "slowpoke":return "Меньше всех расстояния за матч.";
                case "jumper":return "Больше всех принятых прыжков за матч.";
                case "bad-friend":return "Больше всех урона союзникам за матч.";
                case "diamond-eye":return "Самая низкая точность при достаточной стрельбе.";
                case "cemetery-sponsor":return "Больше всех смертей за матч.";
                case "first-pancake":return "Первая смерть в матче.";
                case "humanitarian":return "Меньше всех урона врагам за матч.";
                case "walking-target":return "Больше всех урона, полученного от врагов.";
                case "pharmacy-magnate":return "Больше всех подобранных аптечек за матч.";
                case "weapon-sommelier":return "Больше всех состоявшихся переключений оружия.";
                case "trainee":return "Меньше всех убийств за матч.";
                case "own-pain":return "Больше всех урона самому себе.";
                case "no-help-needed":return "Больше всех самоустранений за матч.";
                case "all-mine":return "Больше всех подобранных бонусов: брони, лечения, скорости или урона.";
                case "warning-fire":return "Больше всех выстрелов и меньше всех убийств.";
                case "cardio":return "Больше всех пройденного расстояния и меньше всех убийств.";
                case "almost-dangerous":return "Больше всех урона врагам среди участников с минимумом убийств.";
                case "assistant-assistant":return "Больше всех ассистов и меньше всех убийств.";
                case "armor-didnt-help":return "Больше всех подборов брони и смертей.";
                case "greed":return "Больше всех подборов бонусов и меньше всех очков.";
                case "own-opponent":return "Больше всех урона себе и меньше всех урона врагам.";
                case "jumped-to-end":return "Больше всех прыжков и меньше всех убийств.";
                case "noise-force":return "Больше всех выстрелов; самая низкая точность при достаточной стрельбе.";
                case "bad-trade":return "Больше всех урона от врагов и меньше всех урона врагам.";
                case "pacifist":return "Больше всех ассистов; меньше всех убийств и урона врагам.";
                case "enemy-within":return "Больше всех урона союзникам и меньше всех урона врагам.";
                case "why-ammo":return "Достаточно стрелял: максимум выстрелов, минимум точности и урона врагам.";
                case "worst-own-enemy":return "Больше всех урона себе и смертей.";
                case "team-saboteur":return "Больше всех убийств союзников и меньше всех убийств врагов.";
                case "collector":return "Больше всех бонусов и расстояния; меньше всех убийств.";
                default:return "Награда сохранена в итогах матча.";
            }
        }
        static string TierName(NativeAchievementTier tier)=>tier==NativeAchievementTier.Gold?"ЗОЛОТО":tier==NativeAchievementTier.Silver?"СЕРЕБРО":"БРОНЗА";
        static Color TierColor(NativeAchievementTier tier)=>tier==NativeAchievementTier.Gold?Gold:tier==NativeAchievementTier.Silver?Silver:Bronze;
        static NativeAchievementMedalGraphic.ThemeKind ThemeFor(string id)
        {
            if(id=="why-ammo")return NativeAchievementMedalGraphic.ThemeKind.Ammo;
            if(id=="jumper"||id=="cardio"||id=="jumped-to-end")return NativeAchievementMedalGraphic.ThemeKind.Movement;
            if(id=="pharmacy-magnate")return NativeAchievementMedalGraphic.ThemeKind.Heal;
            if(id=="all-mine"||id=="greed"||id=="collector"||id=="armor-didnt-help")return NativeAchievementMedalGraphic.ThemeKind.Bonus;
            if(id=="diamond-eye"||id=="noise-force"||id=="why-ammo")return NativeAchievementMedalGraphic.ThemeKind.Aim;
            if(id=="own-pain"||id=="own-opponent"||id=="enemy-within"||id=="bad-friend"||id=="team-saboteur")return NativeAchievementMedalGraphic.ThemeKind.Relations;
            if(id=="slowpoke")return NativeAchievementMedalGraphic.ThemeKind.Distance;
            if(id=="first-pancake"||id=="cemetery-sponsor"||id=="no-help-needed"||id=="walking-target")return NativeAchievementMedalGraphic.ThemeKind.Survival;
            return NativeAchievementMedalGraphic.ThemeKind.Combat;
        }
        static void Fit(Text text){while(text.fontSize>12&&(text.preferredWidth>Mathf.Max(1,text.rectTransform.rect.width)||text.preferredHeight>Mathf.Max(1,text.rectTransform.rect.height)))text.fontSize--;}
        static void FitWrapped(Text text)
        {
            while(text.fontSize>12&&(!AllWrappedCharactersVisible(text)||SplitsAWord(text)))text.fontSize--;
        }
        static bool SplitsAWord(Text text)
        {
            var settings=text.GetGenerationSettings(new Vector2(Mathf.Max(1,text.rectTransform.rect.width),4096));
            foreach(var word in text.text.Split((char[])null,StringSplitOptions.RemoveEmptyEntries))
            {
                FullLayout.Populate(word,settings);
                if(FullLayout.lineCount>1)return true;
            }
            return false;
        }
        static readonly TextGenerator FullLayout=new TextGenerator(), FittedLayout=new TextGenerator();
        static bool AllWrappedCharactersVisible(Text text)
        {
            if(text==null||string.IsNullOrEmpty(text.text))return true;
            float width=Mathf.Max(1,text.rectTransform.rect.width);
            var reference=FullLayout;var referenceSettings=text.GetGenerationSettings(new Vector2(width,4096));reference.Populate(text.text,referenceSettings);
            var fitted=FittedLayout;var fittedSettings=text.GetGenerationSettings(new Vector2(width,Mathf.Max(1,text.rectTransform.rect.height)));fitted.Populate(text.text,fittedSettings);
            return fitted.characterCountVisible==reference.characterCountVisible;
        }
        static void Layout(RectTransform rect,Vector2 min,Vector2 max){rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    }
}
