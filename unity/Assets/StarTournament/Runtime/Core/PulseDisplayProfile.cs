using System;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public const string PulseDisplayPrefix="presentation.pulseDisplay.";
        void AddPulseDisplayDescriptors()
        {
            void F(string key,string label,string description,string unit,float min,float max,float step,float value)
                =>Add(PulseDisplayPrefix+key,"pulse-display-v1",label,description,unit,min,max,step,value);
            F("diameter","Счётчик · диаметр","Диаметр чёрной панели внутри заднего обода Pulse до масштаба модели.","asset-meters",.10f,.148f,.001f,.144f);
            F("centerY","Счётчик · высота центра","Центр круглой панели относительно исходной модели Pulse.","asset-meters",.075f,.085f,.001f,.08f);
            F("rearZ","Счётчик · задняя плоскость","Лицевая плоскость перед утопленным торцом; отрицательное направление к владельцу.","asset-meters",-.222f,-.215f,.001f,-.216f);
            F("intensity","Счётчик · яркость цифр","Яркость тёплых цифр, знака ракеты и красного нуля без источника света.","ratio",.2f,2,.05f,1);
            F("arcIntensity","Счётчик · яркость дуги","Яркость янтарных сегментов доли оставшегося профильного запаса.","ratio",.1f,1.5f,.05f,.8f);
            F("digitScale","Счётчик · размер цифр","Масштаб числа внутри круглой панели; значения выше 99 автоматически ужимаются.","ratio",.75f,1.05f,.01f,1);
        }
        internal ProvingProfile BeforePulseDisplay()
        {
            if(id!=DefaultId||Descriptor(PulseDisplayPrefix+"diameter")==null)return this;
            var copy=DetachedCopy();
            copy.descriptors.RemoveAll(d=>d.Path.StartsWith(PulseDisplayPrefix,StringComparison.Ordinal));
            copy.values.RemoveAll(v=>v.Path.StartsWith(PulseDisplayPrefix,StringComparison.Ordinal));return copy;
        }
    }
}
