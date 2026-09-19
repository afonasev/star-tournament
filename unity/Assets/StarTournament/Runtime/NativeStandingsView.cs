using System;
using UnityEngine;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    /// <summary>Read-only table. Every row shares the same seven-column grid.</summary>
    public sealed class NativeStandingsView
    {
        public GameObject Root { get; }
        readonly Text[,] cells = new Text[5,7];
        readonly Image[] backgrounds = new Image[4];
        // Participant identities use the vetted palette. Grid ratios are layout structure, not gameplay tuning.
        public static readonly Color[] Palette = { new Color32(59,130,246,255), new Color32(217,70,239,255), new Color32(251,113,133,255), new Color32(139,92,246,255) };
        public NativeStandingsView(Transform parent, Font font, int size, string name, Vector2 min, Vector2 max)
        {
            Root=new GameObject(name,typeof(RectTransform),typeof(Image)); Root.transform.SetParent(parent,false);
            Rect((RectTransform)Root.transform,min,max); Root.GetComponent<Image>().color=new Color32(10,18,30,245);
            string[] headers={"УЧАСТНИК","УБ","ПМ","СМ","УРОН +","УРОН −","ОЧКИ"};
            float[] columns={0,.28f,.38f,.48f,.58f,.73f,.88f,1};
            for(int r=0;r<5;r++)
            {
                var row=new GameObject("row-"+r,typeof(RectTransform),typeof(Image)); row.transform.SetParent(Root.transform,false);
                Rect((RectTransform)row.transform,new Vector2(0,1-(r+1)/5f),new Vector2(1,1-r/5f));
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
        public void Show(bool visible, NativeMatchSnapshot state, bool diagnostic)
        {
            Root.SetActive(visible); if(!visible || state==null) return;
            int maximum=state.Standings[0].Score;
            for(int r=0;r<state.Standings.Length;r++)
            {
                var s=state.Standings[r];
                string[] values={"Игрок "+(s.Seat+1)+(diagnostic?" · D":" · Ч"),s.Kills.ToString(),s.Assists.ToString(),s.Deaths.ToString(),
                    Math.Round(s.DamageDealt,MidpointRounding.AwayFromZero).ToString(),Math.Round(s.DamageReceived,MidpointRounding.AwayFromZero).ToString(),s.Score.ToString()};
                for(int c=0;c<7;c++) { cells[r+1,c].text=values[c]; cells[r+1,c].color=s.Score==maximum?Palette[s.Seat]:Color.white; }
                backgrounds[r].color=diagnostic?new Color32(23,39,52,255):Color.Lerp(new Color32(23,39,52,255),Palette[s.Seat],.15f);
            }
        }
        static void Rect(RectTransform r,Vector2 min,Vector2 max) { r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero; }
    }
}
