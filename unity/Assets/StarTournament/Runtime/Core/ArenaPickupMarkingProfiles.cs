namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        static void AddPickupMarkings(ProvingProfile p,string prefix,float radius,float maximum,float height)
        {
            p.Add(prefix+"pickupRingRadius","pickup-markings","Радиус круга предмета","Постоянная белая линия под каждым оружием и бонусом; не меняет радиус подбора.","m",.5f,maximum,.01f,radius);
            p.Add(prefix+"pickupRingWidth","pickup-markings","Толщина круга предмета","Ширина белой напольной линии в стиле Combat Bowl.","m",.02f,.15f,.01f,.06f);
            p.Add(prefix+"pickupRingHeight","pickup-markings","Высота краски над полом","Отступ от authored опоры против мерцания; лунная пыль учитывается дополнительно.","m",.004f,.04f,.002f,height);
        }
        internal ProvingProfile BeforeUniformPickupRadii()
        {
            bool bowl=(Id=="orbital-league-v1"||Id=="orbital-league-ring-v1")&&Version>=11;
            bool tunnels=Id==TunnelsArtId&&Version>=4,lunar=Id=="lunar-laboratory-presentation-v1"&&Version>=6;
            if(!bowl&&!tunnels&&!lunar)return this;
            var previous=DetachedCopy();previous.version=bowl?10:tunnels?3:5;
            string path=bowl?"detail.ringRadius":tunnels?"tunnels.pickupRingRadius":"lunar.pickupRingRadius";
            var descriptor=previous.FindDescriptor(path);descriptor.DefaultValue=lunar?.78f:1.65f;
            if(lunar)descriptor.Maximum=1.2f;
            previous.Set(path,descriptor.DefaultValue);return previous;
        }
        internal static float UpgradeUniformPickupRadius(string path,int previousVersion,float value,NumericDescriptor current)
        {
            bool bowl=path=="detail.ringRadius"&&previousVersion<11;
            bool tunnels=path=="tunnels.pickupRingRadius"&&previousVersion<4;
            bool lunar=path=="lunar.pickupRingRadius"&&previousVersion<6;
            return (bowl||tunnels||lunar)&&UnityEngine.Mathf.Approximately(value,lunar?.78f:1.65f)?current.DefaultValue:value;
        }
        internal ProvingProfile BeforeArenaPickupMarkings()
        {
            bool tunnels=Id==TunnelsArtId&&Version>=3,lunar=Id=="lunar-laboratory-presentation-v1"&&Version>=5;
            if(!tunnels&&!lunar)return this;
            var previous=BeforeUniformPickupRadii().DetachedCopy();previous.version=tunnels?2:4;string prefix=tunnels?"tunnels.":"lunar.";
            foreach(var key in new[]{"pickupRingRadius","pickupRingWidth","pickupRingHeight"})
            {previous.descriptors.RemoveAll(d=>d.Path==prefix+key);previous.values.RemoveAll(v=>v.Path==prefix+key);}
            return previous;
        }
    }
}
