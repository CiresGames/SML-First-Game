using System;
using UnityEngine;
using MantaFlight.Rider;

namespace MantaFlight
{
    [DefaultExecutionOrder(10)]
    [DisallowMultipleComponent]
    public sealed class MantaProgression : MonoBehaviour
    {
        [Tooltip("Use a distinct ID for each persistent companion/save slot.")]
        public string saveId = "companion-1";
        public bool spawnExplorationFruits = true;
        [Min(0)] public float cargoKilograms;
        public MantaProgressionData Data { get; private set; } = new MantaProgressionData();
        public MantaController Controller { get; private set; }
        public MantaMountAdapter Mount { get; private set; }
        public bool Failing { get; private set; }
        public float Fade { get; private set; }
        public string Notice { get; private set; } = "";
        public float MaxEndurance => 100 * (1 + Data.Strength(MantaStat.Endurance));
        public float Endurance01 => Mathf.Clamp01(Data.endurance / MaxEndurance);
        public float Capacity => 40 + 100 * Data.Strength(MantaStat.Force);
        public float Load01 => Mathf.Clamp01(cargoKilograms / Capacity);
        public float WindMultiplier => 1 / (1 + 2 * Data.Strength(MantaStat.Force));
        public float SpeedMultiplier => (1 + .5f * Data.Strength(MantaStat.Speed)) * FatiguePerformance;
        public float TurnMultiplier => (1 + .6f * Data.Strength(MantaStat.Manoeuvrability)) * Mathf.Lerp(1, .65f, Load01) * FatiguePerformance;
        public float ResponseMultiplier => (1 + .8f * Data.Strength(MantaStat.Manoeuvrability)) * Mathf.Lerp(1, .7f, Load01);
        public float AccelerationMultiplier => Mathf.Lerp(1, .45f, Load01) * FatiguePerformance;
        public float ClimbMultiplier => Mathf.Lerp(1, .45f, Load01) * FatiguePerformance;
        public float FatiguePerformance => Mathf.Lerp(.55f, 1, Mathf.Clamp01(Endurance01 / .1f));
        public bool CanFly => !Failing && !Data.exhausted && Data.endurance > 0 && cargoKilograms <= Capacity;
        public bool Airborne => Mount == null || !Mount.Landed;
        public bool Accessible => Mount != null && Mount.rider != null && (Mount.rider.Attached
            || (!Mount.rider.Airborne && Vector3.Distance(Mount.rider.transform.position, Mount.Seat.position) <= 9));
        public string FlightBlockReason => cargoKilograms > Capacity ? $"Load too heavy: {cargoKilograms:F0} / {Capacity:F0} kg"
            : Data.exhausted ? "Exhausted — recover to 30% before riding" : "";
        Vector3 lastPosition, safeManta, safeRider;
        Quaternion safeRotation;
        bool haveSafeGround, initialized, sharpLast;
        float failureTime, saveTimer, groundTimer, soundTimer;
        MantaInput input;
        MantaManeuvers maneuvers;
        MantaTrick previousTrick;
        AudioSource fatigueAudio;
        AudioClip fatigueClip;
        string Key => "Manta.Progression.v1." + saveId;

