using System;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        internal const string VectorPrecisionPrefix="presentation.vectorPrecision.";
        void AddVectorPrecisionDescriptors()
        {
            void F(string key,string label,string description,string unit,float min,float max,float step,float value)
                =>Add(VectorPrecisionPrefix+key,"vector-precision-v1",label,description,unit,min,max,step,value);
            F("flashScale","VECTOR · лепестки","Масштаб компактной двойной вспышки относительно размера muzzle; не влияет на разброс.","ratio",.5f,3,.05f,1.65f);
            F("flashRadiance","VECTOR · горячее ядро","Яркость только дульной вспышки; дробь остаётся несветящейся.","ratio",.5f,4,.1f,1.8f);
            F("plumeLength","VECTOR · длина факела","Продольная длина короткого объёмного факела от реального дула.","meters",.05f,.6f,.01f,.32f);
            F("plumeWidth","VECTOR · ширина факела","Ширина тёплых лепестков, без перекрытия центральной области прицела.","meters",.02f,.25f,.01f,.12f);
            F("smokeCount","VECTOR · слои дыма","Количество небольших неоднородных клубков на ствол.","count",1,5,1,3);
            F("smokeOpacity","VECTOR · пороховой дым","Непрозрачность остаточного дыма; центральная область прицела должна оставаться читаемой.","ratio",0,.4f,.01f,.17f);
            F("smokeGrowth","VECTOR · рассеивание","Относительное расширение дыма по мере рассеивания.","ratio",1,4,.1f,2.1f);
            F("sparkLength","VECTOR · длина искры","Вытяжка направленной искры относительно толщины.","ratio",1,10,.5f,5);
            F("traceLength","VECTOR · короткий след","Максимальная длина металлического следа; endpoint и время попадания не меняются.","meters",.05f,1,.01f,.42f);
            F("contactSparkScale","VECTOR · искры контакта","Размер направленных контактных искр относительно дульных.","ratio",1,4,.05f,2.25f);
            F("contactSparkSpeedScale","VECTOR · разлёт искр","Скорость контактных искр относительно дульных.","ratio",1,4,.1f,2);
            F("contactRadiance","VECTOR · яркость контакта","Яркость только контактных искр; дробь и сколы не светятся.","ratio",1,4,.1f,2);
            F("contactFlashSize","VECTOR · начало контакта","Размер краткого неоднородного бело-тёплого импульса на поверхности.","meters",.02f,.3f,.01f,.12f);
            F("contactFlashSeconds","VECTOR · импульс контакта","Короткий начальный импульс перед разлётом искр и сколов.","seconds",.01f,.1f,.005f,.035f);
            F("chipLifetimeScale","VECTOR · время сколов","Длительность сколов относительно базового contact lifetime.","ratio",1,3,.1f,2);
            F("dustLifetimeScale","VECTOR · время пыли","Длительность остаточной пыли относительно базового contact lifetime.","ratio",1,4,.1f,3);
            F("chipSpeed","VECTOR · скорость сколов","Начальная скорость декоративных фрагментов поверхности.","meters-per-second",.1f,4,.1f,1.2f);
            F("chipSize","VECTOR · размер сколов","Размер небольших угловатых фрагментов поверхности.","meters",.002f,.05f,.001f,.018f);
            F("debrisGravity","VECTOR · оседание частиц","Доля гравитации для декоративных искр и сколов; collision и gameplay отсутствуют.","ratio",0,1,.05f,.35f);
            F("dustOpacity","VECTOR · пыль контакта","Непрозрачность небольшой локальной пыли на архитектуре.","ratio",0,.5f,.01f,.4f);
            F("dustGrowth","VECTOR · расширение пыли","Относительное расширение пылевого облачка при затухании.","ratio",1,4,.1f,2);
        }
        internal ProvingProfile BeforeVectorPrecisionEffects()
        {
            if(id!=DefaultId||Descriptor(VectorPrecisionPrefix+"flashScale")==null)return this;
            var copy=DetachedCopy();
            copy.descriptors.RemoveAll(d=>d.Path.StartsWith(VectorPrecisionPrefix,StringComparison.Ordinal));
            copy.values.RemoveAll(v=>v.Path.StartsWith(VectorPrecisionPrefix,StringComparison.Ordinal));return copy;
        }
    }
}
