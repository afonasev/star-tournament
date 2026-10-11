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
        static void VisualPolishDescriptors(ProvingProfile p)
        {
            foreach(VisualEffect effect in System.Enum.GetValues(typeof(VisualEffect)))
                p.Add(NativeVisualPreferences.Path(effect),"visual-polish",NativeVisualPreferences.Names[(int)effect],"Сила улучшения графики: 0 — исходный вид; пользовательский выбор сохраняется отдельно от Lab.","%",0,100,effect==VisualEffect.AmbientOcclusion?25:5,50);
            void V(string path,string label,float min,float max,float step,float value)=>p.Add("visual."+path,"visual-polish",label,"Presentation-only художественный параметр; применяется при подготовке арены.","ratio",min,max,step,value);
            V("fillScale","Фоновая заливка",.4f,1,.05f,.75f);V("keyScale","Основной свет",1,2,.05f,1.25f);
            V("wallSmoothness","Блик стен",0,1,.01f,.2f);V("floorSmoothness","Блик пола",0,1,.01f,.35f);V("metalSmoothness","Блик металла",0,1,.01f,.55f);
            V("wallMetallic","Металличность стен",0,1,.01f,.05f);V("floorMetallic","Металличность пола",0,1,.01f,.1f);V("metalMetallic","Металличность металла",0,1,.01f,.65f);
            V("bloomIntensity","Максимальная сила свечения",0,1,.01f,.3f);V("bloomThreshold","Порог свечения",1,4,.05f,1.15f);V("bloomScatter","Ширина свечения",0,1,.01f,.35f);
            p.Add(NativeVisualPreferences.ExposurePath,"visual-polish","Экспозиция","Общая экспозиция цветокоррекции; 0 — нейтральная. Пользовательский выбор сохраняется отдельно от Lab.","EV",-1,1,.1f,0);
            p.Add(NativeVisualPreferences.SaturationPath,"visual-polish","Насыщенность","100% сохраняет насыщенность исходной палитры; применяется пропорционально силе цветокоррекции.","%",50,150,5,100);
            V("ColorGradingShadowLift","Подъём теней",0,.12f,.005f,.08f);
            V("ColorGradingHighlightCompression","Смягчение белого",0,.15f,.005f,.08f);
            V("reflectionIntensity","Максимальная сила отражения",0,1,.05f,.7f);
        }
    }
}
