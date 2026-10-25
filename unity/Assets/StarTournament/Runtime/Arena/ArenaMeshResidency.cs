using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    internal interface IArenaMeshOwner
    {
        IEnumerable<Mesh> ReadOwnedMeshes();
        IEnumerable<Mesh> ReadDiagnosticMeshes();
        void ReleaseUnusedMeshes(HashSet<int> referencedInstanceIds);
    }

    /// <summary>Releases generated intermediate meshes after all arena combines have completed.</summary>
    public static class ArenaMeshResidency
    {
        // Public for focused matched-control diagnostics; production defaults to pruning.
        public static bool PruningEnabled { get; set; } = true;

        public static Mesh[] ReadUnreferencedOwnedMeshes(Transform arena)
        {
            if(!arena) return System.Array.Empty<Mesh>();
            var referenced=ReferencedMeshIds(arena);
            return arena.GetComponentsInChildren<MonoBehaviour>(true)
                .OfType<IArenaMeshOwner>()
                .SelectMany(owner=>owner.ReadOwnedMeshes())
                .Where(mesh=>mesh&&!referenced.Contains(mesh.GetInstanceID()))
                .GroupBy(mesh=>mesh.GetInstanceID()).Select(group=>group.First()).ToArray();
        }

        public static Mesh[] ReadInclusiveOwnedMeshes(Transform arena)
        {
            if(!arena)return System.Array.Empty<Mesh>();
            var meshes=new Dictionary<int,Mesh>();
            foreach(var filter in arena.GetComponentsInChildren<MeshFilter>(true))if(filter.sharedMesh)meshes[filter.sharedMesh.GetInstanceID()]=filter.sharedMesh;
            foreach(var renderer in arena.GetComponentsInChildren<MeshRenderer>(true))if(renderer.additionalVertexStreams)meshes[renderer.additionalVertexStreams.GetInstanceID()]=renderer.additionalVertexStreams;
            foreach(var collider in arena.GetComponentsInChildren<MeshCollider>(true))if(collider.sharedMesh)meshes[collider.sharedMesh.GetInstanceID()]=collider.sharedMesh;
            foreach(var renderer in arena.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(renderer.sharedMesh)meshes[renderer.sharedMesh.GetInstanceID()]=renderer.sharedMesh;
            foreach(var optimizer in arena.GetComponentsInChildren<ArenaRenderChunks>(true))
                foreach(var mesh in optimizer.ReadOwnedMeshes())if(mesh)meshes[mesh.GetInstanceID()]=mesh;
            foreach(var owner in arena.GetComponentsInChildren<MonoBehaviour>(true).OfType<IArenaMeshOwner>())
                foreach(var mesh in owner.ReadOwnedMeshes())if(mesh)meshes[mesh.GetInstanceID()]=mesh;
            return meshes.Values.ToArray();
        }

        public static int ReleaseUnreferencedOwnedMeshes(Transform arena)
        {
            if(!arena||!PruningEnabled) return 0;
            var referenced=ReferencedMeshIds(arena);
            var owners=arena.GetComponentsInChildren<MonoBehaviour>(true).OfType<IArenaMeshOwner>().ToArray();
            var unreferenced=owners.SelectMany(owner=>owner.ReadOwnedMeshes())
                .Where(mesh=>mesh&&!referenced.Contains(mesh.GetInstanceID()))
                .GroupBy(mesh=>mesh.GetInstanceID()).Select(group=>group.First()).ToArray();
            foreach(var owner in owners) owner.ReleaseUnusedMeshes(referenced);
            if(unreferenced.Length>0)
            {
                foreach(var mesh in unreferenced)if(mesh)
                {
                    if(Application.isPlaying)Object.Destroy(mesh);
                    else Object.DestroyImmediate(mesh);
                }
            }
            return unreferenced.Length;
        }

        static HashSet<int> ReferencedMeshIds(Transform arena)
        {
            var ids=new HashSet<int>();
            foreach(var filter in arena.GetComponentsInChildren<MeshFilter>(true))
                if(filter.sharedMesh) ids.Add(filter.sharedMesh.GetInstanceID());
            foreach(var renderer in arena.GetComponentsInChildren<MeshRenderer>(true))
                if(renderer.additionalVertexStreams) ids.Add(renderer.additionalVertexStreams.GetInstanceID());
            foreach(var collider in arena.GetComponentsInChildren<MeshCollider>(true))
                if(collider.sharedMesh) ids.Add(collider.sharedMesh.GetInstanceID());
            foreach(var renderer in arena.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if(renderer.sharedMesh) ids.Add(renderer.sharedMesh.GetInstanceID());
            foreach(var optimizer in arena.GetComponentsInChildren<ArenaRenderChunks>(true))
                foreach(var mesh in optimizer.ReadOwnedMeshes())if(mesh)ids.Add(mesh.GetInstanceID());
            foreach(var owner in arena.GetComponentsInChildren<MonoBehaviour>(true).OfType<IArenaMeshOwner>())
                foreach(var mesh in owner.ReadDiagnosticMeshes())if(mesh)ids.Add(mesh.GetInstanceID());
            return ids;
        }

        internal static IEnumerable<Mesh> OwnedMeshes(IEnumerable<Object> assets) => assets.OfType<Mesh>().Where(mesh=>mesh);

        internal static void ReleaseUnused(IList<Object> assets,HashSet<int> referencedInstanceIds)
        {
            foreach(var mesh in OwnedMeshes(assets).Where(mesh=>!referencedInstanceIds.Contains(mesh.GetInstanceID())).ToArray())
                assets.Remove(mesh);
        }
    }
}
