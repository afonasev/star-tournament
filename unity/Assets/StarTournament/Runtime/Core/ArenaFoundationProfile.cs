namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public static ProvingProfile CreateArenaFamiliesDefault(int family = 0)
        {
            var p=new ProvingProfile{id="unity-arena-families-v1",version=1};
            p.Add("arena.family","arena-family","Spatial family","Explicit family ordinal: 0 wide hall, 1 gallery, 2 balconies, 3 stacked combat, 4 basement.","count",0,4,1,family);
            p.Add("fixture.width","arena-foundation","Arena width","Width shared by the ARENA-2 family catalog.","meters",12,80,1,32);p.Add("fixture.depth","arena-foundation","Arena depth","Depth shared by the ARENA-2 family catalog.","meters",12,80,1,32);p.Add("fixture.wallThickness","arena-foundation","Wall thickness","Physical wall thickness.","meters",.1f,2,.05f,.3f);p.Add("fixture.wallHeight","arena-foundation","Wall height","Height above each support.","meters",1.5f,8,.1f,3);p.Add("fixture.floorThickness","arena-foundation","Floor thickness","Physical floor thickness.","meters",.1f,2,.05f,.3f);p.Add("fixture.floorHeight","arena-foundation","Lower floor height","Lower support world height.","meters",-5,5,.1f,0);p.Add("fixture.upperFloorHeight","arena-foundation","Upper floor height","Upper support world height.","meters",2,10,.1f,4);p.Add("fixture.stairRise","arena-foundation","Stair rise","Height per generated stair.","meters",.05f,.5f,.01f,.25f);p.Add("fixture.stairTread","arena-foundation","Stair tread","Depth per generated stair.","meters",.1f,1,.01f,.75f);p.Add("fixture.rampLength","arena-foundation","Ramp length","Horizontal ramp run.","meters",3,20,.5f,12);p.Add("fixture.rampWidth","arena-foundation","Ramp width","Clear ramp and stair width.","meters",1.2f,8,.1f,4);p.Add("fixture.headroomHeight","arena-foundation","Headroom probe","Current foundation headroom diagnostic height.","meters",1,5,.1f,2.1f);return p;
        }

        public static ProvingProfile CreateArenaFoundationDefault() => CreateArenaFamiliesDefault();
    }
}
