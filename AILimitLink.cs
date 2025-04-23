﻿using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AILimitTool
{

    static class Offsets
    {
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
        public const int actorModelSync = 0xB0; // internally labelled as Confidence

        public const int actorModelPhysicalDamage = 0x20;
        public const int actorModelPoisonDamage = 0x48;
        public const int actorModelPunctureDamage = 0x50;
        public const int actorModelInfectDamage = 0x58;

        public const int actorModelPhysicalDefense = 0x28;
        public const int actorModelElectricDefense = 0x30;
        public const int actorModelPsychoDefense = 0x38;
        public const int actorModelDimensionDefense = 0x40;

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
        public bool mainObjectsFound = false;

        public bool loadingScreenActive = false;

        const ulong ADDRESS_MINIMUM = 0x10000000000;
        const ulong ADDRESS_MAXIMUM = 0x2FFFFFFFFFF;

        System.Windows.Threading.DispatcherTimer monitorTimer = new System.Windows.Threading.DispatcherTimer();

        bool disposed = false;
        //Thread erMonitorThread = null;

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint dwDesiredAcess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int iSize, ref int lpNumberOfBytesRead);

        [DllImport("ntdll.dll")]
        static extern int NtWriteVirtualMemory(IntPtr ProcessHandle, IntPtr BaseAddress, byte[] Buffer, UInt32 NumberOfBytesToWrite, ref UInt32 NumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        [DllImport("kernel32.dll")]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);




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
                LocateBaseAddress();
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
                    mainObjectsFound = false;
                }
            }
            else
            {
                InitGameLink();
            }

            if (linkActive)
            {

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

        private void LocateBaseAddress()
        {
            try
            {
                foreach (var module in _aiLimitProcess.Modules)
                {
                    var processModule = module as ProcessModule;
                    var moduleName = processModule.ModuleName.ToLower();

                    if (moduleName == "gameassembly.dll")
                    {
                        gameAssemblyBaseAddress = processModule.BaseAddress;
                        gameAssemblySize = processModule.ModuleMemorySize;
                    }

                    if (moduleName == "unityplayer.dll")
                    {
                        unityPlayerBaseAddress = processModule.BaseAddress;
                        unityPlayerSize = processModule.ModuleMemorySize;
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Something went wrong."); }

        }

        IntPtr archiveDataBase = 0;
        IntPtr playerBase = 0;
        IntPtr loadingViewBase = 0;
        IntPtr levelRootBase = 0;
        IntPtr transferDestination = 0;
        IntPtr timerAddress = 0;

        public bool versionIdentificationFailure = false;
        public GameVersion version = GameVersion.vNotFound;
        public Dictionary<uint, bool> addressFound = new Dictionary<uint, bool>();

        private Dictionary<string, (int, int)> bossResetStates = new Dictionary<string, (int, int)>()
        {
            { "Sewer",      (31,    32) },
            { "Lore",       (1,     173) },
        };

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
        private bool LocateMainObjects()
        {
            IdentifyGameVersion();

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
                loadingViewBase = ResolvePointerChain(gameAssemblyBaseAddress + 0x043474C0, 0xB8, 0x8, 0xD0, 0x48, 0x18, 0x48);
                levelRootBase = ResolvePointerChain(gameAssemblyBaseAddress + 0x04345600, 0xB8, 0x10) + 0x38;
                transferDestination = ResolvePointerChain(gameAssemblyBaseAddress + 0x04314268, 0xB8) + 0x1C;
                
            }
            else
            {
                return false;
            }

            timerAddress = ResolvePointerChain(unityPlayerBaseAddress + 0x01CA3978) + 0x60;

            //MessageBox.Show(version.ToString());
            //MessageBox.Show(archiveDataBase.ToString("X12") + " " + playerBase.ToString("X12") + " " + loadingViewBase.ToString("X12") + " " + levelRootBase.ToString("X12") + " " + transferDestination.ToString("X12"));

            return archiveDataBase > 0x100000
                   && playerBase > 0x100000
                   && loadingViewBase > 0x100000
                   && levelRootBase > 0x100000
                   && transferDestination > 0x100000;
        }



        public uint RunThread(IntPtr address, uint timeout = 0xFFFFFFFF, IntPtr? param = null)
        {
            var thread = CreateRemoteThread(_aiLimitProcessHandle, IntPtr.Zero, 0, address, param ?? IntPtr.Zero, 0, IntPtr.Zero);
            var returnValue = WaitForSingleObject(thread, timeout);
            CloseHandle(thread); //return value unimportant
            return returnValue;
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
            uint i = 0;
            NtWriteVirtualMemory(_aiLimitProcessHandle, address, data, (uint)data.Length, ref i);
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
        // Getting and setting data
        //

        // depreciated. eventually get rid of everything here
        public enum DataTypes
        {
            // 100 - 199    Stats accessed through Player object, use GetValue()

            Immortal = 100,
            LockSync,
            PlayerSpeed,


            // 200 - 299    Stats accessed through Player -> ActorModel object
            HP = 200,
            HPMax,

            // 300 - 399    Stats accessed through Player -> Target (targetted enemy), use GetMonsterValue()
            TargetHP = 300,
            TargetHPMax,
            TargetHPPercent,
            TargetSync,
            TargetPoisonAccumulation,
            TargetPiercingAccumulation,
            TargetInfectionAccumulation,
            TargetTenacityDecreaseTick,
            TargetTenacity,
            TargetTenacityMax,
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

        // if add is set, newValue should be added to existing value, instead of replacing it
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

        public void SetPlayerImmortal(bool value)
        {

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

        public IntPtr previousAddress = 0;

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
            PsychoDefense,
            DimensionDefense,

            PoisonResist,
            PiercingResist,
            InfectionResist,

            SuperArmourLevel,
        }

        public double SetTargetMonsterValue(MonsterStats stat, bool usePreviousAddressIfEmpty = false, double newValue = -1)
        {
            IntPtr address = ResolvePointerChain(playerBase, Offsets.playerTarget);

            if (address != 0)
            {
                previousAddress = address;
            }

            if (address == 0 && usePreviousAddressIfEmpty)
            {
                if (previousAddress == 0)
                {
                    return 0;
                }

                address = previousAddress;
            }

            

            return SetMonsterValue(stat, address, newValue);
        }

        private double SetMonsterValue(MonsterStats option, IntPtr monsterAddress, double newValue = -1)
        {
            //IntPtr playerTarget = ResolvePointerChain(playerBase) + Offsets.playerTarget;
          //IntPtr actorModelBase = monsterAddress
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
                case MonsterStats.PsychoDefense:
                    address = ResolvePointerChain(actorModelBase, Offsets.actorModelPsychoDefense) + 0x10;
                    break;
                case MonsterStats.DimensionDefense:
                    address = ResolvePointerChain(actorModelBase, Offsets.actorModelDimensionDefense) + 0x10;
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

            // Assume we never need to manually set TenacityMax value.
            if (newValue > -1)
            {
                WriteFloat(address, (float)newValue);
            }

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

            WriteDouble(address + Offsets.playerX, x);
            WriteDouble(address + Offsets.playerY, y);
            WriteDouble(address + Offsets.playerZ, z);
        }

        public void TestRunThread()
        {
            RunThread(gameAssemblyBaseAddress + 0x235AB50);
        }


        // Personal data dumping
        public void DataToFile()
        {
            IntPtr based = ResolvePointerChain(levelRootBase, 0xA8, 0x10) + 0x20;


            using (StreamWriter outputFile = new StreamWriter("D:\\ailimitactorlist-sewers.txt"))
            {

                for (int i = 0; i < 128; i++)
                {
                    outputFile.WriteLine("" + i + " " + ReadFloat(ResolvePointerChain(based + (i * 8), 0x58, 0x10) + 0x10).ToString() + " / " + ReadFloat(ResolvePointerChain(based + (i * 8), 0x58, 0x18) + 0x10).ToString());
                }

            }


            based = ResolvePointerChain(archiveDataBase, 0x38, 0x18, 0x10) + 0x20;

            using (StreamWriter outputFile = new StreamWriter("D:\\ailimit monsterstatelist-sewers.txt"))
            {
                for (int i = 0; i < 128; i++)
                {
                    outputFile.WriteLine("" + i + " ID: " + ReadUInt32((IntPtr)ReadUInt64(based + (i * 8)) + 0x10).ToString() + " Is dead: " + ReadByte((IntPtr)ReadUInt64(based + (i * 8)) + 0x14).ToString() + " Can relive: " + ReadByte((IntPtr)ReadUInt64(based + (i * 8)) + 0x1C).ToString());
                }
            }
        }

        public enum StateTypes
        {
            GameState,
            MonsterState,
        }

        /*public int SetState(uint state, StateTypes stateType, int newValue = -1, int levelID =-1)
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
                //MessageBox.Show("" + state + " " + (ReadUInt64(statesBase + (i * 8)) + 0x10).ToString("X") + " " + ReadUInt32((IntPtr)ReadUInt64(statesBase + (i * 8)) + 0x10));
                if (ReadUInt32((IntPtr)ReadUInt64(statesBase + (i * 8)) + 0x10) == state)
                {

                    if (newValue > -1)
                        WriteUInt32((IntPtr)ReadUInt64(statesBase + (i * 8)) + 0x14, (uint)newValue);

                    return (int)ReadUInt32((IntPtr)ReadUInt64(statesBase + (i * 8)) + 0x14);
                }
            }
            return -1;
        }*/

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

        private uint[,] states = new uint[2, 2000];          // Holds existing values of states [Game, Monster]
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
    }
}
