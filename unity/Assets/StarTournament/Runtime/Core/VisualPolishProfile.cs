namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        internal ProvingProfile BeforeVisualPolish()
        {
            if(id!=DefaultId||Descriptor(NativeVisualPreferences.Path(VisualEffect.Lighting))==null)return this;
            var copy=DetachedCopy();copy.descriptors.RemoveAll(d=>d.Path.StartsWith("visual.",System.StringComparison.Ordinal));copy.values.RemoveAll(v=>v.Path.StartsWith("visual.",System.StringComparison.Ordinal));copy.version=System.Math.Min(copy.version,15);return copy;
        }
        internal ProvingProfile BeforeNeutralColorGrading()
        {
            if(id!=DefaultId||Descriptor(NativeVisualPreferences.Path(VisualEffect.ColorGrading))==null)return this;
            var copy=DetachedCopy();copy.descriptors.RemoveAll(d=>d.Path.StartsWith("visual.ColorGrading",System.StringComparison.Ordinal));copy.values.RemoveAll(v=>v.Path.StartsWith("visual.ColorGrading",System.StringComparison.Ordinal));copy.version=System.Math.Min(copy.version,16);return copy;
        }
        // v17 is an exact trusted predecessor: preserve its metadata and values for history validation.
        internal static readonly string[] ArenaVisualPaths={"visual.fillScale","visual.keyScale","visual.wallSmoothness","visual.floorSmoothness","visual.metalSmoothness","visual.wallMetallic","visual.floorMetallic","visual.metalMetallic"};
        internal static readonly float[] ArenaVisualPrevious={.75f,1.25f,.2f,.35f,.55f,.05f,.1f,.65f};
        internal ProvingProfile BeforeArenaHarmonization()
        {
            if(id!=DefaultId||version<18)return this;
            var copy=DetachedCopy();copy.version=17;
            copy.descriptors.RemoveAll(d=>d.Path=="visual.ambientLift"||d.Path.EndsWith("LocalLightScale",System.StringComparison.Ordinal));
            copy.values.RemoveAll(v=>v.Path=="visual.ambientLift"||v.Path.EndsWith("LocalLightScale",System.StringComparison.Ordinal));
            for(int i=0;i<ArenaVisualPaths.Length;i++){copy.FindDescriptor(ArenaVisualPaths[i]).DefaultValue=ArenaVisualPrevious[i];copy.Set(ArenaVisualPaths[i],ArenaVisualPrevious[i]);}
            return copy;
        }
        internal static float UpgradeArenaVisualValue(string path,int previousVersion,float value,NumericDescriptor current)
        {
            int i=System.Array.IndexOf(ArenaVisualPaths,path);
            return previousVersion<18&&i>=0&&UnityEngine.Mathf.Approximately(value,ArenaVisualPrevious[i])?current.DefaultValue:value;
        }
        static void VisualPolishDescriptors(ProvingProfile p)
        {
            foreach(VisualEffect effect in System.Enum.GetValues(typeof(VisualEffect)))
                p.Add(NativeVisualPreferences.Path(effect),"visual-polish",NativeVisualPreferences.Names[(int)effect],"Сила улучшения графики: 0 — исходный вид; пользовательский выбор сохраняется отдельно от Lab.","%",0,100,effect==VisualEffect.AmbientOcclusion?25:5,50);
            void V(string path,string label,float min,float max,float step,float value)=>p.Add("visual."+path,"visual-polish",label,"Presentation-only художественный параметр; применяется при подготовке арены.","ratio",min,max,step,value);
            V("fillScale","Фоновая заливка",.4f,1,.05f,.95f);V("keyScale","Основной свет",1,2,.05f,1f);
            V("ambientLift","Заполнение тёмных переходов",0,.4f,.01f,.18f);
            V("bowlLocalLightScale","Локальный свет Combat Bowl",0,2,.05f,.8f);
            V("tunnelsLocalLightScale","Локальный свет тоннелей",0,2,.05f,.8f);
            V("lunarLocalLightScale","Локальный свет лаборатории",0,2,.05f,.8f);
            V("wallSmoothness","Блик стен",0,1,.01f,.08f);V("floorSmoothness","Блик пола",0,1,.01f,.42f);V("metalSmoothness","Блик металла",0,1,.01f,.72f);
            V("wallMetallic","Металличность стен",0,1,.01f,0);V("floorMetallic","Металличность пола",0,1,.01f,.05f);V("metalMetallic","Металличность металла",0,1,.01f,.75f);
            V("bloomIntensity","Максимальная сила свечения",0,1,.01f,.3f);V("bloomThreshold","Порог свечения",1,4,.05f,1.15f);V("bloomScatter","Ширина свечения",0,1,.01f,.35f);
            p.Add(NativeVisualPreferences.ExposurePath,"visual-polish","Экспозиция","Общая экспозиция цветокоррекции; 0 — нейтральная. Пользовательский выбор сохраняется отдельно от Lab.","EV",-1,1,.1f,0);
            p.Add(NativeVisualPreferences.SaturationPath,"visual-polish","Насыщенность","100% сохраняет насыщенность исходной палитры; применяется пропорционально силе цветокоррекции.","%",50,150,5,100);
            V("ColorGradingShadowLift","Подъём теней",0,.12f,.005f,.08f);
            V("ColorGradingHighlightCompression","Смягчение белого",0,.15f,.005f,.08f);
            V("reflectionIntensity","Максимальная сила отражения",0,1,.05f,.7f);
        }
    }
}
