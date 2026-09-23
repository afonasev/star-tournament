using System.IO;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests
{
    // Immutable authored test data, excluded from the Player catalog. No builders, seeds or profiles generate geometry.
    public static class AuthoredPhysicsFixture
    {
        static readonly System.Collections.Generic.Dictionary<string,float> Values=new System.Collections.Generic.Dictionary<string,float>{{"fixture.width",32.0f},{"fixture.depth",32.0f},{"fixture.wallThickness",0.3f},{"fixture.wallHeight",3.0f},{"fixture.floorThickness",0.3f},{"fixture.floorHeight",0.0f},{"fixture.upperFloorHeight",4.0f},{"fixture.stairRise",0.25f},{"fixture.stairTread",0.75f},{"fixture.rampLength",12.0f},{"fixture.rampWidth",4.0f},{"fixture.headroomHeight",2.1f}};
        public static float Value(string key)=>Values[key];
        public static ArenaFreezeSnapshot Freeze()=>ArenaFreezeSnapshot.Create(JsonUtility.FromJson<ArenaDefinition>(File.ReadAllText(Path.Combine(Application.dataPath,"StarTournament/Tests/PlayMode/Fixtures/physics-contract-v1.json"))),ProvingProfile.CreateDefault());
    }
}
