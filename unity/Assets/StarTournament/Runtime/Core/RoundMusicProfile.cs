using System;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        // round-music-v1 tuning lives in the named proving-ground presentation registry.
        void AddRoundMusicDescriptors()
        {
            void V(string key,string label,string description,string unit,float min,float max,float step,float value)=>Add("audio.music."+key,"music",label,description,unit,min,max,step,value);
            V("gain","Уровень игрового музыкального микса","Уровень музыки до пользовательской громкости; общий микс музыки и эффектов нормализуется без перегрузки.","ratio",0,1,.05f,.55f);
            V("transitionBeats","Плавность музыкального перехода","Длительность перехода в долях текущего темпа; ограничивается доступной длиной петли.","beats",1,8,1,4);
            V("finishSeconds","Хвост завершения музыки","Время затухания при завершении матча.","seconds",.2f,5,.1f,2);
            // menu-music-v1: user selected the four-second comparison, 2026-10-04.
            V("menuTransitionSeconds","Переход меню и матча","Время плавной смены темы меню и матчевой музыки; повторная навигация сохраняет текущую огибающую.","seconds",.2f,8,.1f,4);
            V("developAt","Начало развития музыки","Доля прошедшей длительности раунда для средней фазы.","ratio",.1f,.6f,.01f,.33f);
            V("climaxAt","Начало финальной фазы","Доля прошедшей длительности раунда для насыщенной быстрой фазы; overtime всегда использует её.","ratio",.6f,.9f,.01f,.67f);
        }
        internal ProvingProfile BeforeRoundMusic()
        {
            if(id!=DefaultId||Descriptor("audio.music.gain")==null)return this;
            var copy=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(this));
            copy.descriptors.RemoveAll(d=>d.Path.StartsWith("audio.music.",StringComparison.Ordinal));
            copy.values.RemoveAll(v=>v.Path.StartsWith("audio.music.",StringComparison.Ordinal));copy.valueIndex=null;
            copy.FindDescriptor("audio.musicDefaultPercent").Description="Начальная громкость музыкального канала до пользовательского изменения; трек пока отсутствует.";
            return copy;
        }
        internal ProvingProfile BeforeMenuMusic()
        {
            if(id!=DefaultId||Descriptor("audio.music.menuTransitionSeconds")==null)return this;
            var copy=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(this));
            copy.descriptors.RemoveAll(d=>d.Path=="audio.music.menuTransitionSeconds");
            copy.values.RemoveAll(v=>v.Path=="audio.music.menuTransitionSeconds");copy.valueIndex=null;
            return copy;
        }
    }
}
