using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    /// <summary>Read-only table. Every row shares the same seven-column grid.</summary>
    public sealed class NativeStandingsView
    {
        public GameObject Root { get; }
        public float TopInset { get; set; }
        readonly Text[,] cells = new Text[11,7];
        readonly Image[] backgrounds = new Image[10];
        // Participant identities use the vetted palette. Grid ratios are layout structure, not gameplay tuning.
        public static Color[] Palette => NativeParticipantColors.Read();
        public bool SwapTeamColors { get; set; }
        public static string TeamName(NativeTeam team) => team==NativeTeam.TeamA?"Team A":team==NativeTeam.TeamB?"Team B":"";
        public static Color TeamColor(NativeTeam team,bool swap) => Palette[(team==NativeTeam.TeamA ? 0 : 1) ^ (swap?1:0)];
        public static Color Identity(NativeMatchSnapshot state,int participant,bool swap) => Identity(state?.Roster,participant,swap);
        public static Color Identity(NativeRosterSnapshot roster,int participant,bool swap) => roster?.Mode==NativeMatchMode.Teams ? TeamColor(roster.Teams[participant],swap) : Palette[participant];
        public NativeStandingsView(Transform parent, Font font, int size, string name, Vector2 min, Vector2 max)
        {
            Root=new GameObject(name,typeof(RectTransform),typeof(Image)); Root.transform.SetParent(parent,false);
            Rect((RectTransform)Root.transform,min,max); Root.GetComponent<Image>().color=new Color32(10,18,30,255); Root.GetComponent<Image>().raycastTarget=false;
            string[] headers={"УЧАСТНИК","УБ","ПМ","СМ","УРОН +","УРОН −","ОЧКИ"};
            float[] columns={0,.28f,.38f,.48f,.58f,.73f,.88f,1};
            // Header + eight participant rows + two team totals; structural UI capacity.
            for(int r=0;r<11;r++)
            {
                var row=new GameObject("row-"+r,typeof(RectTransform),typeof(Image)); row.transform.SetParent(Root.transform,false);
                Rect((RectTransform)row.transform,new Vector2(0,1-(r+1)/5f),new Vector2(1,1-r/5f));
                row.GetComponent<Image>().raycastTarget=false;
                row.GetComponent<Image>().color=r==0 ? new Color32(32,55,72,255) : new Color32(23,39,52,255);
                if(r>0) backgrounds[r-1]=row.GetComponent<Image>();
                for(int c=0;c<7;c++)
                {
                    var go=new GameObject("cell-"+c,typeof(RectTransform),typeof(Text)); go.transform.SetParent(row.transform,false);
                    var t=go.GetComponent<Text>(); t.font=font; t.fontSize=size; t.color=Color.white;
                    t.alignment=c==0?TextAnchor.MiddleLeft:TextAnchor.MiddleRight; t.raycastTarget=false;
                    Rect(t.rectTransform,new Vector2(columns[c],0),new Vector2(columns[c+1],1));
                    t.rectTransform.offsetMin=new Vector2(size*.4f,0); t.rectTransform.offsetMax=new Vector2(-size*.4f,0);
                    t.text=r==0?headers[c]:""; cells[r,c]=t;
                }
            }
        }
        public void Show(bool visible, NativeMatchSnapshot state, bool diagnostic, NativeMatchComposition composition=null)
        {
            Root.SetActive(visible); if(!visible || state==null) return;
            bool teams=state.Roster?.Mode==NativeMatchMode.Teams;
            var entries=new List<(NativeStanding? player,NativeTeam team,int total)>();
            if(teams)
            {
                foreach(var team in state.Teams)
                {
                    entries.Add((null,team.Team,team.Score));
                    foreach(var row in state.Standings.Where(r=>state.Roster.Teams[r.Seat]==team.Team)) entries.Add((row,team.Team,0));
                }
            }
            else foreach(var row in state.Standings) entries.Add((row,NativeTeam.None,0));
            int rowCount=Math.Max(5,entries.Count+1);
            float contentHeight=1-Mathf.Clamp01(TopInset/Mathf.Max(1,((RectTransform)Root.transform).rect.height));
            Rect((RectTransform)cells[0,0].transform.parent,new Vector2(0,contentHeight*(1-1f/rowCount)),new Vector2(1,contentHeight));
            int maximum=state.Standings.Length>0?state.Standings[0].Score:0;
            for(int r=0;r<backgrounds.Length;r++)
            {
                var root=backgrounds[r].gameObject;root.SetActive(r<entries.Count);
                for(int c=0;c<7;c++) cells[r+1,c].text="";
                if(r>=entries.Count) continue;
                Rect((RectTransform)root.transform,new Vector2(0,contentHeight*(1-(r+2f)/rowCount)),new Vector2(1,contentHeight*(1-(r+1f)/rowCount)));
                var entry=entries[r];
                if(!entry.player.HasValue)
                {
                    cells[r+1,0].text=TeamName(entry.team);cells[r+1,6].text=entry.total.ToString();
                    Color teamColor=composition==null?TeamColor(entry.team,SwapTeamColors):composition.Participant(Enumerable.Range(0,composition.ParticipantCount).First(p=>composition.Roster.TeamOf(p)==entry.team)).Color;
                    for(int c=0;c<7;c++) cells[r+1,c].color=teamColor;
                    backgrounds[r].color=new Color32(32,55,72,255);continue;
                }
                var s=entry.player.Value;var color=composition?.Participant(s.Seat).Color ?? Identity(state,s.Seat,SwapTeamColors);
                bool local=composition!=null?composition.SeatOf(s.Seat)>=0:!diagnostic;
                string[] values={composition?.Participant(s.Seat).Label ?? ("Игрок "+(s.Seat+1)+(diagnostic?" · D":" · Ч")),s.Kills.ToString(),s.Assists.ToString(),s.Deaths.ToString(),
                    Math.Round(s.DamageDealt,MidpointRounding.AwayFromZero).ToString(),Math.Round(s.DamageReceived,MidpointRounding.AwayFromZero).ToString(),s.Score.ToString()};
                for(int c=0;c<7;c++) { cells[r+1,c].text=values[c];cells[r+1,c].color=c==0 || teams || s.Score==maximum?color:Color.white; }
                backgrounds[r].color=!local?new Color32(23,39,52,255):Color.Lerp(new Color32(23,39,52,255),color,.15f);
            }
        }

        static void Rect(RectTransform r,Vector2 min,Vector2 max) { r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero; }
    }
}
