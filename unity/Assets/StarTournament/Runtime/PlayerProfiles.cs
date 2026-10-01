using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    [Serializable]
    public sealed class PlayerProfileRecord
    {
        public string Id;
        public string Name;
        public float MouseDegreesPerPixel;
        public bool ShowFps;
        public int GamepadLookVersion;
        public float GamepadHorizontal, GamepadVertical;
        public bool GamepadAutoLevel;
    }

    [Serializable]
    sealed class PlayerProfileDocument
    {
        public int Version = 1;
        public List<PlayerProfileRecord> Profiles = new List<PlayerProfileRecord>();
    }

    // Shell identity data only. No profile ID or InputDevice enters a match snapshot.
    public sealed class PlayerProfileCatalog
    {
        readonly string path;
        readonly PlayerProfileDocument document;
        public IReadOnlyList<PlayerProfileRecord> Profiles => document.Profiles;
        public PlayerProfileCatalog(string path)
        {
            this.path = path ?? throw new ArgumentNullException(nameof(path));
            document = File.Exists(path) ? JsonUtility.FromJson<PlayerProfileDocument>(File.ReadAllText(path)) : new PlayerProfileDocument();
            if(document == null || document.Version != 1 || document.Profiles == null ||
                document.Profiles.Any(p => p == null || string.IsNullOrEmpty(p.Id) || string.IsNullOrWhiteSpace(p.Name)) ||
                document.Profiles.Select(p => p.Id).Distinct().Count() != document.Profiles.Count)
                throw new InvalidDataException("Invalid player profiles document");
        }
        public PlayerProfileRecord Find(string id) => document.Profiles.FirstOrDefault(p => p.Id == id);
        public PlayerProfileRecord Create(string name, float mouseSensitivity, bool showFps)
        {
            name = CleanName(name);
            var profile = new PlayerProfileRecord { Id = Guid.NewGuid().ToString("N"), Name = name,
                MouseDegreesPerPixel = mouseSensitivity, ShowFps = showFps };
            document.Profiles.Add(profile); Save(); return profile;
        }
        public void Rename(string id, string name)
        {
            var profile = Find(id) ?? throw new ArgumentException("Unknown profile", nameof(id));
            profile.Name = CleanName(name); Save();
        }
        public bool Delete(string id)
        {
            var profile = Find(id); if(profile == null) return false;
            document.Profiles.Remove(profile); Save(); return true;
        }
        public void SetPersonal(string id, float mouseSensitivity, bool showFps)
        {
            var profile = Find(id) ?? throw new ArgumentException("Unknown profile", nameof(id));
            profile.MouseDegreesPerPixel = mouseSensitivity; profile.ShowFps = showFps; Save();
        }
        public void SetGamepad(string id, GamepadLookSettings settings)
        {
            var record=Find(id) ?? throw new ArgumentException("Unknown profile", nameof(id));
            record.GamepadLookVersion=1;record.GamepadHorizontal=settings.Horizontal;
            record.GamepadVertical=settings.Vertical;record.GamepadAutoLevel=settings.AutoLevel;Save();
        }
        static string CleanName(string name)
        {
            name = (name ?? "").Trim();
            if(name.Length == 0 || name.Length > 24) throw new ArgumentException("Profile name must contain 1–24 characters", nameof(name));
            return name;
        }
        void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonUtility.ToJson(document, true));
                if(File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if(File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    public sealed class LocalIdentitySession
    {
        public sealed class Guest
        {
            public readonly string Id = Guid.NewGuid().ToString("N");
            public float MouseDegreesPerPixel;
            public bool ShowFps;
            public GamepadLookSettings? GamepadLook;
        }
        readonly string[] seatProfile = new string[SeatInputCoordinator.SeatCount];
        readonly Guest[] seatGuest = new Guest[SeatInputCoordinator.SeatCount];
        readonly Dictionary<int,string> preferredProfileByDevice = new Dictionary<int,string>();
        readonly Dictionary<int,Guest> guestByDevice = new Dictionary<int,Guest>();
        public string ProfileId(int seat) => seatProfile[seat];
        public Guest GuestAt(int seat) => seatGuest[seat];
        public bool HasIdentity(int seat) => seatProfile[seat] != null || seatGuest[seat] != null;
        public bool InUse(string id, int exceptSeat = -1)
        {
            for(int seat=0;seat<seatProfile.Length;seat++) if(seat!=exceptSeat && seatProfile[seat]==id) return true;
            return false;
        }
        public void ClearSeat(int seat) { seatProfile[seat]=null; seatGuest[seat]=null; }
        public void RemoveSeat(int seat,int count)
        {
            for(int i=seat;i<count-1;i++){seatProfile[i]=seatProfile[i+1];seatGuest[i]=seatGuest[i+1];}
            ClearSeat(count-1);
        }
        public void RememberBinding(int seat,int deviceId)
        {
            if(seatProfile[seat]!=null)preferredProfileByDevice[deviceId]=seatProfile[seat];
            else if(seatGuest[seat]!=null){guestByDevice[deviceId]=seatGuest[seat];preferredProfileByDevice.Remove(deviceId);}
        }
        public void ForgetProfile(string id)
        {
            for(int i=0;i<seatProfile.Length;i++) if(seatProfile[i]==id) ClearSeat(i);
            foreach(var key in preferredProfileByDevice.Where(pair=>pair.Value==id).Select(pair=>pair.Key).ToArray()) preferredProfileByDevice.Remove(key);
        }
        public bool TryRestore(int seat, int deviceId, PlayerProfileCatalog catalog)
        {
            if(preferredProfileByDevice.TryGetValue(deviceId,out var id) && catalog.Find(id)!=null && !InUse(id,seat))
                return ChooseProfile(seat,deviceId,id,catalog);
            return false;
        }
        public bool ChooseProfile(int seat, int deviceId, string id, PlayerProfileCatalog catalog)
        {
            if(catalog.Find(id)==null || InUse(id,seat)) return false;
            seatProfile[seat]=id; seatGuest[seat]=null; preferredProfileByDevice[deviceId]=id; return true;
        }
        public Guest ChooseGuest(int seat, int deviceId, float defaultSensitivity, bool defaultShowFps)
        {
            if(!guestByDevice.TryGetValue(deviceId,out var guest))
            {
                guest = new Guest { MouseDegreesPerPixel = defaultSensitivity, ShowFps = defaultShowFps };
                guestByDevice[deviceId]=guest;
            }
            seatProfile[seat]=null; seatGuest[seat]=guest; preferredProfileByDevice.Remove(deviceId); return guest;
        }
    }
}
