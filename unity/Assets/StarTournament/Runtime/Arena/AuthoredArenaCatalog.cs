using System;
namespace StarTournament.ProvingGround
{
    public static class AuthoredArenaCatalog
    {
        public static string Next(string id)=>id==CombatBowlCatalog.Id?IndustrialTunnelsCatalog.Id:id==IndustrialTunnelsCatalog.Id?LunarLaboratoryCatalog.Id:id==LunarLaboratoryCatalog.Id?WellArenaCatalog.Id:id==WellArenaCatalog.Id?FiringRangeCatalog.Id:CombatBowlCatalog.Id;
        public static string Name(string id)=>id==FiringRangeCatalog.Id?"Полигон":id==WellArenaCatalog.Id?"Станция Вега":id==LunarLaboratoryCatalog.Id?"Луна-9":id==CombatBowlCatalog.Id?"Орбита":id==IndustrialTunnelsCatalog.Id?"Тоннели":throw new ArgumentException("Unknown map "+id);
        public static int Maximum(string id)=>id==FiringRangeCatalog.Id?4:id==WellArenaCatalog.Id?8:id==LunarLaboratoryCatalog.Id?8:id==CombatBowlCatalog.Id?8:id==IndustrialTunnelsCatalog.Id?4:throw new ArgumentException("Unknown map "+id);
        public static bool Supports(string id,int count)=>count>=(id==FiringRangeCatalog.Id?1:2)&&count<=Maximum(id);
        public static ArenaDefinition Resolve(string id,string revision=null)
        {
            if(id==FiringRangeCatalog.Id&&(revision==null||revision==FiringRangeCatalog.Revision))return FiringRangeCatalog.Build();
            if(id==CombatBowlCatalog.Id&&revision=="19")return Previous(CombatBowlCatalog.Build(true),revision);
            if(id==LunarLaboratoryCatalog.Id&&revision=="8")return Previous(LunarLaboratoryCatalog.Draft(previousForgePlacement:true),revision);
            if(id==WellArenaCatalog.Id&&revision=="8")return WellArenaCatalog.Build(previousDensity:true);
            if(id==WellArenaCatalog.Id&&revision=="7")return WellArenaCatalog.Build(previousWorkZones:true);
            if(id==WellArenaCatalog.Id&&revision=="6")return Previous(WellArenaCatalog.Build(true),revision);
            if(id==WellArenaCatalog.Id&&(revision==null||revision==WellArenaCatalog.Revision))return WellArenaCatalog.Build();
            if(id==WellArenaCatalog.Id&&revision==WellArenaRevisionFour.Revision)return WellArenaRevisionFour.Build();
            if(id==WellArenaCatalog.Id&&revision==WellArenaRevisionThree.Revision)return WellArenaRevisionThree.Build();
            if(id==LunarLaboratoryCatalog.Id&&(revision==null||revision==LunarLaboratoryCatalog.Revision))return LunarLaboratoryCatalog.Build();
            if(id==CombatBowlCatalog.Id)return CombatBowlCatalog.Resolve(id,revision??CombatBowlCatalog.Revision);
            if(id==IndustrialTunnelsCatalog.Id&&(revision==null||revision==IndustrialTunnelsCatalog.Revision))return IndustrialTunnelsCatalog.Build();
            throw new ArgumentException("Unknown authored map "+id+"@"+revision);
        }
        static ArenaDefinition Previous(ArenaDefinition definition,string revision)
        {definition.Revision=revision;definition.Identity=definition.MapId+"@"+revision;definition.ProfileFingerprint="authored:"+definition.Identity;return definition;}
        public static ArenaFreezeSnapshot Freeze(string id,ProvingProfile profile)=>ArenaFreezeSnapshot.Create(Resolve(id),profile);
    }
}
