using System;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        internal const float DefaultMusicBalanceGain=.32f;
        internal static float MusicBalanceGain(ProvingProfile profile)=>profile.Descriptor("audio.music.balanceGain")==null?DefaultMusicBalanceGain:profile.Get("audio.music.balanceGain");

        internal const float DefaultCombatMusicDuckGain=.25f;
        internal const float DefaultCombatMusicDuckReleaseSeconds=.3f;
        internal const float DefaultCombatMusicDuckReferenceGain=.3f;
        internal static float CombatMusicDuckGain(ProvingProfile profile)=>profile.Descriptor("audio.music.combatDuckGain")==null?DefaultCombatMusicDuckGain:profile.Get("audio.music.combatDuckGain");
        internal static float CombatMusicDuckReleaseSeconds(ProvingProfile profile)=>profile.Descriptor("audio.music.combatDuckReleaseSeconds")==null?DefaultCombatMusicDuckReleaseSeconds:profile.Get("audio.music.combatDuckReleaseSeconds");

        internal static float CombatMusicDuckReferenceGain(ProvingProfile profile)=>profile.Descriptor("audio.music.combatDuckReferenceGain")==null?DefaultCombatMusicDuckReferenceGain:profile.Get("audio.music.combatDuckReferenceGain");

        // round-music-v1 tuning lives in the named proving-ground presentation registry.
        void AddRoundMusicDescriptors()
        {
            void V(string key,string label,string description,string unit,float min,float max,float step,float value)=>Add("audio.music."+key,"music",label,description,unit,min,max,step,value);
            V("gain","Уровень игрового музыкального микса","Уровень музыки до пользовательской громкости; общий микс музыки и эффектов нормализуется без перегрузки.","ratio",0,1,.05f,.55f);
            V("balanceGain","Музыка относительно эффектов","Дополнительный уровень обеих музыкальных тем относительно эффектов; 0.32 снижает музыку примерно на 10 дБ без изменения сохранённых регуляторов.","ratio",0,1,.01f,DefaultMusicBalanceGain);
            V("combatDuckGain","Музыка во время стрельбы","Минимальная доля уровня музыки под слышимыми выстрелами и резаком; далёкие и тихие эффекты приглушают её слабее. 1 отключает приглушение.","ratio",.1f,1,.05f,DefaultCombatMusicDuckGain);
            V("combatDuckReferenceGain","Порог приглушения музыки","Уровень слышимого боевого эффекта после пользовательской громкости, при котором музыка достигает минимальной доли; более тихие и дальние эффекты приглушают её пропорционально слабее.","ratio",.05f,1,.05f,DefaultCombatMusicDuckReferenceGain);
            V("combatDuckReleaseSeconds","Возврат музыки после стрельбы","Время плавного восстановления музыки от максимального приглушения после окончания слышимых боевых эффектов.","seconds",.05f,2,.05f,DefaultCombatMusicDuckReleaseSeconds);
            V("transitionBeats","Плавность музыкального перехода","Длительность перехода в долях текущего темпа; ограничивается доступной длиной петли.","beats",1,8,1,4);
            V("finishSeconds","Хвост завершения музыки","Время затухания при завершении матча.","seconds",.2f,5,.1f,2);
            // menu-music-v1: user selected the four-second comparison, 2026-10-04.
            V("menuTransitionSeconds","Переход меню и матча","Время плавной смены темы меню и матчевой музыки; повторная навигация сохраняет текущую огибающую.","seconds",.2f,8,.1f,4);
            V("developAt","Начало развития музыки","Доля прошедшей длительности раунда для средней фазы.","ratio",.1f,.6f,.01f,.33f);
            V("climaxAt","Начало финальной фазы","Доля прошедшей длительности раунда для насыщенной быстрой фазы; overtime всегда использует её.","ratio",.6f,.9f,.01f,.67f);
        }
        internal ProvingProfile BeforeMusicBalance()
        {
            const string path="audio.music.balanceGain";
            if(id!=DefaultId||Descriptor(path)==null)return this;
            var copy=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(this));
            copy.descriptors.RemoveAll(d=>d.Path==path);
            copy.values.RemoveAll(v=>v.Path==path);copy.valueIndex=null;
            return copy;
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
