using System;
using System.Linq;
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
        readonly Text[] titles=new Text[4], names=new Text[4], facts=new Text[4];
        readonly Vector3[] corners=new Vector3[4];
        public bool CompactLayout { get; private set; }
        public bool HasAwards { get; private set; }
        static readonly Color Ink=new Color32(231,239,250,255);
        static readonly Color Bronze=new Color32(205,139,83,255), Silver=new Color32(190,207,224,255), Gold=new Color32(255,206,88,255);

        public NativeAchievementsView(Transform parent,Font font,int fontSize)
        {
            this.font=font??throw new ArgumentNullException(nameof(font));baseFontSize=Mathf.Max(12,fontSize);
            Root=new GameObject("native-achievements",typeof(RectTransform));Root.transform.SetParent(parent,false);
            RectTransform root=(RectTransform)Root.transform;
            root.anchorMin=new Vector2(.06f,.25f);root.anchorMax=new Vector2(.94f,.415f);root.offsetMin=root.offsetMax=Vector2.zero;
            for(int i=0;i<4;i++)
            {
                var tile=new GameObject("achievement-tile-"+i,typeof(RectTransform),typeof(Image));tile.transform.SetParent(Root.transform,false);
                tiles[i]=(RectTransform)tile.transform;tiles[i].anchorMin=new Vector2(i*.25f,0);tiles[i].anchorMax=new Vector2((i+1)*.25f,1);tiles[i].offsetMin=new Vector2(3,3);tiles[i].offsetMax=new Vector2(-3,-3);
                tile.GetComponent<Image>().color=new Color32(15,26,41,245);tile.GetComponent<Image>().raycastTarget=false;
                titles[i]=Label(tile.transform,"title",TextAnchor.MiddleCenter);
                names[i]=Label(tile.transform,"participant",TextAnchor.MiddleCenter);
                facts[i]=Label(tile.transform,"fact",TextAnchor.MiddleCenter);
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
        static void Place(RectTransform rect,Vector2 min,Vector2 max)
        {rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        public void Show(bool visible,NativeMatchSnapshot snapshot,NativeMatchComposition composition)
        {
            var awards=visible?snapshot?.Achievements:null;
            bool shown=awards!=null&&awards.Length>0;
            HasAwards=shown;
            var root=(RectTransform)Root.transform;var canvas=Root.GetComponentInParent<Canvas>();
            float screenWidth=root.rect.width*(canvas?canvas.scaleFactor:1f);
            CompactLayout=screenWidth<1200;
            root.anchoredPosition=Vector2.zero;
            Root.SetActive(shown);if(!shown){foreach(var tile in tiles)tile.gameObject.SetActive(false);return;}
            int count=Math.Min(4,awards.Length);float fontScale=Mathf.Clamp01(((RectTransform)Root.transform).rect.height/90f);
            int columns=CompactLayout?2:Mathf.Min(4,Mathf.Max(2,count));
            int rows=Mathf.CeilToInt((float)count/columns);
            root.anchorMin=new Vector2(.06f,CompactLayout?.27f:.25f);root.anchorMax=new Vector2(.94f,CompactLayout?.515f:.415f);root.offsetMin=root.offsetMax=Vector2.zero;
            for(int i=0;i<4;i++)
            {
                bool active=i<count;tiles[i].gameObject.SetActive(active);if(!active)continue;
                var award=awards[i];var info=composition?.Participant(award.Participant);
                int column=i%columns,row=i/columns;
                tiles[i].anchorMin=new Vector2((float)column/columns,1f-(float)(row+1)/rows);
                tiles[i].anchorMax=new Vector2((float)(column+1)/columns,1f-(float)row/rows);
                tiles[i].offsetMin=new Vector2(3,3);tiles[i].offsetMax=new Vector2(-3,-3);
                string tier=TierName(award.Tier);
                titles[i].text="● "+tier+" · "+(award.Name??"");titles[i].color=TierColor(award.Tier);titles[i].fontStyle=FontStyle.Bold;
                names[i].text=info?.Name??("Игрок "+(award.Participant+1));names[i].color=info?.Color??NativeStandingsView.Palette[award.Participant%NativeStandingsView.Palette.Length];
                facts[i].text=award.Fact??"";facts[i].color=Ink;
                Place(titles[i].rectTransform,CompactLayout?new Vector2(.04f,.75f):new Vector2(.04f,.68f),new Vector2(.96f,.98f));
                Place(names[i].rectTransform,CompactLayout?new Vector2(.04f,.50f):new Vector2(.04f,.36f),CompactLayout?new Vector2(.96f,.75f):new Vector2(.96f,.68f));
                Place(facts[i].rectTransform,new Vector2(.04f,.02f),CompactLayout?new Vector2(.96f,.50f):new Vector2(.96f,.36f));
                int size=Mathf.Max(12,Mathf.RoundToInt(baseFontSize*Mathf.Lerp(.68f,1f,fontScale)));
                titles[i].fontSize=size;names[i].fontSize=size;facts[i].fontSize=size;
                Fit(titles[i]);Fit(names[i]);Fit(facts[i]);
            }
        }
        public void PositionResultsTable(NativeStandingsView table)
        {
            var rect=(RectTransform)table.Root.transform;
            rect.anchorMin=new Vector2(.06f,HasAwards&&CompactLayout?.52f:.42f);
            rect.anchorMax=new Vector2(.94f,.94f);rect.offsetMin=rect.offsetMax=Vector2.zero;
        }
        public void AdaptToActions(RectTransform actions,NativeStandingsView table)
        {
            PositionResultsTable(table);
            if(!HasAwards)return;
            var parent=(RectTransform)Root.transform.parent;var root=(RectTransform)Root.transform;
            actions.GetWorldCorners(corners);float actionTop=parent.InverseTransformPoint(corners[1]).y;
            root.GetWorldCorners(corners);float awardBottom=parent.InverseTransformPoint(corners[0]).y;
            const float layoutGap=3f;
            float shift=Mathf.Max(0,actionTop+layoutGap-awardBottom);
            if(shift>0)root.anchoredPosition+=Vector2.up*shift;
            root.GetWorldCorners(corners);float awardTop=parent.InverseTransformPoint(corners[1]).y;
            var tableRect=(RectTransform)table.Root.transform;
            tableRect.GetWorldCorners(corners);float tableBottom=parent.InverseTransformPoint(corners[0]).y;
            float requiredTableBottom=awardTop+layoutGap;
            if(tableBottom<requiredTableBottom)
            {
                float parentHeight=parent.rect.height;
                float normalized=(requiredTableBottom-parent.rect.yMin)/Mathf.Max(1,parentHeight);
                tableRect.anchorMin=new Vector2(tableRect.anchorMin.x,normalized);
                tableRect.offsetMin=Vector2.zero;
            }
        }
        public static void PlaceResultsActionsAtBottom(RectTransform menuRect,float inset=.018f)
        {
            float parentHeight=((RectTransform)menuRect.parent).rect.height;
            float center=inset+(parentHeight>0?menuRect.sizeDelta.y/parentHeight*.5f:0);
            Layout(menuRect,new Vector2(.5f,center),new Vector2(.5f,center));
        }
        static string TierName(NativeAchievementTier tier)=>tier==NativeAchievementTier.Gold?"ЗОЛОТО":tier==NativeAchievementTier.Silver?"СЕРЕБРО":"БРОНЗА";
        static Color TierColor(NativeAchievementTier tier)=>tier==NativeAchievementTier.Gold?Gold:tier==NativeAchievementTier.Silver?Silver:Bronze;
        static void Fit(Text text)
        {
            while(text.fontSize>12&&(text.preferredWidth>Mathf.Max(1,text.rectTransform.rect.width)||text.preferredHeight>Mathf.Max(1,text.rectTransform.rect.height)))text.fontSize--;
        }
        static void Layout(RectTransform rect,Vector2 min,Vector2 max)
        {rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    }
}
