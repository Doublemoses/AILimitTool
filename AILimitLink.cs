using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AILimitTool
{

    static class Offsets
    {
        // ViewManager
        public const int currentUI = 0x50;
        public const int currentUIID = 0x40;

        // ArchiveData offsets
        public const int playerLevel = 0x18;
        public const int transferDestination = 0x24;
        public const int crystals = 0x1C;
        public const int HP = 0xB8;
        public const int newGameCycle = 0x68;
        public const int playerStats = 0x88;
        public const int currentTransferID = 0x24; // current destination when using broken branch

        // Player offsets
        public const int playerImmortal = 0xDC;
        public const int playerLockSync = 0x208;
        public const int playerTarget = 0xF8;

        public const int playerDefine = 0xC0;
        public const int playerSpeed = 0x20;

        public const int playerWeaponMain = 0x178;
        public const int playerWeaponReserve = 0x180;

        // Weapon Offsets
        public const int weaponDefine = 0x28;

        public const int weaponLevelupCost = 0x74;
        public const int weaponLevelupItem = 0x78;

        // Player xyz
        public const int playerX = 0x1F8;
        public const int playerY = 0x200;
        public const int playerZ = 0x208;

        // ActorModel offsets
        public const int actorModelHP = 0x10;
        public const int actorModelHPMax = 0x18;
        public const int actorModelPlayer = 0x88;
        public const int actorModelMonster = 0x58;
        public const int actorModelPoisonAccumulation = 0x78;
        public const int actorModelPiercingAccumulation = 0x80; // internally labelled puncture
        public const int actorModelInfectionAccumulation = 0x88;
        public const int actorModelSync = 0xB0; // internally labelled confidence

        public const int actorModelPhysicalDamage = 0x20;
        public const int actorModelPoisonDamage = 0x48;
        public const int actorModelPunctureDamage = 0x50;
        public const int actorModelInfectDamage = 0x58;

        public const int actorModelPhysicalDefense = 0x28;
        public const int actorModelElectricDefense = 0x30;
        public const int actorModelShatterDefense = 0x38; // internally psycho defense
        public const int actorModelFireDefense = 0x40; // internally dimension defense

        public const int actorModelPoisonResist = 0x60;
        public const int actorModelPiercingResist = 0x68;
        public const int actorModelInfectionResist = 0x70;

        public const int actorModelSuperArmourLevel = 0xA8;

        // Monster offsets
        public const int monsterTenacityDecreaseTick = 0x4D8; // labelled NowTenacityBreak
        public const int monsterTenacity = 0x4E8; // labelled NowTenacityBreak
        public const int monsterTenacityMax = 0x4E0; // labelled NowTenacity

        // View offsets
        public const int viewId = 0x40;

        // Loading screen offsets
        public const int isLoading = 0x44;

        // LevelRoot offsets
        public const int levelID = 0x44;
        public const int levelName = 0x48;
        public const int levelModel = 0x58;

        // ArchiveData shop stuff
        public const int ShopSystemData = 0x78;
    }

    class AILimitLink : IDisposable
    {
        private const uint PROCESS_ALL_ACCESS = 0x1F0FFF;               
        private const string processName = "ai-limit";

        const ulong addressMinimum = 0x10000000000;
        const ulong addressMaximum = 0x6FFFFFFFFFF;

        private ConnectionState connectionStatus = ConnectionState.NotConnected;

        private GameLink aiLimit;

        System.Windows.Threading.DispatcherTimer monitorTimer = new System.Windows.Threading.DispatcherTimer();

        bool disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
            }
            disposed = true;
        }

        public AILimitLink()
        {
            aiLimit = new GameLink(processName, new string[] { "gameassembly.dll", "unityplayer.dll" });

            monitorTimer.Tick += monitorTimer_Tick;
            monitorTimer.Interval = TimeSpan.FromMilliseconds(200);
            monitorTimer.Start();
        }

        private bool waitingForLoadingScreenToEnd = false;

        private void monitorTimer_Tick(object? sender, EventArgs e)
        {
            if (connectionStatus == ConnectionState.Connected && !aiLimit.CheckConnectionStatus())
            {
                connectionStatus = ConnectionState.NotConnected;
                monitorTimer.Interval = TimeSpan.FromMilliseconds(10000);
            }

            if (connectionStatus == ConnectionState.NotConnected)
            {
                if (aiLimit.InitGameLink())
                    connectionStatus = ConnectionState.ConnectedOffsetsNotFound;
                else
                    connectionStatus = ConnectionState.ProcessNotFound;
            }

            if (connectionStatus == ConnectionState.ConnectedOffsetsNotFound)
            {
                if (LocatePointers())
                {
                    connectionStatus = ConnectionState.Connected;
                    monitorTimer.Interval = TimeSpan.FromMilliseconds(200);
                }
            }

            if (connectionStatus == ConnectionState.Connected)
            {
                double value;

                // Reapply options that must be constantly set
                if (activeOptions.TryGetValue(GameOptions.LockTargetHP, out value))
                    SetTargetMonsterValue(MonsterStats.HPPercent, false, value);

                if (activeOptions.ContainsKey(GameOptions.FreeUpgrade))
                    FreeWeaponUpgrades();

                // Reapply options that may reset upon level loads/reloading save
                if (IsLoadingScreenActive())
                    waitingForLoadingScreenToEnd = true;
                else if (waitingForLoadingScreenToEnd)
                {
                    waitingForLoadingScreenToEnd = false;

                    foreach (KeyValuePair<GameOptions, double> entry in activeOptions)
                    {
                        switch (entry.Key)
                        {
                            case GameOptions.Immortal:
                            case GameOptions.LockSync:
                                TogglePlayerBools(entry.Key, true);
                                break;
                        }
                    }
                }
            }
        }

        enum Module
        {
            GameAssembly,
            UnityPlayer,
        }

        IntPtr archiveDataBase = IntPtr.Zero;
        IntPtr viewManagerBase = IntPtr.Zero;
        IntPtr levelLoadManagerBase = IntPtr.Zero;
        IntPtr playerBase = IntPtr.Zero;
        
        IntPtr timerAddress = IntPtr.Zero;
        IntPtr xPosCodeAddress = IntPtr.Zero;
        IntPtr zPosCodeAddress = IntPtr.Zero;

        IntPtr infiniteConsumablesCodeAddress = IntPtr.Zero;
        IntPtr passiveEnemiesCodeAddress = IntPtr.Zero;

        public GameVersion version = GameVersion.vNotFound;
        public Dictionary<uint, bool> addressFound = new Dictionary<uint, bool>();

        public enum GameVersion
        {
            v1_0_020a, // three versions from release day
            v1_0_020b, // I don't know the actual versions, so assuming 1.0.020 
            v1_0_020c, // and added letters to separate different versions
            v1_0_021,
            v1_0_022,
            v1_0_023,
            vNotFound = 100,
        }

        // Seemingly not needed since adding AOBs. Delete?
        private void IdentifyGameVersion()
        {
            switch (aiLimit.BaseAddress[(int)Module.GameAssembly])
            {
                case 79798272:
                    version = GameVersion.v1_0_020b;
                    break;
                case 79904768:
                    version = GameVersion.v1_0_020c;
                    break;
                case 79880192:
                    version = GameVersion.v1_0_021;
                    break;
                case 79888384:
                    version = GameVersion.v1_0_022;
                    break;
                case 79896576:
                    version = GameVersion.v1_0_023;
                    break;
            }
        }

        // Stable pointers that should get set once and never change while game is running
        // GameAssembly.dll pointers will change every patch
        // UnityPlayer.dll pointers are stable across all versions, so static pointers should be fine long term
        private bool LocatePointers()
        {
            Debug.Print(aiLimit.BaseAddress[(int)Module.GameAssembly].ToString("x") + " " + " Seeking addresses...");

            using (AOBScanner scanner = new AOBScanner(aiLimit.ProcessHandle, aiLimit.BaseAddress[(int)Module.GameAssembly], "il2cpp"))
            {
                archiveDataBase = scanner.FindAddress("48 8B 0d ?? ?? ?? ?? 83 B9 E0 00 00 00 00 75 ?? E8 99 0B");
                archiveDataBase = aiLimit.ResolveOffsetPointer(archiveDataBase + 3);

                viewManagerBase = scanner.FindAddress("48 8B 0D ?? ?? ?? ?? 8B F8 48 8B 41 20");
                viewManagerBase = aiLimit.ResolveOffsetPointer(viewManagerBase + 3);


                levelLoadManagerBase = scanner.FindAddress("33 D2 E8 ?? ?? ?? ?? 66 66 66 0F 1F 84 00 00 00 00 00 48 8B 0D");
                levelLoadManagerBase = aiLimit.ResolveOffsetPointer(levelLoadManagerBase + 21);

                playerBase = scanner.FindAddress("48 8b 0d ?? ?? ?? ?? 0f 95 c3");
                playerBase = aiLimit.ResolveOffsetPointer(playerBase + 3);

                infiniteConsumablesCodeAddress = scanner.FindAddress("89 41 14 48 8B 4C 24 30");
                if (infiniteConsumablesCodeAddress == IntPtr.Zero)
                    infiniteConsumablesCodeAddress = scanner.FindAddress("48 8B 4C 24 30 48 85 C9 0F 84 8B") - 3;

                passiveEnemiesCodeAddress = scanner.FindAddress("48 89 BB 30 01 00 00 48 8B D7 E8 4E");
                if (passiveEnemiesCodeAddress == IntPtr.Zero)
                    passiveEnemiesCodeAddress = scanner.FindAddress("48 8B D7 E8 4E 83 1A FE") - 7;
            }

            if (archiveDataBase > 0x100000
                && viewManagerBase > 0x100000
                && levelLoadManagerBase > 0x100000
                && playerBase > 0x100000 )
            {
                // UnityPlayer.dll addresses are unchanged across different patches.
                timerAddress = aiLimit.ResolvePointerChain(aiLimit.BaseAddress[(int)Module.UnityPlayer] + 0x01CA3978) + 0x60;
                xPosCodeAddress = aiLimit.BaseAddress[(int)Module.UnityPlayer] + 0x0140227C;
                zPosCodeAddress = aiLimit.BaseAddress[(int)Module.UnityPlayer] + 0x01402288;

                return true;
            }
            return false;
        }

        public IntPtr GetAddress(AddressType type)
        {
            switch (type)
            {
                case AddressType.GameAssembly:
                    return aiLimit.BaseAddress[(int)Module.GameAssembly];
                case AddressType.UnityPlayer:
                    return aiLimit.BaseAddress[(int)Module.UnityPlayer];
                case AddressType.ArchiveData:
                    return aiLimit.ResolvePointerChain(archiveDataBase + 0xB8, 0x0, 0x18);
                case AddressType.Player:
                    return aiLimit.ResolvePointerChain(playerBase + 0x20, 0xB8, 0x0, 0x48);
                case AddressType.CurrentView:
                    return aiLimit.ResolvePointerChain(viewManagerBase + 0x20, 0xB8, 0x0, 0x50);
                case AddressType.LevelRoot:
                    return aiLimit.ResolvePointerChain(levelLoadManagerBase + 0xB8, 0x0, 0x38);
                case AddressType.Timer:
                    return timerAddress;
            }
            return 0;
        }

        public ConnectionState ConnectionStatus
        {
            get
            {
                return connectionStatus;
            }
        }

        // Generates empty machine code that does nothing. 0x90 is the default value.
        public byte[] EmptyCode(int length, byte data = 0x90)
        {
            byte[] returnBytes = new byte[length];

            for (int i = 0; i < length; i++)
                returnBytes[i] = data;

            return returnBytes;
        }

        public enum GameOptions
        {
            None,
            Immortal,
            LockSync,
            LockTargetHP,
            FreeUpgrade,
            InfiniteDew,
            PlayerSpeed,
            PassiveEnemies,

            TargetInfo,

            AddCrystals,

            TeleportQucksave,
            TeleportQuickload,
            Teleport,
            SetTransfer,

            NextTab,
        }

        // Contains all currently active options that need to be managed beyond just turning on and off
        private Dictionary<GameOptions, double> activeOptions = new Dictionary<GameOptions, double>();

        public void AddGameOption(GameOptions option, double value = 0)
        {
            if (!activeOptions.ContainsKey(option))
            {
                activeOptions.Add(option, value);
            }
        }

        public void RemoveGameOption(GameOptions option)
        {
            activeOptions.Remove(option);
        }

        private bool AddressInRange(IntPtr address)
        {
            if ((ulong)address < addressMinimum || (ulong)address > addressMaximum)
                return false;
            return true;
        }

        //
        // Loading data
        //
        public bool IsLoadingScreenActive()
        {
            if (aiLimit.ReadUInt32(GetAddress(AddressType.CurrentView) + Offsets.viewId) == 3)
                return true;
            return false;
        }

        public string GetCurrentLevelName()
        {
            IntPtr address = (IntPtr)aiLimit.ReadUInt64(GetAddress(AddressType.LevelRoot) + Offsets.levelName);

            return aiLimit.ReadString(address + 0x14,
                (int)aiLimit.ReadUInt32(address + 0x10));
        }

        public uint GetCurrentLevelID()
        {
            return aiLimit.ReadUInt32(aiLimit.ResolvePointerChain(GetAddress(AddressType.LevelRoot) + Offsets.levelModel) + 0x10);
        }

        public double GetTimer()
        {
            return aiLimit.ReadDouble((IntPtr)timerAddress);
        }

        public enum PlayerStats
        {
            Life,
            Strength,
            Technique,
            Spirit,
            Vitality,
            PlayerLevel,
            Crystals,
            TransferDestination,
        }

        // if add == true, newValue should be added to existing value, instead of replacing it
        // if newValue is default -1, check current value and return it
        private IntPtr GetPlayerStatAddress(PlayerStats stat)
        {
            IntPtr address = GetAddress(AddressType.ArchiveData);

            if ((int)stat < 5) // player stats 0-4 are player stats in ArchiveData -> _PlayerPoint
            {
                address = aiLimit.ResolvePointerChain(address + Offsets.playerStats, 0x10 + ((int)stat * 8)) + 0x10;
            }
            else
            {
                switch (stat)
                {
                    case PlayerStats.PlayerLevel:
                        address += Offsets.playerLevel;
                        break;
                    case PlayerStats.Crystals:
                        address += Offsets.crystals;
                        break;
                    case PlayerStats.TransferDestination:
                        address += Offsets.transferDestination;
                        break;
                }
            }
            
            return address;
        }

        public uint GetPlayerStat(PlayerStats stat)
        {
            IntPtr address = GetPlayerStatAddress(stat);

            if (!AddressInRange(address))
                return 0;

            return aiLimit.ReadUInt32(address);
        }

        public void SetPlayerStat(PlayerStats stat, uint newValue, bool add = false)
        {
            IntPtr address = GetPlayerStatAddress(stat);

            if (!AddressInRange(address))
                return;

            if (add)
                aiLimit.WriteUInt32(address, aiLimit.ReadUInt32(address) + (uint)newValue);
            else
                aiLimit.WriteUInt32(address, (uint)newValue);
        }

        enum ReturnTypes
        {
            returnByte,
            returnUInt32,
            returnFloat,
        }

        public bool TogglePlayerBools(GameOptions option, bool value)
        {
            IntPtr address = GetAddress(AddressType.Player);

            switch (option)
            {
                case GameOptions.Immortal:
                    address += Offsets.playerImmortal;
                    break;
                case GameOptions.LockSync:
                    address += Offsets.playerLockSync;
                    break;
            }

            if (!AddressInRange(address))
                return false;

            if (value)
                activeOptions.TryAdd(option, 0);
            else
                activeOptions.Remove(option);

            aiLimit.WriteByte(address, Convert.ToByte(value));
            return true;
        }

        public void SetPlayerSpeed(float speed)
        {
            IntPtr address = GetAddress(AddressType.Player) + Offsets.playerDefine;
            address = (IntPtr)aiLimit.ReadUInt64(address) + Offsets.playerSpeed;

            if (!AddressInRange(address))
                return;

            aiLimit.WriteFloat(address, speed);
        }

        //
        // Retreving data values from enemies
        //

        public enum MonsterStats
        {
            HP,
            HPMax,
            HPPercent,
            Sync,
            PoisonAccumulation,
            PiercingAccumulation,
            InfectionAccumulation,

            TenacityDecreaseTick,
            Tenacity,
            TenacityMax,

            PhysicalDamage,
            PoisonDamage,
            PunctureDamage,
            InfectDamage,

            PhysicalDefense,
            ElectricDefense,
            ShatterDefense,
            FireDefense,

            PoisonResist,
            PiercingResist,
            InfectionResist,

            SuperArmourLevel,
        }

        public IntPtr previousTargetAddress = 0;  // holds the memory address of the last targetted enemy

        public double SetTargetMonsterValue(MonsterStats stat, bool usePreviousAddressIfEmpty = false, double newValue = -1)
        {
            IntPtr address = (IntPtr)aiLimit.ReadUInt64(GetAddress(AddressType.Player) + Offsets.playerTarget);

            if (address != 0)
                previousTargetAddress = address;

            if (address == 0 && usePreviousAddressIfEmpty)
            {
                if (previousTargetAddress == 0)
                    return 0;

                address = previousTargetAddress;
            }

            return SetMonsterValue(stat, address, newValue);
        }

        // TODO: Needs a complete rewrite
        private double SetMonsterValue(MonsterStats option, IntPtr monsterAddress, double newValue = -1)
        {
            IntPtr actorModelBase = monsterAddress + Offsets.actorModelMonster;
            IntPtr address = 0;

            ReturnTypes returnType = ReturnTypes.returnFloat;

            switch (option)
            {
                case MonsterStats.HP:
                    address = aiLimit.ResolvePointerChain(actorModelBase, Offsets.actorModelHP) + 0x10;
                    break;
                case MonsterStats.HPMax:
                    address = aiLimit.ResolvePointerChain(actorModelBase, Offsets.actorModelHPMax) + 0x10;
                    break;
                case MonsterStats.Sync:
                    address = aiLimit.ResolvePointerChain(actorModelBase, Offsets.actorModelSync) + 0x10;
                    break;
                case MonsterStats.HPPercent: // value returned wont be a %, but assume hp% will never be used
                    address = aiLimit.ResolvePointerChain(actorModelBase, Offsets.actorModelHP) + 0x10;
                    newValue = aiLimit.ReadFloat(aiLimit.ResolvePointerChain(actorModelBase, Offsets.actorModelHPMax) + 0x10) * (newValue / 100);
                    break;
                case MonsterStats.PhysicalDamage:
                    address = aiLimit.ResolvePointerChain(actorModelBase, Offsets.actorModelPhysicalDefense) + 0x10;
                    break;
                case MonsterStats.PoisonAccumulation:
                    address = aiLimit.ResolvePointerChain(actorModelBase, Offsets.actorModelPoisonAccumulation) + 0x10;
                    break;
                case MonsterStats.PiercingAccumulation:
                    address = aiLimit.ResolvePointerChain(actorModelBase, Offsets.actorModelPiercingAccumulation) + 0x10;
                    break;
                case MonsterStats.InfectionAccumulation:
                    address = aiLimit.ResolvePointerChain(actorModelBase, Offsets.actorModelInfectionAccumulation) + 0x10;
                    break;

                case MonsterStats.PhysicalDefense:
                    address = aiLimit.ResolvePointerChain(actorModelBase, Offsets.actorModelPhysicalDefense) + 0x10;
                    break;
                case MonsterStats.ElectricDefense:
                    address = aiLimit.ResolvePointerChain(actorModelBase, Offsets.actorModelElectricDefense) + 0x10;
                    break;
                case MonsterStats.ShatterDefense:
                    address = aiLimit.ResolvePointerChain(actorModelBase, Offsets.actorModelShatterDefense) + 0x10;
                    break;
                case MonsterStats.FireDefense:
                    address = aiLimit.ResolvePointerChain(actorModelBase, Offsets.actorModelFireDefense) + 0x10;
                    break;

                case MonsterStats.PoisonResist:
                    address = aiLimit.ResolvePointerChain(actorModelBase, Offsets.actorModelPoisonResist) + 0x10;
                    break;
                case MonsterStats.PiercingResist:
                    address = aiLimit.ResolvePointerChain(actorModelBase, Offsets.actorModelPiercingResist) + 0x10;
                    break;
                case MonsterStats.InfectionResist:
                    address = aiLimit.ResolvePointerChain(actorModelBase, Offsets.actorModelInfectionResist) + 0x10;
                    break;

                case MonsterStats.SuperArmourLevel:
                    address = aiLimit.ResolvePointerChain(actorModelBase, Offsets.actorModelSuperArmourLevel) + 0x10;
                    returnType = ReturnTypes.returnUInt32;
                    break;

                case MonsterStats.TenacityDecreaseTick:
                    address = monsterAddress + Offsets.monsterTenacityDecreaseTick;
                    break;
                case MonsterStats.Tenacity:
                    address = (IntPtr)aiLimit.ReadUInt64(monsterAddress + Offsets.monsterTenacity) + 0x10;
                    break;
                case MonsterStats.TenacityMax:
                    address = (IntPtr)aiLimit.ReadUInt64(monsterAddress + Offsets.monsterTenacityMax) + 0x10;
                    returnType = ReturnTypes.returnUInt32;
                    break;
                default:
                    return -1;
            }

            if (!AddressInRange(address))
                return 0;

            // Assume we never need to manually set TenacityMax value, as it is the only field that isn't a float.
            if (newValue > -1)
                aiLimit.WriteFloat(address, (float)newValue);

            if (returnType == ReturnTypes.returnUInt32)
                return aiLimit.ReadUInt32(address);

            return aiLimit.ReadFloat(address);
        }

        private void FreeWeaponUpgrades()
        {
            IntPtr address = GetAddress(AddressType.Player) + Offsets.playerWeaponMain;
            aiLimit.WriteUInt32(aiLimit.ResolvePointerChain(address, Offsets.weaponDefine) + Offsets.weaponLevelupCost, 0);
            aiLimit.WriteUInt32(aiLimit.ResolvePointerChain(address, Offsets.weaponDefine, Offsets.weaponLevelupItem, 0x10, 0x20) + 0x14, 0);
        }

        //
        // Player position code overwrites
        //

        readonly byte[] xPosOriginalCode = new byte[] { 0x0F, 0x11, 0x81, 0xF0, 0x01, 0x00, 0x00 };         // movups [rcx+1F0],xmm0
        readonly byte[] zPosOriginalCode = new byte[] { 0xF2, 0x0F, 0x11, 0x89, 0x00, 0x02, 0x00, 0x00 };   // movsd [rcx+200],xmm1

        public (double, double, double) GetPlayerPosition()
        {
            IntPtr address = aiLimit.ResolvePointerChain(GetAddress(AddressType.Player) + 0xA8, 0x10, 0x80);

            aiLimit.ReadDouble(address + Offsets.playerX);

            return (aiLimit.ReadDouble(address + Offsets.playerX),
                    aiLimit.ReadDouble(address + Offsets.playerY),
                    aiLimit.ReadDouble(address + Offsets.playerZ));
        }

        public void SetPlayerPosition(double x, double y, double z)
        {
            IntPtr address = aiLimit.ResolvePointerChain(GetAddress(AddressType.Player) + 0xA8, 0x10, 0x80);

            if (!AddressInRange(address))
                return;

            Thread SetPosition = new Thread(() => SetPlayerPositionThread(address, x, y, z));
            SetPosition.Start();
        }

        // Overwrites code that sets the player position before writing new position.
        // Sets new location, waits so that the engine doesn't immediately overwrite it, then changes the code back.
        private void SetPlayerPositionThread(IntPtr address, double x, double y, double z)
        {
            aiLimit.WriteProtectedMemory(xPosCodeAddress, EmptyCode(7));
            aiLimit.WriteProtectedMemory(zPosCodeAddress, EmptyCode(8));

            aiLimit.WriteDouble(address + Offsets.playerX, x);
            aiLimit.WriteDouble(address + Offsets.playerY, y);
            aiLimit.WriteDouble(address + Offsets.playerZ, z);

            Thread.Sleep(50);

            aiLimit.WriteProtectedMemory(xPosCodeAddress, xPosOriginalCode);
            aiLimit.WriteProtectedMemory(zPosCodeAddress, zPosOriginalCode);
        }

        readonly byte[] infiniteConsumablesOriginalCode = new byte[] { 0x89, 0x41, 0x14 };        // mov [rcx+14],eax

        public void SetInfiniteConsumables(bool state)
        {
            if (state)
                aiLimit.WriteProtectedMemory(infiniteConsumablesCodeAddress, EmptyCode(3));
            else
                aiLimit.WriteProtectedMemory(infiniteConsumablesCodeAddress, infiniteConsumablesOriginalCode);
        }

        readonly byte[] passiveEnemiesOriginalCode = new byte[] { 0x48, 0x89, 0xBB, 0x30, 0x01, 0x00, 0x00 };   // mov [rbx+130],rdi

        // Overwrites the code that sets the target field of a monster.
        // Can be a bit jank. Find a better way.
        public void SetPassiveEnemies(bool state)
        {
            if (state)
                aiLimit.WriteProtectedMemory(passiveEnemiesCodeAddress, EmptyCode(7));
            else
                aiLimit.WriteProtectedMemory(passiveEnemiesCodeAddress, passiveEnemiesOriginalCode);
        }

        //
        // Game state stuff
        //

        public enum StateTypes
        {
            GameState,
            MonsterState,
        }

        public int SetState(uint state, StateTypes stateType, int newValue = -1, int levelID = -1)
        {
            int offset = 0x10;

            if (stateType == StateTypes.MonsterState)
                offset = 0x18;

            IntPtr address = GetAddress(AddressType.ArchiveData) + 0x38;

            //IntPtr statesBase = aiLimit.ResolvePointerChain(archiveDataBase, 0x38, offset, 0x10) + 0x20;
            //uint statesSize = aiLimit.ReadUInt32(aiLimit.ResolvePointerChain(archiveDataBase, 0x38, offset) + 0x18);

            IntPtr statesBase = aiLimit.ResolvePointerChain(address, offset, 0x10) + 0x20;
            uint statesSize = aiLimit.ReadUInt32(aiLimit.ResolvePointerChain(address, offset) + 0x18);

            if (statesSize > 2000) // safety, not sure what actual limit is
                statesSize = 2000;

            for (int i = 0; i < statesSize; i++)
            {
                uint currentLevelID = 0;
                if (stateType == StateTypes.MonsterState)
                    currentLevelID = aiLimit.ReadUInt32((IntPtr)aiLimit.ReadUInt64(statesBase + (i * 8)) + 0x18);

                if (aiLimit.ReadUInt32((IntPtr)aiLimit.ReadUInt64(statesBase + (i * 8)) + 0x10) == state)
                {
                    if (stateType == StateTypes.GameState || (stateType == StateTypes.MonsterState && levelID == currentLevelID))
                    {
                        if (newValue > -1)
                            aiLimit.WriteByte((IntPtr)aiLimit.ReadUInt64(statesBase + (i * 8)) + 0x14, (byte)newValue);

                        return (int)aiLimit.ReadByte((IntPtr)aiLimit.ReadUInt64(statesBase + (i * 8)) + 0x14);
                    }
                }
            }
            return -1;
        }

        private uint[,] states = new uint[2, 2000];             // Holds existing values of states [Game, Monster]
        private uint[] oldStatesListSize = new uint[2] {0, 0};  // Size of statelist is dynamic, track size so statelog doesn't get spammed with rubbish when list size is updated {Game, Monster}

        // Scan through all states and save current value. Returns values that have changed since last scan.
        public string CheckStateData(StateTypes stateType)
        {
            int offset = 0x10;
            int index = 0;

            if (stateType == StateTypes.MonsterState)
            {
                offset = 0x18;
                index = 1;
            }

            IntPtr address = GetAddress(AddressType.ArchiveData) + 0x38;

            //IntPtr statesBase = ResolvePointerChain(archiveDataBase, 0x38, offset, 0x10) + 0x20;
            //uint statesSize = ReadUInt32(ResolvePointerChain(archiveDataBase, 0x38, offset) + 0x18);
            IntPtr statesBase = aiLimit.ResolvePointerChain(address, offset, 0x10) + 0x20;
            uint statesSize = aiLimit.ReadUInt32(aiLimit.ResolvePointerChain(address, offset) + 0x18);

            string returnData = "";

            if (statesSize > 2000) // safety
                statesSize = 2000;

            for (int i = 0; i < statesSize; i++)
            {
                uint newValue = (uint)aiLimit.ReadByte((IntPtr)aiLimit.ReadUInt64(statesBase + (i * 8)) + 0x14);
                uint levelID = aiLimit.ReadUInt32((IntPtr)aiLimit.ReadUInt64(statesBase + (i * 8)) + 0x18);
                if (newValue != states[index, i])
                {
                    if (i < oldStatesListSize[index])
                    {
                        if (stateType == StateTypes.GameState)
                            returnData += "[Game] ID: " + aiLimit.ReadUInt32((IntPtr)aiLimit.ReadUInt64(statesBase + (i * 8)) + 0x10) + " New value: " + newValue + " (" + states[index, i] + ") [" + i + "]\n";
                        else
                            returnData += "[Monster] ID: " + aiLimit.ReadUInt32((IntPtr)aiLimit.ReadUInt64(statesBase + (i * 8)) + 0x10) + " Level: " + levelID + " New value: " + newValue + " (" + states[index, i] + ") [" + i + "]\n";
                    }
                    states[index, i] = newValue;
                }
            }
            oldStatesListSize[index] = statesSize;
            return returnData;
        }

        public enum Boss
        {
            SewerCleaner,
            Lore,
            Patriarch,
            NecroPanic,
            Pardoner,
            HunterSquad,
            CleansingKnight,
            Saint,
            Choirmaster,
            Hunter,
            Persephone,
            NecroWanderer,
            Colossaint,
            Eunomia,

            Ursula,
            Guardians,

            Absolver,
            BossRush,
            Vikas,
            Loskid,
            Aether,
            Charon,
        }

        // States used to respawn bosses. (StateType, level, stateID)
        private Dictionary<Boss, List<(StateTypes, int, uint)>> bossRespawnStates = new Dictionary<Boss, List<(StateTypes, int, uint)>> ()
        {
            { Boss.SewerCleaner,    new List<(StateTypes, int, uint)>() {       (StateTypes.MonsterState, 10101, 31),
                                                                                (StateTypes.MonsterState, 10101, 32)    }},
            { Boss.Lore,            new List<(StateTypes, int, uint)>() {       (StateTypes.MonsterState, 10101, 1),
                                                                                (StateTypes.MonsterState, 10101, 173)   }},
            { Boss.Patriarch,       new List<(StateTypes, int, uint)>() {       (StateTypes.MonsterState, 10201, 1)     }},
            { Boss.NecroPanic,      new List<(StateTypes, int, uint)>() {       (StateTypes.MonsterState, 10201, 3)     }},
            { Boss.Pardoner,        new List<(StateTypes, int, uint)>() {       (StateTypes.MonsterState, 10301, 639)   }},
            { Boss.HunterSquad,     new List<(StateTypes, int, uint)>() {       (StateTypes.MonsterState, 10903, 201),
                                                                                (StateTypes.MonsterState, 10903, 202),
                                                                                (StateTypes.MonsterState, 10903, 203),
                                                                                (StateTypes.MonsterState, 10903, 204),  }},
            { Boss.CleansingKnight, new List<(StateTypes, int, uint)>() {       (StateTypes.MonsterState, 10302, 226)   }},
            { Boss.Saint,           new List<(StateTypes, int, uint)>() {       (StateTypes.MonsterState, 10401, 2)     }},
            { Boss.Choirmaster,     new List<(StateTypes, int, uint)>() {       (StateTypes.MonsterState, 10501, 1)     }},
            { Boss.Hunter,          new List<(StateTypes, int, uint)>() {       (StateTypes.MonsterState, 10601, 50)    }},
            { Boss.Persephone,      new List<(StateTypes, int, uint)>() {       (StateTypes.MonsterState, 10701, 1),
                                                                                (StateTypes.MonsterState, 10701, 2)     }},
            { Boss.NecroWanderer,   new List<(StateTypes, int, uint)>() {       (StateTypes.MonsterState, 10401, 120)   }},
            { Boss.Colossaint,      new List<(StateTypes, int, uint)>() {       (StateTypes.MonsterState, 10401, 1)     }},
            { Boss.Eunomia,         new List<(StateTypes, int, uint)>() {       (StateTypes.MonsterState, 10401, 116),
                                                                                (StateTypes.MonsterState, 10401, 117),
                                                                                (StateTypes.MonsterState, 10401, 215),
                                                                                (StateTypes.GameState,    0,  360010),
                                                                                (StateTypes.GameState,    0,  360022)   }},
            { Boss.Ursula,          new List<(StateTypes, int, uint)>() {       (StateTypes.MonsterState, 10202, 1),
                                                                                (StateTypes.MonsterState, 10202, 2)     }},
            { Boss.Guardians,       new List<(StateTypes, int, uint)>() {       (StateTypes.MonsterState, 10801, 28),
                                                                                (StateTypes.MonsterState, 10801, 29),
                                                                                (StateTypes.MonsterState, 10801, 30),
                                                                                (StateTypes.MonsterState, 10801, 31),
                                                                                (StateTypes.MonsterState, 10801, 32),
                                                                                (StateTypes.MonsterState, 10801, 33),   }},
            { Boss.Absolver,        new List<(StateTypes, int, uint)>() {       (StateTypes.MonsterState, 10502, 231)   }},
            { Boss.BossRush,        new List<(StateTypes, int, uint)>() {       (StateTypes.MonsterState, 10502, 256),
                                                                                (StateTypes.GameState,    0,  360023),
                                                                                (StateTypes.GameState,    0,  360502)   }},
            { Boss.Vikas,           new List<(StateTypes, int, uint)>() {       (StateTypes.GameState,    0,  399998),
                                                                                (StateTypes.GameState,    0,  300515),
                                                                                (StateTypes.GameState,    0,  200357),
                                                                                (StateTypes.GameState,    0,  300518)   }},
            { Boss.Loskid,          new List<(StateTypes, int, uint)>() {       (StateTypes.MonsterState, 10504, 1),
                                                                                (StateTypes.MonsterState, 10504, 2)     }},
            { Boss.Aether,          new List<(StateTypes, int, uint)>() {       (StateTypes.MonsterState, 10902, 1),
                                                                                (StateTypes.MonsterState, 10902, 100),
                                                                                (StateTypes.GameState,    0,  360014),
                                                                                (StateTypes.GameState,    0,  200900),
                                                                                (StateTypes.GameState,    0,  333333)   }},
            { Boss.Charon,          new List<(StateTypes, int, uint)>() {       (StateTypes.MonsterState, 10902, 3),
                                                                                (StateTypes.MonsterState, 10902, 4),
                                                                                (StateTypes.GameState,    0,  360015),
                                                                                (StateTypes.GameState,    0,  333333)   }},
        };

        public void RespawnBoss(Boss boss)
        {
            List<(StateTypes, int, uint)> states;

            bossRespawnStates.TryGetValue(boss, out states);

            foreach((StateTypes stateType, int level, uint state) entry in states)
            {
                int value = 0;

                // A few gamestates need to be set to something other than 0
                if (entry.state == 360502) // Boss rush
                    value = 1;
                if (entry.state == 399998) // Vikas quest progression
                    value = 7;
                SetState(entry.state, entry.stateType, value, entry.level);
            }

        }

        //
        // Items / shop handlers
        //

        public bool SetShopItem(uint shopID, uint itemID, uint itemCategory)
        {
            IntPtr address = aiLimit.ResolvePointerChain(GetAddress(AddressType.ArchiveData) + Offsets.ShopSystemData, 0x10);
            uint size = aiLimit.ReadUInt32(address + 0x18);
            address = (IntPtr)aiLimit.ReadUInt64(address + 0x10) + 0x20;

            if (size > 20)
                size = 20;

            for (int i = 0; i < size; i++)
            {
                IntPtr currentAddress = (IntPtr)aiLimit.ReadUInt64(address + (i * 8));

                if (aiLimit.ReadUInt32( currentAddress + 0x10 ) == shopID)
                {
                    currentAddress = aiLimit.ResolvePointerChain(currentAddress + 0x18, 0x10, 0x20);

                    aiLimit.WriteUInt32(currentAddress + 0x14, itemID);                                             // Item
                    aiLimit.WriteUInt32(currentAddress + 0x18, 0);                                                  // Number of items bought
                    aiLimit.WriteUInt32(aiLimit.ResolvePointerChain(currentAddress + 0x28) + 0x18, itemCategory);   // Category
                    aiLimit.WriteUInt32(aiLimit.ResolvePointerChain(currentAddress + 0x28) + 0x20, 0xFFFFFFFF);     // Number in stock (0xFFFFFFFF is infinite)
                    aiLimit.WriteUInt32(aiLimit.ResolvePointerChain(currentAddress + 0x28) + 0x24, 0);              // Price

                    return true;
                }
            }
            return false;
        }
    }

    public enum ConnectionState
    {
        NotConnected,
        ProcessNotFound,
        ConnectedOffsetsNotFound,
        Connected,
        ConnectionLost,
    }

    public enum AddressType
    {
        GameAssembly,
        UnityPlayer,
        ArchiveData,
        Player,
        CurrentView,
        LevelRoot,
        TransferDestination,
        Timer,
    }
}
