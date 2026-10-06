using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    public enum NativeBotDifficulty { Easy, Normal, Hard }

    // Filtered input: positions here have already passed the observation provider's FOV/LOS.
    [Serializable] public struct NativeBotSighting
    {
        public int Participant, Life;
        public Vector3 Position, Velocity;
        public float Health, Armor;
        // Observer-specific direct shot obstruction; optical visibility remains independent.
        public bool HasVitals, ShotBlocked;
        public NativeBotSighting(int participant, int life, Vector3 position)
        { Participant = participant; Life = life; Position = position; Velocity=Vector3.zero; Health=Armor=0; HasVitals=false; ShotBlocked=false; }
        public NativeBotSighting(int participant,int life,Vector3 position,float health,float armor):this(participant,life,position)
        {Health=health;Armor=armor;HasVitals=true;}
    }
    public sealed class NativeBotObservationFrame
    {
        public int OwnLife;
        public bool Alive;
        public NativeBotSighting[] Direct = Array.Empty<NativeBotSighting>();
    }
    [Serializable] public struct NativeBotMemoryEntry
    {
        public NativeBotSighting Sighting;
        public double ObservedAt;
        public int Source;
        public bool Visible;
    }
    [Serializable] public sealed class NativeBotKnowledge
    {
        public int OwnLife;
        public bool Alive;
        public NativeBotMemoryEntry[] Enemies = Array.Empty<NativeBotMemoryEntry>();
    }
    [Serializable] public struct NativeBotPendingReport
    {
        public int Receiver, ReceiverLife, Source;
        public NativeBotSighting Sighting;
        public double ObservedAt, DeliverAt;
    }
    [Serializable] public sealed class NativeBotPerceptionSnapshot
    {
        public int Version = 2;
        public string Configuration;
        public double Time = -1;
        public NativeBotKnowledge[] Observers;
        public NativeBotPendingReport[] Pending;
    }

    /// <summary>Owns only knowledge, never the raw enemy world. No scene/device/render handles.</summary>
    public sealed class NativeBotPerception
    {
        readonly NativeMatchRoster roster;
        readonly float[] fov, memory;
        readonly float delay;
        readonly string configuration;
        NativeBotKnowledge[] observers;
        readonly List<NativeBotPendingReport> pending = new List<NativeBotPendingReport>();
        public double Time { get; private set; } = -1;
        public int Count => roster.Count;
        public float FieldOfView(int observer) => fov[observer];
        public NativeBotKnowledge Read(int observer) => Copy(observers[observer]);
        static NativeBotKnowledge Copy(NativeBotKnowledge value) => new NativeBotKnowledge
        { OwnLife = value.OwnLife, Alive = value.Alive, Enemies = (NativeBotMemoryEntry[])value.Enemies.Clone() };

        public NativeBotPerception(NativeMatchRoster roster, NativeBotDifficulty[] difficulties, ProvingProfile profile)
        {
            this.roster = roster ?? throw new ArgumentNullException(nameof(roster));
            if (difficulties == null || difficulties.Length != Count || difficulties.Any(d => !Enum.IsDefined(typeof(NativeBotDifficulty), d)))
                throw new ArgumentException("Invalid observer difficulties");
            if (profile == null || profile.Id != "unity-bot-perception-v1" || profile.Version != 2 || profile.Validate().Count != 0)
                throw new ArgumentException("Invalid perception profile");
            var canonical = ProvingProfile.CreateBotPerceptionDefault();
            // Metadata cannot be widened to smuggle invalid runtime tuning through a restored profile.
            foreach (var descriptor in canonical.Descriptors)
                if (!descriptor.Contains(profile.Get(descriptor.Path))) throw new ArgumentException("Invalid perception value: " + descriptor.Path);
            fov = new float[Count]; memory = new float[Count]; observers = new NativeBotKnowledge[Count];
            for (int i = 0; i < Count; i++)
            {
                string prefix = "bots." + difficulties[i].ToString().ToLowerInvariant();
                fov[i] = profile.Get(prefix + ".fieldOfViewDegrees"); memory[i] = profile.Get(prefix + ".memorySeconds");
                observers[i] = new NativeBotKnowledge();
            }
            delay = profile.Get("bots.cooperation.communicationSeconds");
            configuration = JsonUtility.ToJson(roster.Read()) + "|" + string.Join(",", difficulties.Select(d => (int)d)) + "|" + JsonUtility.ToJson(profile);
        }

        public void Sample(double time, NativeBotObservationFrame[] frames)
        {
            if (!Finite(time) || time < 0 || time <= Time || frames == null || frames.Length != Count)
                throw new ArgumentException("Invalid observation time/count");
            // Validate the complete input before changing state.
            for (int i = 0; i < Count; i++)
            {
                var frame = frames[i];
                if (frame == null || frame.OwnLife < 1 || frame.Direct == null || (!frame.Alive && frame.Direct.Length != 0))
                    throw new ArgumentException("Invalid observer frame");
                var targets = new HashSet<int>();
                foreach (var sighting in frame.Direct)
                    if (!ValidSighting(i, sighting) || !targets.Add(sighting.Participant)) throw new ArgumentException("Invalid direct sighting");
            }
            Time = time;
            for (int i = 0; i < Count; i++)
            {
                var old = observers[i]; var frame = frames[i];
                bool reset = !frame.Alive || old.OwnLife != frame.OwnLife || !old.Alive;
                if (reset) pending.RemoveAll(p => p.Receiver == i);
                var entries = reset ? Array.Empty<NativeBotMemoryEntry>() : old.Enemies.Where(e => time - e.ObservedAt < memory[i]).ToArray();
                for (int e = 0; e < entries.Length; e++) entries[e].Visible = false;
                observers[i] = new NativeBotKnowledge { OwnLife = frame.OwnLife, Alive = frame.Alive, Enemies = entries };
            }
            // Deliver only previously queued direct sightings; no same-sample propagation or rebroadcast.
            foreach (var report in pending.Where(p => p.DeliverAt <= time).ToArray())
            {
                if (observers[report.Receiver].Alive && observers[report.Receiver].OwnLife == report.ReceiverLife)
                    Remember(report.Receiver, report.Sighting, report.ObservedAt, report.Source, false);
            }
            pending.RemoveAll(p => p.DeliverAt <= time);
            for (int source = 0; source < Count; source++)
            {
                foreach (var sighting in frames[source].Direct)
                {
                    Remember(source, sighting, time, source, true);
                    for (int receiver = 0; receiver < Count; receiver++)
                    {
                        if (!roster.AreAllies(source, receiver) || !observers[receiver].Alive) continue;
                        if (pending.Any(p => p.Receiver == receiver && p.Source == source && p.Sighting.Participant == sighting.Participant)) continue;
                        pending.Add(new NativeBotPendingReport { Receiver = receiver, ReceiverLife = observers[receiver].OwnLife,
                            Source = source, Sighting = observers[source].Enemies.Single(e=>e.Sighting.Participant==sighting.Participant).Sighting, ObservedAt = time, DeliverAt = time + delay });
                    }
                }
            }
        }
        void Remember(int receiver, NativeBotSighting sighting, double observed, int source, bool visible)
        {
            if (Time - observed >= memory[receiver]) return;
            var list = observers[receiver].Enemies.ToList();
            int index = list.FindIndex(e => e.Sighting.Participant == sighting.Participant);
            if (index >= 0 && (list[index].ObservedAt > observed || (list[index].ObservedAt == observed && !visible))) return;
            // Estimate movement only between dated direct observations of the same life.
            if(visible && index>=0 && list[index].Sighting.Life==sighting.Life && source==receiver && list[index].Source==receiver && observed>list[index].ObservedAt)
                sighting.Velocity=(sighting.Position-list[index].Sighting.Position)/(float)(observed-list[index].ObservedAt);
            var entry = new NativeBotMemoryEntry { Sighting = sighting, ObservedAt = observed, Source = source, Visible = visible };
            if (index < 0) list.Add(entry); else list[index] = entry;
            observers[receiver].Enemies = list.OrderBy(e => e.Sighting.Participant).ToArray();
        }
        bool ValidSighting(int observer, NativeBotSighting s) => s.Participant >= 0 && s.Participant < Count && s.Participant != observer &&
            !roster.AreAllies(observer, s.Participant) && s.Life > 0 && Finite(s.Position.x) && Finite(s.Position.y) && Finite(s.Position.z) && Finite(s.Velocity.x) && Finite(s.Velocity.y) && Finite(s.Velocity.z) && Finite(s.Health) && Finite(s.Armor) && s.Health>=0 && s.Armor>=0;
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        bool ValidSource(int receiver, int source) => source >= 0 && source < Count && (source == receiver || roster.AreAllies(receiver, source));
        public NativeBotPerceptionSnapshot Capture() => new NativeBotPerceptionSnapshot
        { Configuration = configuration, Time = Time, Observers = observers.Select(Copy).ToArray(), Pending = pending.ToArray() };

        public void ValidateSnapshot(NativeBotPerceptionSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Version != 2 || snapshot.Configuration != configuration || !Finite(snapshot.Time) ||
                (snapshot.Time < 0 && snapshot.Time != -1) || snapshot.Observers == null || snapshot.Observers.Length != Count || snapshot.Pending == null)
                throw new ArgumentException("Invalid perception snapshot identity");
            for (int i = 0; i < Count; i++)
            {
                var observer = snapshot.Observers[i];
                if (observer == null || observer.Enemies == null || observer.OwnLife < (snapshot.Time < 0 ? 0 : 1) ||
                    (!observer.Alive && observer.Enemies.Length != 0)) throw new ArgumentException("Invalid observer snapshot");
                var targets = new HashSet<int>();
                foreach (var e in observer.Enemies)
                    if (!ValidSighting(i, e.Sighting) || !targets.Add(e.Sighting.Participant) || !ValidSource(i, e.Source) ||
                        !Finite(e.ObservedAt) || e.ObservedAt < 0 || e.ObservedAt > snapshot.Time || snapshot.Time - e.ObservedAt >= memory[i] ||
                        (e.Visible && (e.Source != i || e.ObservedAt != snapshot.Time))) throw new ArgumentException("Invalid memory snapshot");
            }
            var keys = new HashSet<string>();
            foreach (var p in snapshot.Pending)
            {
                if (p.Receiver < 0 || p.Receiver >= Count || !ValidSource(p.Receiver, p.Source) || p.Receiver == p.Source ||
                    !ValidSighting(p.Receiver, p.Sighting) || !Finite(p.ObservedAt) || p.ObservedAt < 0 || p.ObservedAt > snapshot.Time ||
                    !Finite(p.DeliverAt) || p.DeliverAt != p.ObservedAt + delay || p.DeliverAt <= snapshot.Time ||
                    !snapshot.Observers[p.Receiver].Alive || p.ReceiverLife != snapshot.Observers[p.Receiver].OwnLife ||
                    !keys.Add(p.Receiver+":"+p.Source+":"+p.Sighting.Participant)) throw new ArgumentException("Invalid report snapshot");
            }
            if (snapshot.Time < 0 && (snapshot.Pending.Length != 0 || snapshot.Observers.Any(o => o.Alive || o.OwnLife != 0 || o.Enemies.Length != 0)))
                throw new ArgumentException("Invalid initial snapshot");
        }
        public void Restore(NativeBotPerceptionSnapshot snapshot)
        {
            ValidateSnapshot(snapshot);
            Time = snapshot.Time; observers = snapshot.Observers.Select(Copy).ToArray(); pending.Clear(); pending.AddRange(snapshot.Pending);
        }
    }
}
