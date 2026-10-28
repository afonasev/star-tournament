using System;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public const string ForgeDisplayPrefix="presentation.forgeDisplay.";
        void AddForgeDisplayDescriptors()
        {
            void F(string key,string label,string description,string unit,float min,float max,float step,float value)
                =>Add(ForgeDisplayPrefix+key,"forge-display-v1",label,description,unit,min,max,step,value);
            F("radius","Кольцо · радиус","Внешний радиус индикатора на заднем ободе FORGE до масштаба модели.","asset-meters",.13f,.16f,.001f,.148f);
            F("centerY","Кольцо · высота","Центр индикатора относительно исходной модели FORGE.","asset-meters",.19f,.22f,.001f,.205f);
            F("rearZ","Индикатор · глубина","Плоскость перед задним ободом; отрицательное направление к владельцу.","asset-meters",-.26f,-.243f,.001f,-.244f);
            F("width","Число · ширина панели","Ширина нижней панели с числом; без дополнительной шкалы.","asset-meters",.08f,.16f,.001f,.12f);
            F("height","Число · высота панели","Высота фасеточной нижней панели.","asset-meters",.04f,.085f,.001f,.064f);
            F("offsetY","Число · смещение вниз","Расстояние от центра реактора до центра нижней панели.","asset-meters",-.19f,-.12f,.001f,-.151f);
            F("intensity","Индикатор · яркость","Яркость цифр и сегментов без дополнительного источника света.","ratio",.2f,2,.05f,1);
            F("lowFraction","Индикатор · малый запас","Доля полного запаса, ниже которой число и сегменты янтарные.","ratio",.05f,.4f,.01f,.15f);
        }
        internal ProvingProfile BeforeForgeDisplay()
        {
            if(id!=DefaultId||Descriptor(ForgeDisplayPrefix+"radius")==null)return this;
            var copy=DetachedCopy();
            copy.descriptors.RemoveAll(d=>d.Path.StartsWith(ForgeDisplayPrefix,StringComparison.Ordinal));
            copy.values.RemoveAll(v=>v.Path.StartsWith(ForgeDisplayPrefix,StringComparison.Ordinal));return copy;
        }
    }
}
