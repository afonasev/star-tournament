using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    /// <summary>Read-only tournament grid shared by held, persistent and finished projections.</summary>
    public sealed class NativeStandingsView
    {
        public GameObject Root { get; }
        public float TopInset { get; set; }
        readonly Text[,] cells=new Text[11,8];
        readonly Image[] backgrounds=new Image[10], accents=new Image[10];
        readonly StandingsIcon[] lifeIcons=new StandingsIcon[10], crowns=new StandingsIcon[10], kindIcons=new StandingsIcon[10];
        readonly Text[] ranks=new Text[10], details=new Text[10];
        readonly Text title;
        readonly ProvingProfile tuning;
        // Column ratios are structural grid geometry. All number groups share the header axis.
        static readonly float[] Columns={0,.29f,.38f,.44f,.50f,.64f,.76f,.84f,1};
        static readonly Color Ink=new Color32(231,239,250,255), Muted=new Color32(146,165,189,255), Gold=new Color32(255,206,88,255);
        static readonly Color Base=new Color32(15,26,41,255), Pulse=new Color32(107,239,177,255);
        public static Color[] Palette=>NativeParticipantColors.Read();
        public bool SwapTeamColors {get;set;}
        public static string TeamName(NativeTeam team)=>team==NativeTeam.TeamA?"Team A":team==NativeTeam.TeamB?"Team B":"";
        public static Color TeamColor(NativeTeam team,bool swap)=>Palette[(team==NativeTeam.TeamA?0:1)^(swap?1:0)];
        public static Color Identity(NativeMatchSnapshot state,int participant,bool swap)=>Identity(state?.Roster,participant,swap);
        public static Color Identity(NativeRosterSnapshot roster,int participant,bool swap)=>roster?.Mode==NativeMatchMode.Teams?TeamColor(roster.Teams[participant],swap):Palette[participant];
        public NativeStandingsView(Transform parent,Font font,int size,string name,Vector2 min,Vector2 max,ProvingProfile profile=null)
        {
            tuning=profile??ProvingProfile.CreateDefault();
            Root=new GameObject(name,typeof(RectTransform),typeof(Image));Root.transform.SetParent(parent,false);
            Rect((RectTransform)Root.transform,min,max);Root.GetComponent<Image>().color=Base;Root.GetComponent<Image>().raycastTarget=false;
            title=Text(Root.transform,font,"title",size,TextAnchor.MiddleLeft);
            var symbols=new[]{StandingsIcon.Symbol.Crosshair,StandingsIcon.Symbol.Handshake,StandingsIcon.Symbol.Skull,StandingsIcon.Symbol.Outgoing,StandingsIcon.Symbol.Incoming,StandingsIcon.Symbol.Percent,StandingsIcon.Symbol.Star};
            // Header + maximum eight participant and two team rows; capacity is a product invariant.
            for(int r=0;r<11;r++)
            {
                var row=new GameObject("row-"+r,typeof(RectTransform),typeof(Image));row.transform.SetParent(Root.transform,false);
                var image=row.GetComponent<Image>();image.raycastTarget=false;image.color=r==0?Color.clear:Base;
                for(int c=0;c<8;c++)
                {
                    var cell=Text(row.transform,font,"cell-"+c,size,c==0?TextAnchor.MiddleLeft:TextAnchor.MiddleCenter);
                    Rect(cell.rectTransform,new Vector2(Columns[c],0),new Vector2(Columns[c+1],1));cells[r,c]=cell;
                    if(r==0 && c>0){cell.text="";var icon=Icon(row.transform,"header-"+c,symbols[c-1]);Place(icon.rectTransform,(Columns[c]+Columns[c+1])/2,0,1);icon.color=Muted;}
                }
                if(r==0){cells[0,0].text="УЧАСТНИК";cells[0,0].color=Muted;continue;}
                int i=r-1;backgrounds[i]=image;
                accents[i]=new GameObject("identity-accent",typeof(RectTransform),typeof(Image)).GetComponent<Image>();accents[i].transform.SetParent(row.transform,false);accents[i].raycastTarget=false;
                ranks[i]=Text(row.transform,font,"rank",size,TextAnchor.MiddleLeft);
                lifeIcons[i]=Icon(row.transform,"life",StandingsIcon.Symbol.Pulse);
                crowns[i]=Icon(row.transform,"leader",StandingsIcon.Symbol.Crown);crowns[i].color=Gold;
                kindIcons[i]=Icon(row.transform,"kind",StandingsIcon.Symbol.Human);
                details[i]=Text(row.transform,font,"detail",size,TextAnchor.MiddleLeft);
                var line=new GameObject("separator",typeof(RectTransform),typeof(Image));line.transform.SetParent(row.transform,false);
                Rect((RectTransform)line.transform,Vector2.zero,new Vector2(1,0));((RectTransform)line.transform).sizeDelta=new Vector2(0,1);line.GetComponent<Image>().color=new Color32(37,55,75,255);line.GetComponent<Image>().raycastTarget=false;
            }
        }
        static StandingsIcon Icon(Transform parent,string name,StandingsIcon.Symbol kind)
        {var go=new GameObject(name,typeof(RectTransform),typeof(StandingsIcon));go.transform.SetParent(parent,false);var i=go.GetComponent<StandingsIcon>();i.Kind=kind;i.raycastTarget=false;return i;}
        static Text Text(Transform parent,Font font,string name,int size,TextAnchor alignment)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var t=go.GetComponent<Text>();t.font=font;t.fontSize=size;t.color=Ink;t.alignment=alignment;t.alignByGeometry=true;t.raycastTarget=false;t.supportRichText=true;t.horizontalOverflow=HorizontalWrapMode.Overflow;return t;}
        void Place(RectTransform r,float x,float y,float scale)
        {r.anchorMin=r.anchorMax=new Vector2(x,y==0?.5f:y);r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=Vector2.zero;r.sizeDelta=Vector2.one*tuning.Get("ui.standingsIconSize")*scale;}
        static string Suffix(string value,string suffix)=>suffix==null?value:value+" <color=#FF4352>("+suffix+")</color>";
        public static string[] Values(NativeStanding row,bool teams)=>new[]{"",Suffix(row.Kills.ToString(),row.SelfKills+row.AllyKills==0?null:(row.SelfKills+row.AllyKills).ToString()),row.Assists.ToString(),row.Deaths.ToString(),
            Suffix(Math.Round(row.DamageDealt,MidpointRounding.AwayFromZero).ToString(),!teams||row.AllyDamageDealt==0?null:Math.Round(row.AllyDamageDealt,MidpointRounding.AwayFromZero).ToString()),
            Math.Round(row.DamageReceived,MidpointRounding.AwayFromZero).ToString(),Math.Round(row.AccuracyPercent,MidpointRounding.AwayFromZero).ToString()+"%",Suffix(row.Score.ToString(),row.AccumulatedPenalty==0?null:"−"+row.AccumulatedPenalty)};
        public void Show(bool visible,NativeMatchSnapshot state,bool diagnostic,NativeMatchComposition composition=null,CombatLifeState[] lives=null)
        {
            Root.SetActive(visible);if(!visible||state==null)return;
            bool teams=state.Roster?.Mode==NativeMatchMode.Teams,finished=state.Phase==NativeMatchPhase.Finished;
            var entries=new List<(NativeStanding? player,NativeTeam team,int total)>();
            if(teams)foreach(var team in state.Teams){entries.Add((null,team.Team,team.Score));foreach(var row in state.Standings.Where(r=>state.Roster.Teams[r.Seat]==team.Team))entries.Add((row,team.Team,0));}
            else foreach(var row in state.Standings)entries.Add((row,NativeTeam.None,0));
            var rect=(RectTransform)Root.transform;float padding=tuning.Get("ui.standingsPadding"),height=Mathf.Max(1,rect.rect.height-TopInset-2*padding);
            int count=Math.Max(5,entries.Count+2);float rowHeight=height/count;
            Rect(title.rectTransform,new Vector2(0,1),Vector2.one);title.rectTransform.offsetMin=new Vector2(padding,-TopInset-padding-rowHeight);title.rectTransform.offsetMax=new Vector2(-padding,-TopInset-padding);
            title.text=(teams?"Командный матч":"FFA")+(finished?" · РЕЗУЛЬТАТЫ":"");title.fontSize=Mathf.RoundToInt(Mathf.Min(tuning.Get("ui.standingsFontSize"),rowHeight*.7f));title.fontStyle=FontStyle.Bold;
            for(int r=0;r<11;r++)
            {
                var row=(RectTransform)cells[r,0].transform.parent;Rect(row,new Vector2(0,1),Vector2.one);
                row.offsetMin=new Vector2(padding,-TopInset-padding-(r+2)*rowHeight);row.offsetMax=new Vector2(-padding,-TopInset-padding-(r+1)*rowHeight);
                if(r==0){foreach(var icon in row.GetComponentsInChildren<StandingsIcon>())Place(icon.rectTransform,(Columns[int.Parse(icon.name.Substring(7))]+Columns[int.Parse(icon.name.Substring(7))+1])/2,0,1);cells[0,0].fontSize=Mathf.RoundToInt(title.fontSize*.65f);continue;}
                int i=r-1;row.gameObject.SetActive(i<entries.Count);if(i>=entries.Count)continue;
                for(int c=0;c<8;c++){cells[r,c].text="";cells[r,c].color=Ink;cells[r,c].fontSize=title.fontSize;cells[r,c].fontStyle=c==7?FontStyle.Bold:FontStyle.Normal;}
                var entry=entries[i];bool teamHeader=!entry.player.HasValue;
                bool leader=teamHeader?(finished?entry.team==state.WinnerTeam:entry.total==state.Teams.Max(t=>t.Score)):!teams&&(finished?entry.player.Value.Seat==state.Winner:entry.player.Value.Score==state.Standings.Max(s=>s.Score));
                Color identity=teamHeader?(composition==null?TeamColor(entry.team,SwapTeamColors):composition.Participant(Enumerable.Range(0,composition.ParticipantCount).First(p=>composition.Roster.TeamOf(p)==entry.team)).Color):composition?.Participant(entry.player.Value.Seat).Color??Identity(state,entry.player.Value.Seat,SwapTeamColors);
                backgrounds[i].color=Color.Lerp(Base,identity,teamHeader?tuning.Get("ui.standingsLeaderAccent"):0);
                Rect(accents[i].rectTransform,Vector2.zero,new Vector2(0,1));accents[i].rectTransform.sizeDelta=new Vector2(3,0);accents[i].color=identity;accents[i].gameObject.SetActive(teamHeader);
                Place(crowns[i].rectTransform,Columns[1]-.025f,0,.85f);crowns[i].gameObject.SetActive(leader);
                lifeIcons[i].gameObject.SetActive(!teamHeader&&!finished);kindIcons[i].gameObject.SetActive(!teamHeader);ranks[i].gameObject.SetActive(!teamHeader);details[i].gameObject.SetActive(!teamHeader);
                Rect(cells[r,0].rectTransform,new Vector2(.085f,.3f),new Vector2(Columns[1]-.055f,1));cells[r,0].color=identity;cells[r,0].fontStyle=FontStyle.Bold;
                if(teamHeader)
                {cells[r,0].text=TeamName(entry.team);Rect(cells[r,0].rectTransform,new Vector2(.06f,0),new Vector2(Columns[1]-.05f,1));cells[r,7].text=entry.total.ToString();lifeIcons[i].gameObject.SetActive(true);lifeIcons[i].Kind=StandingsIcon.Symbol.Shield;lifeIcons[i].color=identity;Place(lifeIcons[i].rectTransform,.025f,0,1);}
                else
                {
                    var s=entry.player.Value;var info=composition?.Participant(s.Seat);bool dead=!finished && lives!=null && lives[s.Seat].Dead;
                    var values=Values(s,teams);for(int c=1;c<8;c++)cells[r,c].text=values[c];
                    cells[r,0].text=(info?.Name??"Игрок "+(s.Seat+1)).Replace('<','‹').Replace('>','›');
                    lifeIcons[i].Kind=dead?StandingsIcon.Symbol.Skull:StandingsIcon.Symbol.Pulse;lifeIcons[i].color=dead?Muted:Pulse;Place(lifeIcons[i].rectTransform,.06f,0,1);
                    Rect(ranks[i].rectTransform,Vector2.zero,new Vector2(.035f,1));ranks[i].text=(teams?state.Standings.Where(p=>state.Roster.Teams[p.Seat]==entry.team).ToList().FindIndex(p=>p.Seat==s.Seat)+1:i+1).ToString("00");ranks[i].color=Muted;ranks[i].fontSize=Mathf.RoundToInt(title.fontSize*.65f);
                    var kind=info?.Kind??(diagnostic?NativeParticipantKind.DiagnosticFixture:NativeParticipantKind.LocalHuman);
                    kindIcons[i].Kind=kind==NativeParticipantKind.LocalHuman?StandingsIcon.Symbol.Human:kind==NativeParticipantKind.Bot?StandingsIcon.Symbol.Bot:StandingsIcon.Symbol.Fixture;kindIcons[i].color=Muted;Place(kindIcons[i].rectTransform,.095f,.2f,.55f);
                    Rect(details[i].rectTransform,new Vector2(.112f,0),new Vector2(Columns[1],.4f));details[i].fontSize=Mathf.RoundToInt(title.fontSize*.6f);details[i].color=Muted;details[i].text=kind==NativeParticipantKind.Bot?new[]{"Салага","Боец","Ветеран"}[info.Value.Difficulty]:kind==NativeParticipantKind.DiagnosticFixture?"FIXTURE":"ИГРОК";
                    if(dead){float opacity=tuning.Get("ui.standingsDeadOpacity");cells[r,0].color=Color.Lerp(Base,identity,opacity);for(int c=1;c<8;c++)cells[r,c].color=Color.Lerp(Base,Ink,opacity);}
                }
                // Measure the complete rich-text group. One font size for main and suffix retains its exact center.
                for(int c=0;c<8;c++)Fit(cells[r,c]);
            }
        }
        static void Fit(Text text){while(text.fontSize>1 && text.preferredWidth>Mathf.Max(1,text.rectTransform.rect.width))text.fontSize--;}
        static void Rect(RectTransform r,Vector2 min,Vector2 max){r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
    }
}
