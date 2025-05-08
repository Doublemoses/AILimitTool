using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.Dynamic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Windows.Media;
using System.Windows.Shapes;

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
        public const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
        const string aiLimitProcessName = "ai-limit";
        private Process _aiLimitProcess = null;
        private IntPtr _aiLimitProcessHandle = IntPtr.Zero;
        public IntPtr unityPlayerBaseAddress = IntPtr.Zero;
        int unityPlayerSize = 0;
        public IntPtr gameAssemblyBaseAddress = IntPtr.Zero;
        int gameAssemblySize = 0;

        public bool linkActive = false;
        public bool modulesFound = false;
        public bool mainObjectsFound = false;

        public bool loadingScreenActive = false;

        const ulong ADDRESS_MINIMUM = 0x10000000000;
        const ulong ADDRESS_MAXIMUM = 0x6FFFFFFFFFF;

        System.Windows.Threading.DispatcherTimer monitorTimer = new System.Windows.Threading.DispatcherTimer();

        bool disposed = false;
        //Thread erMonitorThread = null;

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint dwDesiredAcess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int iSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, ref int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        [DllImport("kernel32.dll")]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);


        // test


        


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
            InitGameLink();

            monitorTimer.Tick += monitorTimer_Tick;
            monitorTimer.Interval = TimeSpan.FromMilliseconds(200);
            monitorTimer.Start();
        }

        public void InitGameLink()
        {
            if (AttachProcess())
            {
                LocateModules();
                IdentifyGameVersion();
                linkActive = true;
            }
            else
            {
                linkActive = false;
            }
        }

        private bool AttachProcess()
        {
            var processList = Process.GetProcesses();

            foreach (var process in processList)
            {
                if (process.ProcessName.ToLower().Equals(aiLimitProcessName) && !process.HasExited)
                {
                    _aiLimitProcess = process;
                    _aiLimitProcessHandle = OpenProcess(PROCESS_ALL_ACCESS, bInheritHandle: false, _aiLimitProcess.Id);
                    return true;
                }
            }
            return false;
        }

        private bool waitingForLoadingScreenToEnd = false;

        private void monitorTimer_Tick(object? sender, EventArgs e)
        {
            if (linkActive)
            {
                try
                {
                    Process aiLimitProcess = Process.GetProcessById(_aiLimitProcess.Id);
                }
                catch (Exception f)
                {
                    linkActive = false;
                    modulesFound = false;
                    mainObjectsFound = false;
                    version = GameVersion.vNotFound;
                }
            }
            else
            {
                InitGameLink();
            }

            if (linkActive)
            {
                if (!modulesFound)
                {
                    LocateModules();
                    IdentifyGameVersion();
                }

                if (!mainObjectsFound)
                {
                    mainObjectsFound = LocateMainObjects();

                    if (mainObjectsFound)
                    {
                        WriteFloat((IntPtr)ReadUInt64(loadingViewBase + 0x50) + 0x28, 0.6f);
                        WriteFloat((IntPtr)ReadUInt64(loadingViewBase + 0x50) + 0x2C, 0.6f);
                        WriteFloat((IntPtr)ReadUInt64(loadingViewBase + 0x50) + 0x30, 0.8f);
                    }
                }

                if (mainObjectsFound)
                {
                    double value;

                    // Reapply options that must be constantly set
                    if (activeOptions.TryGetValue(GameOptions.LockTargetHP, out value))
                    {
                        SetTargetMonsterValue(MonsterStats.HPPercent, false, value);
                    }

                    if (activeOptions.ContainsKey(GameOptions.FreeUpgrade))
                    {
                        FreeWeaponUpgrades();
                    }

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
        }

        private void LocateModules()
        {
            bool gameAssemblyFound = false;
            bool unityPlayerfound = false;

            try
            {
                Debug.Print("Seeking modules...");
                foreach (var module in _aiLimitProcess.Modules)
                {
                    var processModule = module as ProcessModule;
                    var moduleName = processModule.ModuleName.ToLower();

                    if (moduleName == "gameassembly.dll")
                    {
                        gameAssemblyBaseAddress = processModule.BaseAddress;
                        gameAssemblySize = processModule.ModuleMemorySize;
                        gameAssemblyFound = true;
                    }

                    if (moduleName == "unityplayer.dll")
                    {
                        unityPlayerBaseAddress = processModule.BaseAddress;
                        unityPlayerSize = processModule.ModuleMemorySize;
                        unityPlayerfound = true;
                    }

                    if (gameAssemblyFound && unityPlayerfound)
                    {
                        Debug.Print("Modules found.");
                        modulesFound = true;
                        return;
                    }
                }
            }
            catch (Exception f) { MessageBox.Show("Locate modules error - " + f.ToString()); }

        }

        IntPtr archiveDataBase = 0;
        IntPtr playerBase = 0;
        IntPtr loadingViewBase = 0;
        IntPtr levelRootBase = 0;
        IntPtr transferDestination = 0;
        
        IntPtr viewManagerBase = 0;

        IntPtr timerAddress = 0;
        IntPtr xPosCodeAddress = 0;
        IntPtr zPosCodeAddress = 0;

        public GameVersion version = GameVersion.vNotFound;
        public Dictionary<uint, bool> addressFound = new Dictionary<uint, bool>();

        public enum GameVersion
        {
            v1_0_020a, // three versions from release day
            v1_0_020b, // I don't know the actual versions, so assuming 1.0.020 
            v1_0_020c, // and added letters to separate different versions
            v1_0_021,
            v1_0_022,
            vNotFound = 100,
        }

        private void IdentifyGameVersion()
        {
            switch (gameAssemblySize)
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
            }
        }

        // Stable pointers that should get set once and never change while game is running
        // GameAssembly.dll pointers will change every patch
        // UnityPlayer.dll pointers are stable across all versions
        private bool LocateMainObjects()
        {
            Debug.Print(version.ToString() + " " + gameAssemblySize + " Seeking object addresses...");

            if (version == GameVersion.v1_0_020b)
            {
                archiveDataBase = ResolvePointerChain(gameAssemblyBaseAddress + 0x042C9650, 0xB8, 0x0) + 0x18;
                playerBase = ResolvePointerChain(gameAssemblyBaseAddress + 0x042D4FF0, 0xB8) + 0xB40;
                loadingViewBase = ResolvePointerChain(gameAssemblyBaseAddress + 0x042CCF10, 0xB8, 0x0, 0x48, 0x78, 0x48);
                levelRootBase = ResolvePointerChain(gameAssemblyBaseAddress + 0x04330E68, 0xB8, 0x10) + 0x38;
                transferDestination = ResolvePointerChain(gameAssemblyBaseAddress + 0x042FFC20, 0xB8) + 0x1C;
            }
            else if (version == GameVersion.v1_0_020c)
            {
                archiveDataBase = ResolvePointerChain(gameAssemblyBaseAddress + 0x04350F70, 0xB8, 0x0) + 0x18;
                playerBase = ResolvePointerChain(gameAssemblyBaseAddress + 0x0435C030, 0xB8) + 0xB40;
                loadingViewBase = ResolvePointerChain(gameAssemblyBaseAddress + 0x04353F78, 0xB8, 0x0, 0x48, 0x78, 0x48);
                levelRootBase = ResolvePointerChain(gameAssemblyBaseAddress + 0x04330DF0, 0xB8, 0x10) + 0x38;
                transferDestination = ResolvePointerChain(gameAssemblyBaseAddress + 0x042FFBC8, 0xB8) + 0x1C;
            }
            else if (version == GameVersion.v1_0_021)
            {
                archiveDataBase = ResolvePointerChain(gameAssemblyBaseAddress + 0x042DBAE8, 0xB8, 0x0) + 0x18;
                playerBase = ResolvePointerChain(gameAssemblyBaseAddress + 0x042E73C8, 0xB8) + 0xB40;
                loadingViewBase = ResolvePointerChain(gameAssemblyBaseAddress + 0x042DF2E8, 0xB8, 0x0, 0x48, 0x78, 0x48);
                levelRootBase = ResolvePointerChain(gameAssemblyBaseAddress + 0x043434B8, 0xB8, 0x10) + 0x38;
                transferDestination = ResolvePointerChain(gameAssemblyBaseAddress + 0x04312158, 0xB8) + 0x1C;
            }
            else if (version == GameVersion.v1_0_022)
            {
                archiveDataBase = ResolvePointerChain(gameAssemblyBaseAddress + 0x042DDBC0, 0xB8, 0x0) + 0x18;
                playerBase = ResolvePointerChain(gameAssemblyBaseAddress + 0x042E94A0, 0xB8) + 0xB40;
                loadingViewBase = ResolvePointerChain(gameAssemblyBaseAddress + 0x042E13C0, 0xB8, 0x0, 0x48, 0x78, 0x48);
                levelRootBase = ResolvePointerChain(gameAssemblyBaseAddress + 0x04345600, 0xB8, 0x10) + 0x38;
                transferDestination = ResolvePointerChain(gameAssemblyBaseAddress + 0x04314268, 0xB8) + 0x1C;
                //viewManagerBase = ResolvePointerChain(gameAssemblyBaseAddress + 0x04377100, 0xB8, 0x0); // Not used yet.
            }
            else
            {
                return false;
            }

            timerAddress = ResolvePointerChain(unityPlayerBaseAddress + 0x01CA3978) + 0x60;
            xPosCodeAddress = unityPlayerBaseAddress + 0x0140227C;
            zPosCodeAddress = unityPlayerBaseAddress + 0x01402288;

            //Debug.Print((archiveDataBase > 0x100000).ToString() + (playerBase > 0x100000).ToString() + (loadingViewBase > 0x100000).ToString() + (levelRootBase > 0x100000).ToString());

            return archiveDataBase > 0x100000
                   && playerBase > 0x100000
                   && loadingViewBase > 0x100000
                   && levelRootBase > 0x100000
                   && transferDestination > 0x100000
                   && timerAddress > 0x100000;
        }

        public enum AddressTypes
        {
            GameAssembly,
            UnityPlayer,
            ArchiveData,
            Player,
            LoadingView,
            LevelRoot,
            TransferDestination,
            Timer,
        }

        public IntPtr GetAddress(AddressTypes type)
        {
            switch (type)
            {
                case AddressTypes.GameAssembly:
                    return gameAssemblyBaseAddress;
                case AddressTypes.UnityPlayer:
                    return unityPlayerBaseAddress;
                case AddressTypes.ArchiveData:
                    return archiveDataBase;
                case AddressTypes.Player:
                    return playerBase;
                case AddressTypes.LoadingView:
                    return loadingViewBase;
                case AddressTypes.LevelRoot:
                    return levelRootBase;
                case AddressTypes.TransferDestination:
                    return transferDestination;
                case AddressTypes.Timer:
                    return timerAddress;
            }
            return 0;
        }

        IntPtr ResolvePointerChain(params nint[] pointers)
        {
            IntPtr pointer = (IntPtr)ReadUInt64(pointers[0]);

            for (int i = 1; i < pointers.Length; i++)
            {
                pointer = (IntPtr)ReadUInt64(pointer + pointers[i]);
            }
            return pointer;
        }

        byte[] ReadMemory(IntPtr address, int size)
        {

            var data = new byte[size];
            var i = 1;
            ReadProcessMemory(_aiLimitProcessHandle, address, data, size, ref i);
            return data;

        }
        byte ReadByte(IntPtr address)
        {
            return ReadMemory(address, 1)[0];
        }

        uint ReadUInt32(IntPtr address)
        {
            var data = ReadMemory(address, 4);
            return BitConverter.ToUInt32(data, 0);
        }

        ulong ReadUInt64(IntPtr address)
        {
            var data = ReadMemory(address, 8);
            return BitConverter.ToUInt64(data, 0);
        }

        float ReadFloat(IntPtr address)
        {
            var bytes = ReadMemory(address, 4);
            return BitConverter.ToSingle(bytes, 0);
        }

        double ReadDouble(IntPtr address)
        {
            var bytes = ReadMemory(address, 8);
            return BitConverter.ToDouble(bytes, 0);
        }

        string ReadString(IntPtr address, int length)
        {
            if (length > 32) { length = 32; }
            if (length < 0) { length = 1; }
            return Encoding.Unicode.GetString(ReadMemory(address, length * 2));
        }

        //
        // WRITE 
        //
        public void WriteMemory(IntPtr address, byte[] data)
        {
            int i = 0;
            //NtWriteVirtualMemory(_aiLimitProcessHandle, address, data, (uint)data.Length, ref i);
            WriteProcessMemory(_aiLimitProcessHandle, address, data, data.Length, ref i);
        }

        public void WriteByte(IntPtr address, byte data)
        {
            var bytes = new byte[] { data };
            WriteMemory(address, bytes);
        }

        public void WriteUInt32(IntPtr address, uint data)
        {
            WriteMemory(address, BitConverter.GetBytes(data));
        }

        public void WriteFloat(IntPtr address, float data)
        {
            WriteMemory(address, BitConverter.GetBytes(data));
        }

        public void WriteDouble(IntPtr address, double data)
        {
            WriteMemory(address, BitConverter.GetBytes(data));
        }

        // Generates empty machine code that does nothing. Data is the default value.
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

        private bool AddressInRange(ulong address)
        {
            if (address < ADDRESS_MINIMUM || address > ADDRESS_MAXIMUM)
                return false;
            return true;
        }

        //
        // Loading data
        //
        public bool IsLoadingScreenActive()
        {
            if (ReadByte(loadingViewBase + Offsets.isLoading) == 0)
                return false;
            return true;
        }

        public string GetCurrentLevelName()
        {
            return ReadString(ResolvePointerChain(levelRootBase, Offsets.levelName) + 0x14,
                (int)ReadUInt32(ResolvePointerChain(levelRootBase, Offsets.levelName) + 0x10));
        }

        public uint GetCurrentLevelID()
        {
            return ReadUInt32(ResolvePointerChain(levelRootBase, Offsets.levelModel) + 0x10);
        }

        public double GetTimer()
        {
            return ReadDouble((IntPtr)timerAddress);
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
        public uint SetPlayerStats(PlayerStats option, int newValue = -1, bool add = false)
        {

            IntPtr address = (IntPtr)ReadUInt64(archiveDataBase);

            if ((int)option < 5) // player stats 0-4 are player stats in ArchiveData -> _PlayerPoint
            {
                address = ResolvePointerChain(archiveDataBase, Offsets.playerStats, 0x10 + ((int)option * 8)) + 0x10;
            }
            else
            {
                switch (option)
                {
                    case PlayerStats.PlayerLevel:
                        address += Offsets.playerLevel;
                        break;
                    case PlayerStats.Crystals:
                        address += Offsets.crystals;
                        break;
                    case PlayerStats.TransferDestination: // Do not call this directly, go via SetTransferDestination first, or results will be unreliable
                        address += Offsets.transferDestination;
                        break;
                }
            }

            if (!AddressInRange((ulong)address))
                return 0;

            if (newValue > -1)
            {
                if (add)
                {
                    WriteUInt32(address, ReadUInt32(address) + (uint)newValue);
                }
                else
                {
                    WriteUInt32(address, (uint)newValue);
                    return (uint)newValue;
                }
            }

            return ReadUInt32(address);
        }

        enum ReturnTypes
        {
            returnByte,
            returnUInt32,
            returnFloat,
        }

        public bool TogglePlayerBools(GameOptions option, bool value)
        {
            IntPtr address = (IntPtr)ReadUInt64(playerBase);

            switch (option)
            {
                case GameOptions.Immortal:
                    address += Offsets.playerImmortal;
                    break;
                case GameOptions.LockSync:
                    address += Offsets.playerLockSync;
                    break;
            }

            if (!AddressInRange((ulong)address))
                return false;

            if (value)
                activeOptions.TryAdd(option, 0);
            else
                activeOptions.Remove(option);

            WriteByte(address, Convert.ToByte(value));
            return true;
        }

        public void SetPlayerSpeed(float speed)
        {
            IntPtr address = (IntPtr)ReadUInt64(playerBase) + Offsets.playerDefine;
            address = (IntPtr)ReadUInt64(address) + Offsets.playerSpeed;

            if (!AddressInRange((ulong)address))
                return;

            WriteFloat(address, speed);
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
            IntPtr address = ResolvePointerChain(playerBase, Offsets.playerTarget);

            if (address != 0)
            {
                previousTargetAddress = address;
            }

            if (address == 0 && usePreviousAddressIfEmpty)
            {
                if (previousTargetAddress == 0)
                {
                    return 0;
                }

                address = previousTargetAddress;
            }

            return SetMonsterValue(stat, address, newValue);
        }

        private double SetMonsterValue(MonsterStats option, IntPtr monsterAddress, double newValue = -1)
        {
            IntPtr playerTarget = ResolvePointerChain(playerBase) + Offsets.playerTarget;
            IntPtr actorModelBase = monsterAddress + Offsets.actorModelMonster;
            IntPtr address = 0;

            ReturnTypes returnType = ReturnTypes.returnFloat;

            switch (option)
            {
                case MonsterStats.HP:
                    address = ResolvePointerChain(actorModelBase, Offsets.actorModelHP) + 0x10;
                    break;
                case MonsterStats.HPMax:
                    address = ResolvePointerChain(actorModelBase, Offsets.actorModelHPMax) + 0x10;
                    break;
                case MonsterStats.Sync:
                    address = ResolvePointerChain(actorModelBase, Offsets.actorModelSync) + 0x10;
                    break;
                case MonsterStats.HPPercent: // value returned wont be a %, but assume hp% will never be used
                    address = ResolvePointerChain(actorModelBase, Offsets.actorModelHP) + 0x10;
                    newValue = ReadFloat(ResolvePointerChain(actorModelBase, Offsets.actorModelHPMax) + 0x10) * (newValue / 100);
                    break;
                case MonsterStats.PoisonAccumulation:
                    address = ResolvePointerChain(actorModelBase, Offsets.actorModelPoisonAccumulation) + 0x10;
                    break;
                case MonsterStats.PiercingAccumulation:
                    address = ResolvePointerChain(actorModelBase, Offsets.actorModelPiercingAccumulation) + 0x10;
                    break;
                case MonsterStats.InfectionAccumulation:
                    address = ResolvePointerChain(actorModelBase, Offsets.actorModelInfectionAccumulation) + 0x10;
                    break;

                case MonsterStats.PhysicalDefense:
                    address = ResolvePointerChain(actorModelBase, Offsets.actorModelPhysicalDefense) + 0x10;
                    break;
                case MonsterStats.ElectricDefense:
                    address = ResolvePointerChain(actorModelBase, Offsets.actorModelElectricDefense) + 0x10;
                    break;
                case MonsterStats.ShatterDefense:
                    address = ResolvePointerChain(actorModelBase, Offsets.actorModelShatterDefense) + 0x10;
                    break;
                case MonsterStats.FireDefense:
                    address = ResolvePointerChain(actorModelBase, Offsets.actorModelFireDefense) + 0x10;
                    break;

                case MonsterStats.PoisonResist:
                    address = ResolvePointerChain(actorModelBase, Offsets.actorModelPoisonResist) + 0x10;
                    break;
                case MonsterStats.PiercingResist:
                    address = ResolvePointerChain(actorModelBase, Offsets.actorModelPiercingResist) + 0x10;
                    break;
                case MonsterStats.InfectionResist:
                    address = ResolvePointerChain(actorModelBase, Offsets.actorModelInfectionResist) + 0x10;
                    break;

                case MonsterStats.SuperArmourLevel:
                    address = ResolvePointerChain(actorModelBase, Offsets.actorModelSuperArmourLevel) + 0x10;
                    returnType = ReturnTypes.returnUInt32;
                    break;

                case MonsterStats.TenacityDecreaseTick:
                    address = monsterAddress + Offsets.monsterTenacityDecreaseTick;
                    break;
                case MonsterStats.Tenacity:
                    address = (IntPtr)ReadUInt64(monsterAddress + Offsets.monsterTenacity) + 0x10;
                    break;
                case MonsterStats.TenacityMax:
                    address = (IntPtr)ReadUInt64(monsterAddress + Offsets.monsterTenacityMax) + 0x10;
                    returnType = ReturnTypes.returnUInt32;
                    break;
                default:
                    return -1;
            }

            if (!AddressInRange((ulong)address))
                return 0;

            // Assume we never need to manually set TenacityMax value, as it is the only field that isn't a float.
            if (newValue > -1)
                WriteFloat(address, (float)newValue);

            if (returnType == ReturnTypes.returnUInt32)
                return ReadUInt32(address);

            return ReadFloat(address);
        }

        // Must be written in two places, or it is inconsistent. Race condition?
        public void SetTransferDestination(int newDestination)
        {
            if (!AddressInRange((ulong)transferDestination))
                return;

            WriteUInt32(transferDestination, (uint)newDestination);
            SetPlayerStats(PlayerStats.TransferDestination, newDestination);
        }

        public void InfiniteDew(bool value)
        {
            uint size = ReadUInt32(ResolvePointerChain(archiveDataBase, 0x40, 0x10) + 0x18);

            if (size > 1000) // safety in case bad data is read and returns absurdly huger number
                return;

            for (int i = 0; i < size; i++)
            {
                uint itemID = ReadUInt32(ResolvePointerChain(archiveDataBase, 0x40, 0x10, 0x10, 0x20 + (i * 0x8)) + 0x10);
                if (itemID == 1)
                {
                    WriteByte(ResolvePointerChain(archiveDataBase, 0x40, 0x10, 0x10, 0x20 + (i * 0x8), 0x28) + 0x3C, Convert.ToByte(value));
                    return;
                }
            }
        }

        private void FreeWeaponUpgrades()
        {
            WriteUInt32(ResolvePointerChain(playerBase, Offsets.playerWeaponMain, Offsets.weaponDefine) + Offsets.weaponLevelupCost, 0);
            WriteUInt32(ResolvePointerChain(playerBase, Offsets.playerWeaponMain, Offsets.weaponDefine, Offsets.weaponLevelupItem, 0x10, 0x20) + 0x14, 0);
        }

        //
        // Player position stuff
        //

        readonly byte[] xPosOriginalCode = new byte[]  { 0x0F, 0x11, 0x81, 0xF0, 0x01, 0x00, 0x00 };        // movups [rcx+000001F0],xmm0
        readonly byte[] zPosOriginalCode = new byte[]  { 0xF2, 0x0F, 0x11, 0x89, 0x00, 0x02, 0x00, 0x00 };  // movsd [rcx+00000200],xmm1

        public (double, double, double) GetPlayerPosition()
        {
            IntPtr address = ResolvePointerChain(playerBase, 0xA8, 0x10, 0x80);

            ReadDouble(address + Offsets.playerX);

            return (ReadDouble(address + Offsets.playerX),
                    ReadDouble(address + Offsets.playerY),
                    ReadDouble(address + Offsets.playerZ));
        }

        public void SetPlayerPosition(double x, double y, double z)
        {
            IntPtr address = ResolvePointerChain(playerBase, 0xA8, 0x10, 0x80);

            if (!AddressInRange((ulong)address))
                return;

            Thread SetPosition = new Thread(() => SetPlayerPositionThread(address, x, y, z));
            SetPosition.Start();
        }

        // Overwrites code that sets the player position before writing new position.
        // Sets new location, waits so that the engine doesn't immediately overwrite it, then changes the code back
        private void SetPlayerPositionThread(IntPtr address, double x, double y, double z)
        {
            WriteMemory(xPosCodeAddress, EmptyCode(7));
            WriteMemory(zPosCodeAddress, EmptyCode(8));

            WriteDouble(address + Offsets.playerX, x);
            WriteDouble(address + Offsets.playerY, y);
            WriteDouble(address + Offsets.playerZ, z);

            Thread.Sleep(50);

            WriteMemory(xPosCodeAddress, xPosOriginalCode);
            WriteMemory(zPosCodeAddress, zPosOriginalCode);
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

            IntPtr statesBase = ResolvePointerChain(archiveDataBase, 0x38, offset, 0x10) + 0x20;
            uint statesSize = ReadUInt32(ResolvePointerChain(archiveDataBase, 0x38, offset) + 0x18);

            if (statesSize > 2000) // safety
                statesSize = 2000;

            for (int i = 0; i < statesSize; i++)
            {
                uint currentLevelID = 0;
                if (stateType == StateTypes.MonsterState)
                    currentLevelID = ReadUInt32((IntPtr)ReadUInt64(statesBase + (i * 8)) + 0x18);

                if (ReadUInt32((IntPtr)ReadUInt64(statesBase + (i * 8)) + 0x10) == state)
                {
                    if (stateType == StateTypes.GameState || (stateType == StateTypes.MonsterState && levelID == currentLevelID))
                    {
                        if (newValue > -1)
                            WriteByte((IntPtr)ReadUInt64(statesBase + (i * 8)) + 0x14, (byte)newValue);

                        return (int)ReadByte((IntPtr)ReadUInt64(statesBase + (i * 8)) + 0x14);
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

            IntPtr statesBase = ResolvePointerChain(archiveDataBase, 0x38, offset, 0x10) + 0x20;
            uint statesSize = ReadUInt32(ResolvePointerChain(archiveDataBase, 0x38, offset) + 0x18);
            string returnData = "";

            if (statesSize > 2000) // safety
                statesSize = 2000;

            for (int i = 0; i < statesSize; i++)
            {
                uint newValue = (uint)ReadByte((IntPtr)ReadUInt64(statesBase + (i * 8)) + 0x14);
                uint levelID = ReadUInt32((IntPtr)ReadUInt64(statesBase + (i * 8)) + 0x18);
                if (newValue != states[index, i])
                {
                    if (i < oldStatesListSize[index])
                    {
                        if (stateType == StateTypes.GameState)
                            returnData += "[Game] ID: " + ReadUInt32((IntPtr)ReadUInt64(statesBase + (i * 8)) + 0x10) + " New value: " + newValue + " (" + states[index, i] + ") [" + i + "]\n";
                        else
                            returnData += "[Monster] ID: " + ReadUInt32((IntPtr)ReadUInt64(statesBase + (i * 8)) + 0x10) + " Level: " + levelID + " New value: " + newValue + " (" + states[index, i] + ") [" + i + "]\n";
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
            IntPtr address = ResolvePointerChain(archiveDataBase, Offsets.ShopSystemData, 0x10);
            uint size = ReadUInt32(address + 0x18);
            address = (IntPtr)ReadUInt64(address + 0x10) + 0x20;

            if (size > 20)
                size = 20;

            for (int i = 0; i < size; i++)
            {
                IntPtr currentAddress = (IntPtr)ReadUInt64(address + (i * 8));

                if (ReadUInt32( currentAddress + 0x10 ) == shopID)
                {
                    currentAddress = ResolvePointerChain(currentAddress + 0x18, 0x10, 0x20);

                    WriteUInt32(currentAddress + 0x14, itemID);                                     // Item
                    WriteUInt32(currentAddress + 0x18, 0);                                          // Number of items bought
                    WriteUInt32(ResolvePointerChain(currentAddress + 0x28) + 0x18, itemCategory);   // Category
                    WriteUInt32(ResolvePointerChain(currentAddress + 0x28) + 0x20, 0xFFFFFFFF);     // Number in stock (0xFFFFFFFF is infinite)
                    WriteUInt32(ResolvePointerChain(currentAddress + 0x28) + 0x24, 0);              // Price

                    return true;
                }
            }
            return false;
        }
    }
}
