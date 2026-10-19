using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    [Serializable] public sealed class LabCompatibilitySource
    {public string ProfileId,SourceHash;public int Number,SourceNumber;}
    [Serializable] public sealed class LabParameterEnvelope
    {
        public int Schema=3;public string Integrity;
        public LabParameterFile History;
        public List<LabCompatibilitySource> Views=new List<LabCompatibilitySource>();
    }
    public sealed partial class DesignLabHistory
    {
        static string CompatibilityIntegrity(LabParameterEnvelope file)
        {
            string saved=file.Integrity;file.Integrity=null;
            try{using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(file)))).Replace("-","").ToLowerInvariant();}
            finally{file.Integrity=saved;}
        }
        static string WriteCompatibilityEnvelope(LabParameterFile compact,LabHistoryFile source)
        {
            var views=source.Profiles.SelectMany(p=>p.Revisions.Where(r=>r.CompatibilitySourceNumber>0).Select(r=>new LabCompatibilitySource{
                ProfileId=p.Id,Number=r.Number,SourceNumber=r.CompatibilitySourceNumber,SourceHash=r.CompatibilitySourceHash})).ToList();
            if(views.Count==0)return JsonUtility.ToJson(compact);
            var file=new LabParameterEnvelope{History=compact,Views=views};file.Integrity=CompatibilityIntegrity(file);return JsonUtility.ToJson(file);
        }
        LabHistoryFile ReadCompatibilityEnvelope(string json)
        {
            var envelope=JsonUtility.FromJson<LabParameterEnvelope>(json);
            if(envelope?.Schema!=3||envelope.History?.Schema!=2||envelope.Views==null||envelope.Views.Count==0
                ||string.IsNullOrEmpty(envelope.Integrity)||envelope.Integrity!=CompatibilityIntegrity(envelope))
                throw new InvalidDataException("Compatibility history integrity mismatch");
            var restored=ReadStorage(JsonUtility.ToJson(envelope.History));
            var identities=new HashSet<string>(StringComparer.Ordinal);
            foreach(var view in envelope.Views)
            {
                if(view==null||string.IsNullOrEmpty(view.ProfileId)||view.Number<1||view.SourceNumber<1||string.IsNullOrEmpty(view.SourceHash)
                    ||!identities.Add(view.ProfileId+"/"+view.Number))throw new InvalidDataException("Invalid compatibility source metadata");
                var revision=restored.Profiles.SingleOrDefault(p=>p.Id==view.ProfileId)?.Revisions.SingleOrDefault(r=>r.Number==view.Number);
                if(revision==null)throw new InvalidDataException("Missing compatibility view");
                revision.CompatibilitySourceNumber=view.SourceNumber;revision.CompatibilitySourceHash=view.SourceHash;
            }
            return restored;
        }
        static void ValidateCompatibilitySource(LabHistoryProfile profile,LabRevision view)
        {
            if(view.CompatibilitySourceNumber==0&&string.IsNullOrEmpty(view.CompatibilitySourceHash))return;
            var source=profile.Revisions.SingleOrDefault(r=>r.Number==view.CompatibilitySourceNumber);
            if(view.CompatibilitySourceNumber<1||source==null||source==view||source.CompatibilitySourceNumber!=0
                ||source.Hash!=view.CompatibilitySourceHash||view.ReleaseCandidate)
                throw new InvalidDataException("Invalid compatibility source reference");
            if(profile.Revisions.Count(r=>r.CompatibilitySourceNumber==view.CompatibilitySourceNumber)>1)
                throw new InvalidDataException("Duplicate compatibility source reference");
        }
    }
}
