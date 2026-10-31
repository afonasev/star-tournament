using System;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public const string RifleDisplayPrefix="presentation.rifleDisplay.";
        void AddRifleDisplayDescriptors()
        {
            void F(string key,string label,string description,string unit,float min,float max,float step,float value)
                =>Add(RifleDisplayPrefix+key,"cobalt-display-v1",label,description,unit,min,max,step,value);
            F("width","COBALT · длина дисплея","Длина стекла вдоль верхнего скоса корпуса.","asset-meters",.12f,.42f,.001f,.30f);
            F("height","COBALT · высота дисплея","Высота стекла со шкалой и зелёными цифрами.","asset-meters",.025f,.09f,.001f,.052f);
            F("centerX","COBALT · смещение вбок","Положение модуля на левом скосе ствольной коробки.","asset-meters",-.10f,-.035f,.001f,-.066f);
            F("centerY","COBALT · высота центра","Высота центра дисплея над рукоятью.","asset-meters",.04f,.16f,.001f,.105f);
            F("centerZ","COBALT · положение вдоль корпуса","Положение центра между задником и цевьём.","asset-meters",-.22f,.15f,.001f,-.04f);
            F("tilt","COBALT · наклон стекла","Наклон лицевой поверхности к глазам игрока.","degrees",15,75,1,45);
            F("intensity","COBALT · яркость","Яркость цифр и шкалы без источника света.","ratio",.2f,2,.05f,1);
            F("drainSeconds","COBALT · затухание расхода","Время плавного уменьшения шкалы после выстрела.","seconds",.03f,.4f,.01f,.12f);
            F("fillSeconds","COBALT · заполнение","Время заполнения шкалы при пополнении боезапаса.","seconds",.1f,1,.01f,.35f);
            F("pulseGain","COBALT · отклик цифр","Краткое усиление яркости цифр при изменении запаса.","ratio",0,.5f,.01f,.15f);
        }
        internal ProvingProfile BeforeRifleDisplay()
        {
            if(id!=DefaultId||Descriptor(RifleDisplayPrefix+"width")==null)return this;
            var copy=DetachedCopy();
            copy.descriptors.RemoveAll(d=>d.Path.StartsWith(RifleDisplayPrefix,StringComparison.Ordinal));
            copy.values.RemoveAll(v=>v.Path.StartsWith(RifleDisplayPrefix,StringComparison.Ordinal));return copy;
        }
    }
}
