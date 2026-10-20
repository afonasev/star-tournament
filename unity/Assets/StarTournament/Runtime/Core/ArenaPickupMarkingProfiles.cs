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
        internal ProvingProfile BeforeArenaPickupMarkings()
        {
            bool tunnels=Id==TunnelsArtId&&Version>=3,lunar=Id=="lunar-laboratory-presentation-v1"&&Version>=5;
            if(!tunnels&&!lunar)return this;
            var previous=DetachedCopy();previous.version=tunnels?2:4;string prefix=tunnels?"tunnels.":"lunar.";
            foreach(var key in new[]{"pickupRingRadius","pickupRingWidth","pickupRingHeight"})
            {previous.descriptors.RemoveAll(d=>d.Path==prefix+key);previous.values.RemoveAll(v=>v.Path==prefix+key);}
            return previous;
        }
    }
}
