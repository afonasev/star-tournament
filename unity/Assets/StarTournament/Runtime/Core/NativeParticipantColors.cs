using UnityEngine;
using System.Linq;

namespace StarTournament.ProvingGround
{
    public static class NativeParticipantColors
    {
        // Approved vetted identity palette from src/match/participantPalette.ts; presentation constants, not balance.
        static readonly Color[] colors={
            new Color32(59,130,246,255),new Color32(217,70,239,255),new Color32(251,113,133,255),new Color32(139,92,246,255),
            new Color32(16,185,129,255),new Color32(245,158,11,255),new Color32(225,29,72,255),new Color32(14,165,233,255),
            new Color32(99,102,241,255),new Color32(20,184,166,255),new Color32(34,197,94,255),new Color32(168,85,247,255)};
        public static bool Contains(Color color)=>colors.Contains(color);
        public static Color[] Read()=>(Color[])colors.Clone();
        public static Color For(int participant)=>colors[participant];
    }
}
