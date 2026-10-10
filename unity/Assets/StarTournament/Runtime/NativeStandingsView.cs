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
        public int PerspectiveParticipant { get; set; } = -1;
        public bool AllowSelection { get; set; }
        public bool FitToContent { get; set; }
        public float ContentHeight { get; private set; }
        public int SelectedParticipant { get; private set; } = -1;
        public event Action<int> ParticipantSelected;
        readonly Button[] rowButtons=new Button[10];
        readonly Text[] duels=new Text[10];
        readonly int[] rowParticipants=Enumerable.Repeat(-1,10).ToArray();
        readonly StandingsIcon[] duelHelmets=new StandingsIcon[2];
        readonly Text duelVs;
        NativeMatchSnapshot shownSnapshot;
        NativeMatchComposition shownComposition;
        CombatLifeState[] shownLives;
        bool shownDiagnostic;
        bool hasPerspective;
        static readonly float[] DuelColumns={0,.25f,.37f,.45f,.51f,.57f,.70f,.82f,.89f,1};
        static readonly Color DuelWin=new Color32(117,221,169,255), DuelLoss=new Color32(255,102,120,255);
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
            var headerParent=Root.transform; // Reparent onto the header once it exists.
            duelVs=Text(headerParent,font,"duel-vs",size,TextAnchor.MiddleCenter);duelVs.text="VS";duelVs.color=Muted;
            for(int i=0;i<2;i++){duelHelmets[i]=Icon(headerParent,"duel-helmet-"+i,StandingsIcon.Symbol.Helmet);duelHelmets[i].color=Muted;}
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
                if(r==0){cells[0,0].text="УЧАСТНИК";cells[0,0].color=Muted;
                    duelVs.transform.SetParent(row.transform,false);foreach(var helmet in duelHelmets)helmet.transform.SetParent(row.transform,false);continue;}
                int i=r-1;backgrounds[i]=image;
                duels[i]=Text(row.transform,font,"duel",size,TextAnchor.MiddleCenter);
                rowButtons[i]=row.AddComponent<Button>();rowButtons[i].targetGraphic=image;
                rowButtons[i].transition=Selectable.Transition.None;rowButtons[i].enabled=false;
                int index=i;
                rowButtons[i].onClick.AddListener(()=>SelectParticipant(rowParticipants[index]));
                row.AddComponent<NativeStandingsSelection>().Selected=()=>SelectParticipant(rowParticipants[index]);
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
        public static void FitResultsFrame(RectTransform frame, float width=1920, float height=1080)
        {
            // Scale the whole content block equally on both axes inside 6% screen gutters.
            var parent=(RectTransform)frame.parent;
            frame.anchorMin=frame.anchorMax=frame.pivot=new Vector2(.5f,.5f);
            frame.anchoredPosition=Vector2.zero;frame.sizeDelta=new Vector2(width,height);
            frame.localScale=Vector3.one*Mathf.Min(parent.rect.width*.88f/width,parent.rect.height*.88f/height);
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
            if(!visible){Root.SetActive(false);SelectedParticipant=-1;shownSnapshot=null;return;}
            Root.SetActive(true);if(state?.Standings==null||state.Standings.Length==0)return;
            shownSnapshot=state;shownComposition=composition;shownLives=lives;shownDiagnostic=diagnostic;
            if(AllowSelection && !state.Standings.Any(s=>s.Seat==SelectedParticipant))
                SelectedParticipant=composition==null?state.Standings[0].Seat:
                    Enumerable.Range(0,composition.ParticipantCount).Where(p=>composition.Participant(p).Kind==NativeParticipantKind.LocalHuman).DefaultIfEmpty(0).First();
            int owner=AllowSelection?SelectedParticipant:PerspectiveParticipant;
            hasPerspective=owner>=0 && state.Standings.Any(s=>s.Seat==owner);
            bool teams=state.Roster?.Mode==NativeMatchMode.Teams,finished=state.Phase==NativeMatchPhase.Finished;
            var entries=new List<(NativeStanding? player,NativeTeam team,int total)>();
            if(teams)foreach(var team in state.Teams){entries.Add((null,team.Team,team.Score));foreach(var row in state.Standings.Where(r=>state.Roster.Teams[r.Seat]==team.Team))entries.Add((row,team.Team,0));}
            else foreach(var row in state.Standings)entries.Add((row,NativeTeam.None,0));
            var rect=(RectTransform)Root.transform;float padding=tuning.Get("ui.standingsPadding");
            float fontSize=tuning.Get("ui.standingsFontSize"), toolbarHeight=fontSize*1.6f, headerHeight=fontSize*1.25f;
            float desiredHeight=TopInset+2*padding+toolbarHeight+headerHeight+entries.Count*fontSize*1.5f;
            ContentHeight=desiredHeight;
            if(FitToContent)
            {
                float available=((RectTransform)rect.parent).rect.height*(rect.anchorMax.y-rect.anchorMin.y);
                rect.sizeDelta=new Vector2(rect.sizeDelta.x,Mathf.Min(available,desiredHeight)-available);
            }
            float height=Mathf.Max(1,rect.rect.height-TopInset-2*padding);
            float factor=Mathf.Min(1,height/Mathf.Max(1,toolbarHeight+headerHeight+entries.Count*fontSize*1.5f));
            toolbarHeight*=factor;headerHeight*=factor;float rowHeight=fontSize*1.5f*factor;
            Rect(title.rectTransform,new Vector2(0,1),Vector2.one);title.rectTransform.offsetMin=new Vector2(padding,-TopInset-padding-toolbarHeight);title.rectTransform.offsetMax=new Vector2(-padding,-TopInset-padding);
            title.text=(teams?"Командный матч":"FFA")+(finished?" · РЕЗУЛЬТАТЫ":"");
            if(finished)title.text+=" · "+(teams?TeamName(state.WinnerTeam):SafeName(composition?.Participant(state.Winner).Name??("Игрок "+(state.Winner+1))))+" — победитель";
            title.fontSize=Mathf.Max(10,Mathf.RoundToInt(fontSize*factor));title.fontStyle=FontStyle.Bold;
            Fit(title);
            for(int r=0;r<11;r++)
            {
                var row=(RectTransform)cells[r,0].transform.parent;Rect(row,new Vector2(0,1),Vector2.one);
                float rowTop=TopInset+padding+toolbarHeight+(r==0?0:headerHeight+(r-1)*rowHeight);
                row.offsetMin=new Vector2(padding,-rowTop-(r==0?headerHeight:rowHeight));row.offsetMax=new Vector2(-padding,-rowTop);
                for(int c=0;c<8;c++)Rect(cells[r,c].rectTransform,new Vector2(ColumnStart(c),0),new Vector2(ColumnEnd(c),1));
                if(r==0)
                {
                    foreach(var icon in row.GetComponentsInChildren<StandingsIcon>())if(icon.name.StartsWith("header-")){int c=int.Parse(icon.name.Substring(7));Place(icon.rectTransform,(ColumnStart(c)+ColumnEnd(c))/2,0,Mathf.Min(1,headerHeight/tuning.Get("ui.standingsIconSize")));}
                    cells[0,0].fontSize=Mathf.Max(9,Mathf.RoundToInt(fontSize*.6f*factor));
                    duelVs.gameObject.SetActive(hasPerspective);foreach(var helmet in duelHelmets)helmet.gameObject.SetActive(hasPerspective);
                    float center=(DuelColumns[1]+DuelColumns[2])*.5f, gap=Mathf.Min(headerHeight*.85f,tuning.Get("ui.standingsIconSize"));
                    for(int h=0;h<2;h++){Place(duelHelmets[h].rectTransform,center,0,.8f*factor);duelHelmets[h].rectTransform.anchoredPosition=new Vector2((h==0?-1:1)*gap,0);duelHelmets[h].rectTransform.localScale=new Vector3(h==0?1:-1,1,1);}
                    duelVs.rectTransform.anchorMin=duelVs.rectTransform.anchorMax=new Vector2(center,.5f);duelVs.rectTransform.sizeDelta=new Vector2(gap,gap);duelVs.fontSize=Mathf.Max(8,Mathf.RoundToInt(fontSize*.5f*factor));continue;
                }
                int i=r-1;row.gameObject.SetActive(i<entries.Count);rowParticipants[i]=-1;if(i>=entries.Count)continue;
                for(int c=0;c<8;c++){cells[r,c].text="";cells[r,c].color=Ink;cells[r,c].fontSize=Mathf.Max(10,Mathf.RoundToInt(fontSize*factor));cells[r,c].fontStyle=c==7?FontStyle.Bold:FontStyle.Normal;}
                var entry=entries[i];bool teamHeader=!entry.player.HasValue;rowParticipants[i]=teamHeader?-1:entry.player.Value.Seat;
                rowButtons[i].enabled=AllowSelection&&!teamHeader;imageFor(i).raycastTarget=rowButtons[i].enabled;
                bool leader=teamHeader?(finished?entry.team==state.WinnerTeam:entry.total==state.Teams.Max(t=>t.Score)):!teams&&(finished?entry.player.Value.Seat==state.Winner:entry.player.Value.Score==state.Standings.Max(s=>s.Score));
                Color identity=teamHeader?(composition==null?TeamColor(entry.team,SwapTeamColors):composition.Participant(Enumerable.Range(0,composition.ParticipantCount).First(p=>composition.Roster.TeamOf(p)==entry.team)).Color):composition?.Participant(entry.player.Value.Seat).Color??Identity(state,entry.player.Value.Seat,SwapTeamColors);
                backgrounds[i].color=AllowSelection&&!teamHeader&&entry.player.Value.Seat==owner?new Color32(34,58,83,255):Color.Lerp(Base,identity,teamHeader?tuning.Get("ui.standingsLeaderAccent"):0);
                duels[i].gameObject.SetActive(hasPerspective);Rect(duels[i].rectTransform,new Vector2(DuelColumns[1],0),new Vector2(DuelColumns[2],1));
                duels[i].fontSize=Mathf.Max(10,Mathf.RoundToInt(fontSize*factor));duels[i].fontStyle=FontStyle.Bold;
                var pair=Pair(state,owner,teamHeader?-1:entry.player.Value.Seat);duels[i].text=pair.text;duels[i].color=pair.color;
                Rect(accents[i].rectTransform,Vector2.zero,new Vector2(0,1));accents[i].rectTransform.sizeDelta=new Vector2(3,0);accents[i].color=identity;accents[i].gameObject.SetActive(teamHeader);
                Place(crowns[i].rectTransform,ColumnEnd(0)-.025f,0,.85f);crowns[i].gameObject.SetActive(leader);
                lifeIcons[i].gameObject.SetActive(!teamHeader&&!finished);kindIcons[i].gameObject.SetActive(!teamHeader);ranks[i].gameObject.SetActive(!teamHeader);details[i].gameObject.SetActive(!teamHeader);
                Rect(cells[r,0].rectTransform,new Vector2(.08f,0),new Vector2(ColumnEnd(0)-.025f,1));cells[r,0].color=identity;cells[r,0].fontStyle=FontStyle.Bold;
                if(teamHeader)
                {cells[r,0].text=TeamName(entry.team);Rect(cells[r,0].rectTransform,new Vector2(.06f,0),new Vector2(ColumnEnd(0)-.05f,1));cells[r,7].text=entry.total.ToString();lifeIcons[i].gameObject.SetActive(true);lifeIcons[i].Kind=StandingsIcon.Symbol.Shield;lifeIcons[i].color=identity;Place(lifeIcons[i].rectTransform,.025f,0,1);}
                else
                {
                    var s=entry.player.Value;var info=composition?.Participant(s.Seat);bool dead=!finished && lives!=null && lives[s.Seat].Dead;
                    var values=Values(s,teams);for(int c=1;c<8;c++)cells[r,c].text=values[c];
                    cells[r,0].text=(info?.Name??"Игрок "+(s.Seat+1)).Replace('<','‹').Replace('>','›');
                    lifeIcons[i].Kind=dead?StandingsIcon.Symbol.Skull:StandingsIcon.Symbol.Pulse;lifeIcons[i].color=dead?Muted:Pulse;Place(lifeIcons[i].rectTransform,.06f,0,1);
                    Rect(ranks[i].rectTransform,Vector2.zero,new Vector2(.035f,1));ranks[i].text=(teams?state.Standings.Where(p=>state.Roster.Teams[p.Seat]==entry.team).ToList().FindIndex(p=>p.Seat==s.Seat)+1:i+1).ToString("00");ranks[i].color=Muted;ranks[i].fontSize=Mathf.Max(8,Mathf.RoundToInt(fontSize*.6f*factor));
                    var kind=info?.Kind??(diagnostic?NativeParticipantKind.DiagnosticFixture:NativeParticipantKind.LocalHuman);
                    kindIcons[i].Kind=kind==NativeParticipantKind.Bot?StandingsIcon.Symbol.Bot:StandingsIcon.Symbol.Fixture;kindIcons[i].color=Muted;
                    bool labelled=kind!=NativeParticipantKind.LocalHuman;
                    kindIcons[i].gameObject.SetActive(labelled);details[i].gameObject.SetActive(labelled);
                    // Keep the name and small bot metadata on the same baseline.
                    float nameEnd=ColumnEnd(0)-.025f;
                    if(labelled)
                    {
                        cells[r,0].fontSize=Mathf.Max(10,Mathf.RoundToInt(fontSize*factor));
                        float iconWidth=tuning.Get("ui.standingsIconSize")*.55f*factor;
                        float nameWidth=Mathf.Min(cells[r,0].preferredWidth,row.rect.width*(nameEnd-.08f)-iconWidth-52*factor);
                        float iconX=.08f+(nameWidth+iconWidth*.8f)/Mathf.Max(1,row.rect.width);
                        Place(kindIcons[i].rectTransform,iconX,0,.55f*factor);
                        Rect(cells[r,0].rectTransform,new Vector2(.08f,0),new Vector2(Mathf.Max(.10f,iconX-iconWidth*.6f/Mathf.Max(1,row.rect.width)),1));
                        Rect(details[i].rectTransform,new Vector2(iconX+iconWidth*.7f/Mathf.Max(1,row.rect.width),0),new Vector2(nameEnd,1));
                        details[i].fontSize=Mathf.Max(8,Mathf.RoundToInt(fontSize*.55f*factor));details[i].color=Muted;
                        details[i].text=kind==NativeParticipantKind.Bot?new[]{"Салага","Боец","Ветеран"}[Mathf.Clamp(info?.Difficulty??0,0,2)]:"FIXTURE";Fit(details[i]);
                    }
                    if(dead){float opacity=tuning.Get("ui.standingsDeadOpacity");cells[r,0].color=Color.Lerp(Base,identity,opacity);for(int c=1;c<8;c++)cells[r,c].color=Color.Lerp(Base,Ink,opacity);}
                }
                // Measure the complete rich-text group. One font size for main and suffix retains its exact center.
                for(int c=0;c<8;c++)Fit(cells[r,c]);Fit(duels[i]);
            }
        }
        static string SafeName(string name)=>name.Replace('<','‹').Replace('>','›');
        Image imageFor(int row)=>backgrounds[row];
        float ColumnStart(int c)=>hasPerspective?DuelColumns[c==0?0:c+1]:Columns[c];
        float ColumnEnd(int c)=>hasPerspective?DuelColumns[c==0?1:c+2]:Columns[c+1];
        public static (string text,Color color) Pair(NativeMatchSnapshot state,int owner,int opponent)
        {
            if(owner<0||opponent<0||owner==opponent)return ("—",Muted);
            if(state?.Roster?.Mode==NativeMatchMode.Teams && owner<state.Roster.Teams.Length && opponent<state.Roster.Teams.Length && state.Roster.Teams[owner]==state.Roster.Teams[opponent])return ("—",Muted);
            int count=state?.ParticipantCount??0;
            if(owner>=count||opponent>=count||state.DirectKillsByPair==null||state.DirectKillsByPair.Length!=count*count)return ("н/д",Muted);
            int a=state.DirectKills(owner,opponent),b=state.DirectKills(opponent,owner);
            return (a+" : "+b,a>b?DuelWin:a<b?DuelLoss:Color.white);
        }
        public bool SelectParticipant(int participant)
        {
            if(!AllowSelection||shownSnapshot==null||!shownSnapshot.Standings.Any(s=>s.Seat==participant))return false;
            if(SelectedParticipant==participant)return true;
            SelectedParticipant=participant;Show(true,shownSnapshot,shownDiagnostic,shownComposition,shownLives);ParticipantSelected?.Invoke(participant);return true;
        }
        public void ConfigureResultNavigation(Selectable repeat,Selectable menu)
        {
            if(!AllowSelection||!Root.activeSelf)return;
            var buttons=rowButtons.Where(b=>b.enabled&&b.gameObject.activeInHierarchy).ToArray();
            for(int i=0;i<buttons.Length;i++)buttons[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,
                selectOnUp=i==0?repeat:buttons[i-1],selectOnDown=i==buttons.Length-1?repeat:buttons[i+1],selectOnLeft=buttons[i],selectOnRight=buttons[i]};
            if(buttons.Length==0)return;
            repeat.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=buttons[buttons.Length-1],selectOnDown=buttons[0],selectOnLeft=menu,selectOnRight=menu};
            menu.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=buttons[buttons.Length-1],selectOnDown=buttons[0],selectOnLeft=repeat,selectOnRight=repeat};
        }
        static void Fit(Text text){while(text.fontSize>1 && text.preferredWidth>Mathf.Max(1,text.rectTransform.rect.width))text.fontSize--;}
        static void Rect(RectTransform r,Vector2 min,Vector2 max){r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
    }
}
