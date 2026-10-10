using UnityEngine;

namespace StarTournament.ProvingGround
{
    public static class NativeKillNotice
    {
        public static readonly Color SeriesColor=new Color32(255,207,82,255);
        public static string Points(int points)=>points<0?"−"+(-(long)points):"+"+points;
        public static string PenaltyPoints(int points)=>"−"+System.Math.Abs((long)points);
        public static string Text(string victim,string pair,KillScoreEvent score)
        {
            string text=(score.Enemy?"Вы убили ":"Вы убили союзника ")+victim+" · "+(score.Enemy?Points(score.Points):PenaltyPoints(score.Points));
            if(score.Enemy)text+=" · "+pair;
            if(score.Enemy&&score.SeriesEligible&&score.Chain>=2)text+="\nСерия убийств! · "+score.Chain;
            return text;
        }
        public static Color ColorFor(KillScoreEvent score)=>!score.Enemy?Color.red:
            score.SeriesEligible&&score.Chain>=2?SeriesColor:Color.white;
        public static string Voice(KillScoreEvent score)=>score.Enemy&&score.SeriesEligible?
            score.Chain==3?"kill-triple":score.Chain==4?"kill-quadruple":null:null;
    }
}
