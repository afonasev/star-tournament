using System;
namespace StarTournament.ProvingGround
{
    public static class AuthoredArenaCatalog
    {
        public static string Next(string id)=>id==CombatBowlCatalog.Id?IndustrialTunnelsCatalog.Id:id==IndustrialTunnelsCatalog.Id?LunarLaboratoryCatalog.Id:CombatBowlCatalog.Id;
        public static string Name(string id)=>id==LunarLaboratoryCatalog.Id?"Лунная лаборатория":id==CombatBowlCatalog.Id?"Combat Bowl":id==IndustrialTunnelsCatalog.Id?"Технические тоннели":throw new ArgumentException("Unknown map "+id);
        public static int Maximum(string id)=>id==LunarLaboratoryCatalog.Id?8:id==CombatBowlCatalog.Id?8:id==IndustrialTunnelsCatalog.Id?4:throw new ArgumentException("Unknown map "+id);
        public static bool Supports(string id,int count)=>count>=2&&count<=Maximum(id);
        public static ArenaDefinition Resolve(string id,string revision=null)
        {
            if(id==LunarLaboratoryCatalog.Id&&(revision==null||revision==LunarLaboratoryCatalog.Revision))return LunarLaboratoryCatalog.Build();
            if(id==CombatBowlCatalog.Id)return CombatBowlCatalog.Resolve(id,revision??CombatBowlCatalog.Revision);
            if(id==IndustrialTunnelsCatalog.Id&&(revision==null||revision==IndustrialTunnelsCatalog.Revision))return IndustrialTunnelsCatalog.Build();
            throw new ArgumentException("Unknown authored map "+id+"@"+revision);
        }
        public static ArenaFreezeSnapshot Freeze(string id,ProvingProfile profile)=>ArenaFreezeSnapshot.Create(Resolve(id),profile);
    }
}