        void Start()
        {
            Controller = GetComponent<MantaController>(); Mount = GetComponent<MantaMountAdapter>();
            input = GetComponent<MantaInput>(); maneuvers = GetComponent<MantaManeuvers>();
            Load(); initialized = true; lastPosition = transform.position;
            FindSafeGround(transform.position, true);
            if (GetComponent<MantaProgressionPanel>() == null) gameObject.AddComponent<MantaProgressionPanel>();
            if (spawnExplorationFruits && gameObject.scene.name == "MantaFlight")
            {
                MantaFruit.CreateExplorationRewards(this);
                if (haveSafeGround) MantaCargo.CreateStarterCargo(this, safeRider);
            }
            // A restored exhausted companion must not start a fresh flight on scene reload.
            if (Data.exhausted || Data.endurance <= 0) BeginFailure();
        }
        public bool Feed(MantaStat stat)
        {
            if (!Accessible || Failing || !Data.Feed(stat)) return false;
            Notice = stat + " fruit fed — permanent bonus gained"; Save(); return true;
        }
        public bool CollectFruit(string id, MantaStat stat)
        {
            if (!Data.CollectFruit(id, stat)) return false;
            Notice = stat + " fruit found — feed it from Manta Info"; Save(); return true;
        }
        public void DiscoverLocation(string id) { if (Data.Discover("location:" + id)) { Notice = "Landmark discovered"; Save(); } }
        public void CompleteChallenge(string id, MantaStat stat)
        {
            if (!MantaProgressionData.Valid(stat) || !Data.Discover("challenge:" + id, 150, 50)) return;
            Notice = "Aerial challenge completed"; Save();
        }
        public void CompleteJourney(string id) { if (Data.Discover("journey:" + id, 100, 100)) Save(); }
        public void RecordStoryEvent(string id) { if (Data.Discover("story:" + id, 0, 100)) Save(); }
        public bool SetCargo(float kilograms)
        {
            if (!MantaProgressionData.Valid(kilograms) || (Airborne && kilograms > Capacity)) return false;
            cargoKilograms = kilograms; return true;
        }
        void FixedUpdate()
        {
            if (!initialized || input.MenuOpen) return;
            float dt = Time.fixedDeltaTime;
            if (Failing) { TickFailure(dt); return; }
            groundTimer += dt;
            if (groundTimer >= 4) { groundTimer = 0; FindSafeGround(transform.position, false); }
            float travelled = Vector3.Distance(Controller.PhysicsPosition, lastPosition);
            lastPosition = Controller.PhysicsPosition;
            if (Airborne)
            {
                var state = input.State;
                float wind = WindManager.GetWindAt(transform.position).magnitude;
                float exertion = Mathf.Clamp01(Controller.Speed / Mathf.Max(1, Controller.settings.maximumSpeed));
                bool piloted = Mount == null || Mount.Mode == MantaServiceState.Piloted;
                bool sharp = piloted && state.steering.magnitude > .75f && state.tightTurn;
                bool trick = piloted && maneuvers.Current != MantaTrick.None && maneuvers.Current != previousTrick;
                float climb = Mathf.Clamp01(Controller.Velocity.y / 15);
                bool glide = piloted && state.throttle < .1f && climb < .05f && Controller.Velocity.y < -.2f;
                float drain = glide ? .035f : .18f + .45f * state.throttle + .4f * Mathf.InverseLerp(.7f, 1, exertion);
                drain += climb * .6f + Load01 * .65f + Mathf.Min(2, wind / 25) * WindMultiplier * .4f;
                if (sharp) drain += .25f;
                Data.endurance = Mathf.Max(0, Data.endurance - drain * dt - ((sharp && !sharpLast) || trick ? 1.5f : 0));
                sharpLast = sharp; previousTrick = maneuvers.Current;
                if (piloted && (Mount == null || Mount.rider.Attached) && travelled > .02f && travelled < 20)
                {
                    int before = Data.level;
                    Data.AddXP(dt * (1 + Mathf.Min(wind / 40, 1) + Load01 * .25f) + travelled * .01f);
                    Data.AddBond(dt * .2f);
                    if (trick) Data.AddXP(4);
                    if (before != Data.level) { Notice = $"Level {Data.level}"; Save(); }
                }
                if (Data.endurance <= 0) BeginFailure();
            }
            else
            {
                sharpLast = false; previousTrick = MantaTrick.None;
                Data.endurance = Mathf.Min(MaxEndurance, Data.endurance + dt * 3);
                if (Data.exhausted && Endurance01 >= .3f) { Data.exhausted = false; Notice = "Recovered — ready to ride"; Save(); }
            }
            if (Endurance01 < .25f && Airborne)
            {
                soundTimer -= dt;
                if (soundTimer <= 0) { PlayFatigueSound(); soundTimer = Endurance01 < .1f ? 2 : 5; }
            }
            saveTimer += dt; if (saveTimer >= 20) Save();
        }
        void BeginFailure()
        {
            Data.exhausted = true; Failing = true; failureTime = 0; Fade = 0;
            input.ControlEnabled = false; Controller.Paused = true; maneuvers.Cancel();
            if (Mount && Mount.rider) Mount.rider.CommandWheel.Cancel();
            Notice = "Exhausted — losing altitude"; Save();
        }
        void TickFailure(float dt)
        {
            failureTime += dt;
            if (failureTime < 1.5f)
            {
                Vector3 fall = Vector3.down * (3 + failureTime * 5) * dt;
                if (!Physics.SphereCast(Controller.PhysicsPosition, Controller.settings.collisionRadius, Vector3.down,
                    out _, fall.magnitude + .2f, Controller.settings.environmentMask, QueryTriggerInteraction.Ignore))
                    Controller.MoveExternalMotion(Controller.PhysicsPosition + fall, Controller.Heading, fall / dt);
                Fade = Mathf.Clamp01((failureTime - .5f) / 1);
                return;
            }
            if (failureTime < 2.5f)
            {
                Fade = 1;
                if (haveSafeGround) haveSafeGround = FindSafeGround(safeManta, false);
                if (!haveSafeGround) FindSafeGround(transform.position, true);
                if (!haveSafeGround || Mount == null || Mount.rider == null)
                {
                    // Never teleport a rider to an unverified coordinate.
                    Notice = "No safe ground found — reset to a safe landing area";
                    Failing = false; Fade = 0; return;
                }
                Mount.rider.RecoverFromExhaustion(safeRider, safeRotation);
                Controller.SetExternalMotion(safeManta, safeRotation, Vector3.zero);
                Mount.RestAfterExhaustion();
                failureTime = 2.5f;
            }
            Fade = 1 - Mathf.Clamp01((failureTime - 2.5f) / .8f);
            if (failureTime >= 3.3f) { Failing = false; Fade = 0; lastPosition = safeManta; Notice = "Resting — recover to 30% before riding"; Save(); }
        }
        bool FindSafeGround(Vector3 around, bool broad)
        {
            if (Mount == null || Mount.rider == null) return false;
            var rider = Mount.rider;
            int mask = rider.settings.environment;
            int count = broad ? 80 : 1;
            for (int i = 0; i < count; i++)
            {
                float radius = i == 0 ? 0 : 12 + (i / 8) * 60;
                Vector3 origin = around + new Vector3(Mathf.Cos(i * 2.4f) * radius, 1500, Mathf.Sin(i * 2.4f) * radius);
                if (!Physics.Raycast(origin, Vector3.down, out var hit, 4000, mask, QueryTriggerInteraction.Ignore)
                    || Vector3.Angle(hit.normal, Vector3.up) > 30) continue;
                Vector3 mp = hit.point + Vector3.up * Mathf.Max(rider.settings.mount.hoverHeight, Controller.settings.collisionRadius + .3f);
                if (Physics.CheckSphere(mp, Controller.settings.collisionRadius + .1f, mask, QueryTriggerInteraction.Ignore)) continue;
                float offset = Mathf.Max(Controller.settings.collisionRadius + 2, rider.settings.mount.sideDistance);
                if (!Physics.Raycast(hit.point + Vector3.right * offset + Vector3.up * 5, Vector3.down,
                    out var beside, 10, mask, QueryTriggerInteraction.Ignore) || Vector3.Angle(beside.normal, Vector3.up) > 30) continue;
                Vector3 rp = beside.point + Vector3.up * .12f;
                float r = rider.settings.ground.radius, h = rider.settings.ground.standingHeight;
                if (Physics.CheckCapsule(rp + Vector3.up * r, rp + Vector3.up * (h - r), r, mask, QueryTriggerInteraction.Ignore)) continue;
                safeManta = mp; safeRider = rp; safeRotation = Quaternion.identity; haveSafeGround = true; return true;
            }
            return false;
        }
        void PlayFatigueSound()
        {
            if (fatigueAudio == null)
            {
                fatigueAudio = gameObject.AddComponent<AudioSource>(); fatigueAudio.playOnAwake = false;
                fatigueAudio.spatialBlend = .2f; fatigueAudio.volume = .16f;
                int samples = 22050; float[] pcm = new float[samples];
                for (int i = 0; i < samples; i++) { float t = i / 22050f; pcm[i] = Mathf.Sin(t * 2 * Mathf.PI * (100 - t * 35)) * Mathf.Sin(t * Mathf.PI) * .5f; }
                fatigueClip = AudioClip.Create("Manta tired breath", samples, 1, 22050, false); fatigueClip.SetData(pcm, 0);
                fatigueAudio.clip = fatigueClip;
            }
            fatigueAudio.Play();
        }
        void Load()
        {
            foreach (string key in new[] { Key, Key + ".backup" })
            {
                if (!PlayerPrefs.HasKey(key)) continue;
                try
                {
                    var candidate = MantaProgressionSave.Read(PlayerPrefs.GetString(key));
                    if (candidate != null) { Data = candidate; Data.endurance = Mathf.Min(Data.endurance, MaxEndurance); return; }
                }
                catch (Exception e) { Debug.LogWarning("Manta progression save unreadable: " + e.Message); }
            }
            Data = new MantaProgressionData();
        }
        public void Save()
        {
            if (!initialized) return;
            // Only replace the backup with a validated previous snapshot.
            string previous = PlayerPrefs.GetString(Key, "");
            try { if (MantaProgressionSave.Read(previous) != null) PlayerPrefs.SetString(Key + ".backup", previous); }
            catch (Exception) { /* Preserve the last good backup. */ }
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data)); PlayerPrefs.Save(); saveTimer = 0;
        }
        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationQuit() => Save();
        void OnDestroy() { Save(); if (fatigueClip) Destroy(fatigueClip); }
    }
}
