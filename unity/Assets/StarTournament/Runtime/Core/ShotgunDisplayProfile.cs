using System;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public const string ShotgunDisplayPrefix="presentation.shotgunDisplay.";
        void AddShotgunDisplayDescriptors()
        {
            void F(string key,string label,string description,string unit,float min,float max,float step,float value)
                =>Add(ShotgunDisplayPrefix+key,"shotgun-display-v1",label,description,unit,min,max,step,value);
            F("width","Дисплей · ширина","Ширина экрана на задней панели Vector до штатного масштаба модели.","asset-meters",.08f,.19f,.001f,.152f);
            F("height","Дисплей · высота","Высота экрана на задней панели Vector до штатного масштаба модели.","asset-meters",.08f,.19f,.001f,.155f);
            F("centerY","Дисплей · высота центра","Положение центра экрана по вертикали задней панели.","asset-meters",.24f,.34f,.001f,.318f);
            F("rearZ","Дисплей · задняя плоскость","Глубина лицевой плоскости относительно исходного mesh Vector; отрицательное направление к владельцу.","asset-meters",-.26f,-.234f,.001f,-.239f);
            F("intensity","Дисплей · яркость","Яркость светло-голубых цифр и подчёркивания без дополнительного источника света.","ratio",.2f,2,.05f,1);
        }
        internal ProvingProfile BeforeShotgunDisplay()
        {
            if(id!=DefaultId||Descriptor(ShotgunDisplayPrefix+"width")==null)return this;
            var copy=DetachedCopy();
            copy.descriptors.RemoveAll(d=>d.Path.StartsWith(ShotgunDisplayPrefix,StringComparison.Ordinal));
            copy.values.RemoveAll(v=>v.Path.StartsWith(ShotgunDisplayPrefix,StringComparison.Ordinal));return copy;
        }
    }
}
