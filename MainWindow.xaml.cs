using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static AILimitTool.AILimitLink;

namespace AILimitTool;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, IDisposable
{
    AILimitLink AILimit;
    HotkeyManager hotkeyManager;

    System.Windows.Threading.DispatcherTimer uiTimer = new System.Windows.Threading.DispatcherTimer();
    TargetDisplay targetDisplay = new TargetDisplay();

    bool disposed = false;

    bool uiActive = false;

    public MainWindow()
    {
        this.DataContext = this;

        InitializeComponent();

        Closing += WindowClosed;
        Loaded += WindowLoaded;

        AILimit = new AILimitLink();

        InitialSetup();
        
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        { return; }

        if (disposing)
        {
            AILimit.Dispose();
            targetDisplay.shouldClose = true;
            targetDisplay.Close();
        }
        disposed = true;
    }


    void WindowClosed(object sender, EventArgs e)
    {
        SaveSettings();
        Dispose();
    }

    void WindowLoaded(object sender, EventArgs e)
    {
        LoadSettings();
        ResizeTargetDisplayWindow();
    }

    private void InitialSetup()
    {
        LoadDestinationList();
        LoadTeleportDestinations();

        tabError.Visibility = Visibility.Hidden;

        hotkeyManager = new HotkeyManager(this);
        UpdateUIHotkeyText();

        comboboxBossRespawn.ItemsSource = bossList;

        comboShop.ItemsSource = vendorIDs;
        comboWeapon.ItemsSource = weaponStoreIDs;
        comboSeal.ItemsSource = sealStoreIDs;
        comboHelmet.ItemsSource = helmetStoreIDs;
        comboArmour.ItemsSource = armourStoreIDs;
        comboSpells.ItemsSource = spellStoreIDs;
        comboNucleus.ItemsSource = nucleusStoreIDs;
        comboGeneral.ItemsSource = generalStoreIDs;

        //listboxHotkeys.ItemsSource = hotkeyNameList;

        uiTimer.Tick += uiTimer_Tick;
        uiTimer.Interval = TimeSpan.FromMilliseconds(100);
        uiTimer.Start();
    }

    //
    // UI stuff
    //

    private string GetAddressStateText(AddressTypes type)
    {
        IntPtr address = AILimit.GetAddress(type);

        string returnText = "";

        if (address > 0x100000)
            return address.ToString("X");

        return "Not found";
    }

    private void uiTimer_Tick(object sender, EventArgs e)
    {
        // TODO: Replace checking the variables directly with a method that reports current link state
        if (!AILimit.linkActive)
        {
            this.Title = "AI Limit Tool - game not found";
            textError.Text = "Game not found. Attempting to find AI Limit process. If the game is open, please try restarting.";
            textAddressStatus.Text = "";
            DisableTabs(true);
        }
        else if (!AILimit.modulesFound)
        {
            this.Title = "AI Limit Tool - searching for modules";
            textError.Text = "AI Limit process found. Searching for modules. If this does not resolve within a few seconds, please try restarting.";

            textAddressStatus.Text = "GameAssembly.dll: " + GetAddressStateText(AddressTypes.ArchiveData) + "\n"
                                + "UnityPlayer.dll: " + GetAddressStateText(AddressTypes.UnityPlayer);
        }
        else if (!AILimit.mainObjectsFound)
        {
            this.Title = "AI Limit Tool - searching for offsets";
            textError.Text = "AI Limit process found. Attempting to find offsets. This step cannot be completed until a save game is loaded. If game is loaded, please try restarting.";

            textAddressStatus.Text = "ArchiveData: " + GetAddressStateText(AddressTypes.ArchiveData) + "\n"
                                + "Player: " + GetAddressStateText(AddressTypes.Player) + "\n"
                                + "LoadingView: " + GetAddressStateText(AddressTypes.LoadingView) + "\n"
                                + "LevelRoot: " + GetAddressStateText(AddressTypes.LevelRoot) + "\n"
                                + "Transfer Destination: " + GetAddressStateText(AddressTypes.TransferDestination) + "\n"
                                + "Timer: " + GetAddressStateText(AddressTypes.Timer);

            DisableTabs(true);
        }

        if (uiActive)
        {
            // standard ui operations

            // State monitors
            if (tabcontrolMain.SelectedIndex == (int)TabItems.States && (bool)checkboxStateMonitor.IsChecked)
            {
                string output = AILimit.CheckStateData(StateTypes.GameState);

                if (output != "")
                {
                    textboxStateLog.AppendText(output);
                    textboxStateLog.ScrollToEnd();
                }
            }

            if (tabcontrolMain.SelectedIndex == (int)TabItems.States && (bool)checkboxMonsterStateMonitor.IsChecked)
            {
                string output = AILimit.CheckStateData(StateTypes.MonsterState);

                if (output != "")
                {
                    textboxStateLog.AppendText(output);
                    textboxStateLog.ScrollToEnd();
                }
            }

            if (targetDisplay.Visibility == Visibility.Visible)
                UpdateTargetDisplay();

            if (AILimit.IsLoadingScreenActive())
                UpdateTeleportTab();
        }
        else
        {
            if (AILimit.linkActive && AILimit.mainObjectsFound)
            {
                uiActive = true;
                InitUI();
                this.Title = "AI Limit Tool";   
            }
        }
    }

    private void InitUI()
    {
        DisableTabs(false);
        UpdateTeleportTab();
    }

    private void UpdateTeleportTab()
    {
        textCurrentMap.Text = "Current level: " + AILimit.GetCurrentLevelName() + " (" + AILimit.GetCurrentLevelID() + ")";
        FilterTeleportDestinations();
    }

    private void UpdateTargetDisplay()
    {
        targetDisplay.UpdateDisplay(TargetDisplay.MonsterStats.HP, AILimit.SetTargetMonsterValue(MonsterStats.HP, true),
                                                                   AILimit.SetTargetMonsterValue(MonsterStats.HPMax, true));
        targetDisplay.UpdateDisplay(TargetDisplay.MonsterStats.Tenacity, AILimit.SetTargetMonsterValue(MonsterStats.Tenacity, true),
                                                                         AILimit.SetTargetMonsterValue(MonsterStats.TenacityMax, true),
                                                                         AILimit.SetTargetMonsterValue(MonsterStats.TenacityDecreaseTick, true) - AILimit.GetTimer());
        targetDisplay.UpdateDisplay(TargetDisplay.MonsterStats.Sync, AILimit.SetTargetMonsterValue(MonsterStats.Sync, true));
        targetDisplay.UpdateDisplay(TargetDisplay.MonsterStats.PoisonAccumulation, AILimit.SetTargetMonsterValue(MonsterStats.PoisonAccumulation, true));
        targetDisplay.UpdateDisplay(TargetDisplay.MonsterStats.PiercingAccumulation, AILimit.SetTargetMonsterValue(MonsterStats.PiercingAccumulation, true));
        targetDisplay.UpdateDisplay(TargetDisplay.MonsterStats.InfectionAccumulation, AILimit.SetTargetMonsterValue(MonsterStats.InfectionAccumulation, true));

        string headers = "";
        string values = "";

        if ((bool)checkboxTargetState.IsChecked)
        {
            headers += "Superarmour Level:\n\n";

            values += AILimit.SetTargetMonsterValue(MonsterStats.SuperArmourLevel, true).ToString() + "\n\n";
        }

        if ((bool)checkboxTargetDefense.IsChecked)
        {
            headers += "Physical Defense:\n" +
                    "Fire Defense:\n" +
                    "Electric Defense:\n" +
                    "Shatter Defense:\n\n";

            values += AILimit.SetTargetMonsterValue(MonsterStats.PhysicalDefense, true).ToString("P1") + "\n";
            values += AILimit.SetTargetMonsterValue(MonsterStats.FireDefense, true).ToString("P1") + "\n";
            values += AILimit.SetTargetMonsterValue(MonsterStats.ElectricDefense, true).ToString("P1") + "\n";
            values += AILimit.SetTargetMonsterValue(MonsterStats.ShatterDefense, true).ToString("P1") + "\n\n";
        }

        if ((bool)checkboxTargetStatusResist.IsChecked)
        {
            headers += "Poison Resist:\n" +
                    "Piercing Resist:\n" +
                    "Infection Resist:\n\n";

            values += AILimit.SetTargetMonsterValue(MonsterStats.PoisonResist, true).ToString("P1") + "\n";
            values += AILimit.SetTargetMonsterValue(MonsterStats.PiercingResist, true).ToString("P1") + "\n";
            values += AILimit.SetTargetMonsterValue(MonsterStats.InfectionResist, true).ToString("P1") + "\n\n";
        }

        targetDisplay.UpdateDisplayText(4, headers, values);
    }

    private void tabcontrolMain_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        switch ((sender as TabControl).SelectedIndex)
        {
            case 1:
                PopulateStatsTab();
                break;
        }
    }

    private void DisableTabs(bool disable)
    {
        uiActive = !disable;

        if (disable)
        {
            tabError.Visibility = Visibility.Visible;
            tabcontrolMain.SelectedIndex = (int)TabItems.Error;
            tabMain.IsEnabled = false;
            tabStats.IsEnabled = false;
            tabTeleport.IsEnabled = false;
            tabItems.IsEnabled = false;
            tabStates.IsEnabled = false;
        }
        else
        {
            tabError.Visibility = Visibility.Hidden;
            tabcontrolMain.SelectedIndex = 0;
            tabMain.IsEnabled = true;
            tabStats.IsEnabled = true;
            tabTeleport.IsEnabled = true;
            tabItems.IsEnabled = true;
            tabStates.IsEnabled = true;
        }
    }

    public void ActionHotkey(uint option)
    {
        switch (option)
        {
            case (uint)GameOptions.Immortal:
                checkboxPlayerImmortal.IsChecked = !checkboxPlayerImmortal.IsChecked;
                break;
            case (uint)GameOptions.LockTargetHP:
                checkboxLockTargetHP.IsChecked = !checkboxLockTargetHP.IsChecked;
                break;
            case (uint)GameOptions.LockSync:
                checkboxLockSync.IsChecked = !checkboxLockSync.IsChecked;
                break;
            case (uint)GameOptions.PlayerSpeed:
                checkboxPlayerSpeed.IsChecked = !checkboxPlayerSpeed.IsChecked;
                break;
            case (uint)GameOptions.InfiniteDew:
                checkboxInfiniteDew.IsChecked = !checkboxInfiniteDew.IsChecked;
                break;
            case (uint)GameOptions.PassiveEnemies:
                checkboxPassiveEnemies.IsChecked = !checkboxPassiveEnemies.IsChecked;
                break;

            case (uint)GameOptions.TargetInfo:
                checkboxTargetInfo.IsChecked = !checkboxTargetInfo.IsChecked;
                break;

            case (uint)GameOptions.AddCrystals:
                AddCrystals(int.Parse(textboxAddCrystals.Text));
                break;
            
            case (uint)GameOptions.TeleportQucksave:
                QuicksavePlayerPosition();
                break;
            case (uint)GameOptions.TeleportQuickload:
                QuickloadPlayerPosition();
                break;
            case (uint)GameOptions.NextTab:
                NextTab(true);
                break;
        }
    }

    public void UpdateUIHotkeyText()
    {
        Dictionary<(int, ModifierKeys), GameOptions> hotkeys = hotkeyManager.GetHotkeyDictionary();

        foreach (KeyValuePair<(int hotkey, ModifierKeys modifiers), GameOptions> hotkeyEntry in hotkeys)
        {
            string hotkeyText = "";

            if (hotkeyEntry.Key.Item2 != ModifierKeys.None)
            {
                if (hotkeyEntry.Key.modifiers.HasFlag(ModifierKeys.Control)) { hotkeyText = "Ctrl+"; }
                if (hotkeyEntry.Key.modifiers.HasFlag(ModifierKeys.Alt)) { hotkeyText += "Alt+"; }
                if (hotkeyEntry.Key.modifiers.HasFlag(ModifierKeys.Shift)) { hotkeyText += "Shift+"; }
            }

            hotkeyText += (char)hotkeyEntry.Key.hotkey;

            switch (hotkeyEntry.Value)
            {
                case GameOptions.Immortal:                                              // Player tab
                    textPlayerImmortal_Hotkey.Text = $" ({hotkeyText})"; break;
                case GameOptions.LockTargetHP:
                    textLockTargetHP_Hotkey.Text = $" ({hotkeyText})"; break;
                case GameOptions.LockSync:
                    textLockSync_Hotkey.Text = $" ({hotkeyText})"; break;
                case GameOptions.PlayerSpeed:
                    textPlayerSpeed_Hotkey.Text = $" ({hotkeyText})"; break;
                case GameOptions.InfiniteDew:
                    textInfiniteDew_Hotkey.Text = $" ({hotkeyText})"; break;
                case GameOptions.PassiveEnemies:
                    textPassiveEnemies_Hotkey.Text = $" ({hotkeyText})"; break;

                case GameOptions.TargetInfo:
                    textTargetInfo_Hotkey.Text = $" ({hotkeyText})"; break;

                case GameOptions.AddCrystals:
                    textAddCrystals_Hotkey.Text = $" ({hotkeyText})"; break;

                case GameOptions.TeleportQucksave:
                    textQuicksave_Hotkey.Text = $" ({hotkeyText})"; break;
                case GameOptions.TeleportQuickload:
                    textQuickload_Hotkey.Text = $" ({hotkeyText})"; break;
            }
        }
    }
    //
    // Tab Navigation
    //

    private void NextTab(bool forward = true)
    {
        if (forward)
        {
            int tab = tabcontrolMain.SelectedIndex + 1;

            if (tab == (int)TabItems.Error)
                tab = 0;

            tabcontrolMain.SelectedIndex = tab;
        }
    }


    //
    // Textbox verifiers
    //

    private void CalculatePlayerLevel(object sender, KeyEventArgs e)
    {
        if ((bool)checkboxCalculatePlayerLevel.IsChecked)
        {
            int finalValue = int.Parse(textboxLife.Text);
            finalValue += int.Parse(textboxVitality.Text);
            finalValue += int.Parse(textboxStrength.Text);
            finalValue += int.Parse(textboxTechnique.Text);
            finalValue += int.Parse(textboxSpirit.Text);
            finalValue -= 49;
            if (finalValue < 1)
            {
                finalValue = 1;
            }
            textboxPlayerLevel.Text = finalValue.ToString();
        }
    }

    private void TextBox_VerifyNumeric(object sender, TextCompositionEventArgs e)
    {
        foreach (char c in e.Text)
        {
            if (!char.IsDigit(c)) { e.Handled = true; break; }
        }
    }

    private void TextBox_VerifyPercent(object sender, RoutedEventArgs e)
    {
        int stat = 0;

        try
        {
            stat = int.Parse((sender as TextBox).Text);
        }
        catch
        {
            (sender as TextBox).Text = "50";
        }

        if (stat < 1)
        {
            (sender as TextBox).Text = "1";
        }
        else if (stat > 100)
        {
            (sender as TextBox).Text = "100";
        }
    }

    private void TextBox_VerifyFloat(object sender, TextCompositionEventArgs e)
    {
        foreach (char c in e.Text)
        {
            if (!(char.IsDigit(c) || c.Equals('.'))) { e.Handled = true; break; }
        }
    }

    private void TextBox_VerifyStatRange(object sender, RoutedEventArgs e)
    {
        int stat = 0;

        try
        {
            stat = int.Parse((sender as TextBox).Text);
        }
        catch
        {
            (sender as TextBox).Text = "1";
        }

        if (stat < 1)
        {
            (sender as TextBox).Text = "1";
        }
        else if (stat > 99)
        {
            (sender as TextBox).Text = "99";
        }
    }

    //
    // Tab setups
    //

    public enum TabItems
    {
        Player,
        Stats,
        Teleport,
        Items,
        States,
        Error,
    }

    void PopulateStatsTab()
    {
        textboxPlayerLevel.Text = AILimit.SetPlayerStats(PlayerStats.PlayerLevel).ToString();

        textboxLife.Text = AILimit.SetPlayerStats(PlayerStats.Life).ToString();
        textboxVitality.Text = AILimit.SetPlayerStats(PlayerStats.Vitality).ToString();
        textboxStrength.Text = AILimit.SetPlayerStats(PlayerStats.Strength).ToString();
        textboxTechnique.Text = AILimit.SetPlayerStats(PlayerStats.Technique).ToString();
        textboxSpirit.Text = AILimit.SetPlayerStats(PlayerStats.Spirit).ToString();

        textboxCrystals.Text = AILimit.SetPlayerStats(PlayerStats.Crystals).ToString();
    }

    //
    // PLAYER TAB CHECKBOXES
    //

    private void PlayerImmortal_Toggle(object sender, RoutedEventArgs e)
    {
        if (AILimit.TogglePlayerBools(GameOptions.Immortal, (bool)checkboxPlayerImmortal.IsChecked))
        {
            UpdateStatusBar("Player immortal: " + checkboxPlayerImmortal.IsChecked.ToString());
        }
    }

    private void LockSync_Toggle(object sender, RoutedEventArgs e)
    {
        if (AILimit.TogglePlayerBools(GameOptions.LockSync, (bool)checkboxLockSync.IsChecked))
        {
            UpdateStatusBar("Player sync locked: " + checkboxPlayerImmortal.IsChecked.ToString());
        }
    }

    private void InfiniteDew_Toggle(object sender, RoutedEventArgs e)
    {
        //AILimit.InfiniteDew((bool)checkboxInfiniteDew.IsChecked);
        AILimit.SetInfiniteConsumables((bool)checkboxInfiniteDew.IsChecked);
        UpdateStatusBar("Infinite healing dew: " + checkboxInfiniteDew.IsChecked.ToString());
    }

    private void FreeWeaponUpgrades_Toggle(object sender, RoutedEventArgs e)
    {
        if ((bool)checkboxFreeUpgrades.IsChecked)
            AILimit.AddGameOption(GameOptions.FreeUpgrade);
        else
            AILimit.RemoveGameOption(GameOptions.FreeUpgrade);

        UpdateStatusBar("Free weapon upgrades: " + checkboxFreeUpgrades.IsChecked.ToString());
    }

    private void PassiveEnemies_Toggle(object sender, RoutedEventArgs e)
    {
        AILimit.SetPassiveEnemies((bool)checkboxPassiveEnemies.IsChecked);
        UpdateStatusBar("Passive enemies: " + checkboxPassiveEnemies.IsChecked.ToString());
    }

    private void OneShotEnemies_Toggle(object sender, RoutedEventArgs e)
    {
        //AILimit.SetOneShotEnemies((bool)checkboxOneShotEnemies.IsChecked);
        //UpdateStatusBar("Passive enemies: " + checkboxOneShotEnemies.IsChecked.ToString());
    }

    private void LockTargetHP_Toggle(object sender, RoutedEventArgs e)
    {
        if ((bool)checkboxLockTargetHP.IsChecked)
        {
            AILimit.AddGameOption(GameOptions.LockTargetHP, int.Parse(textboxLockTargetHP.Text));
            textboxLockTargetHP.IsEnabled = false;
            UpdateStatusBar("Target HP locked to " + int.Parse(textboxLockTargetHP.Text) + "%");
        }
        else
        {
            AILimit.RemoveGameOption(GameOptions.LockTargetHP);
            textboxLockTargetHP.IsEnabled = true;
            UpdateStatusBar("Target HP lock disabled");
        }
    }

    private void MovementSpeed_Toggle(object sender, RoutedEventArgs e)
    {
        float speed = 6; //default player speed

        if ((bool)checkboxPlayerSpeed.IsChecked)
        {
            speed = Convert.ToSingle(textboxMovementSpeed.Text);
            textboxMovementSpeed.IsEnabled = false;
        }
        AILimit.SetPlayerSpeed(speed);
        textboxMovementSpeed.IsEnabled = true;

        UpdateStatusBar("Player movement speed set to " + speed);
    }

    private void ShowTargetInfo_Toggle(object sender, RoutedEventArgs e)
    {
        if((bool)checkboxTargetInfo.IsChecked)
            targetDisplay.Show();
        else
            targetDisplay.Hide();
    }



    // Dictionary for binding
    private Dictionary<string, Boss> bossList = new Dictionary<string, Boss>()
    {
        { "Sewer Cleaner",                      Boss.SewerCleaner },
        { "Lore, the Lost Lancer",              Boss.Lore },
        { "Scavenger Patriarch",                Boss.Patriarch },
        { "Necro, the Panic Reaper",            Boss.NecroPanic },
        { "Three-Faced Pardoner",               Boss.Pardoner },
        { "Hunter Squad",                       Boss.HunterSquad },
        { "Cleansing Knight",                   Boss.CleansingKnight },
        { "Saint",                              Boss.Saint },
        { "Choirmaster, the Inspector",         Boss.Choirmaster },
        { "Hunter of Bladers",                  Boss.Hunter },
        { "Persephone",                         Boss.Persephone },
        { "Necro, the Wanderer of Undersea",    Boss.NecroWanderer },
        { "Colossaint",                         Boss.Colossaint },
        { "Eunomia, the Resplendent Bishop",    Boss.Eunomia },
        { "Ursula",                             Boss.Ursula },
        { "Guardians of the Tree",              Boss.Guardians },
        { "Seraphim Absolver",                  Boss.Absolver },
        { "Boss Rush",                          Boss.BossRush },
        { "Vikas",                              Boss.Vikas },
        { "Loskid, the Son",                    Boss.Loskid },
        { "Aether, the Father",                 Boss.Aether },
        { "Charon, the Nurturer",               Boss.Charon },
    };

    private void RespawnSelectedBoss(object sender, RoutedEventArgs e)
    {
        KeyValuePair<string, Boss> selected = (KeyValuePair<string, Boss>)comboboxBossRespawn.SelectedItem;

        RespawnBoss(selected.Value);
    }

    private void RespawnAllBosses(object sender, RoutedEventArgs e)
    {
        foreach (KeyValuePair<string, Boss> entry in bossList)
        {
            RespawnBoss(entry.Value);
        }
    }

    private void RespawnBoss(Boss boss)
    {
        AILimit.RespawnBoss(boss);
    }


    //
    // TELEPORT TAB
    //

    (double x, double y, double z) savedPlayerPosition = (0, 0, 0);

    private void QuicksavePlayerPosition(object sender, RoutedEventArgs e)
    {
        QuicksavePlayerPosition();
    }

    private void QuicksavePlayerPosition(double x = 0xFFFFFFFF, double y = 0xFFFFFFFF, double z = 0xFFFFFFFF, bool updateStatusBar = true)
    {
        if (x == 0xFFFFFFFF)
        {
            savedPlayerPosition = AILimit.GetPlayerPosition();
        }
        else
        {
            savedPlayerPosition = (x, y, z);
        }
        textSavedPosition.Text = "Saved position: x" + savedPlayerPosition.x.ToString("N2") + " y" + savedPlayerPosition.y.ToString("N2") + " z" + savedPlayerPosition.z.ToString("N2");

        if (updateStatusBar)
            UpdateStatusBar("Position saved x" + savedPlayerPosition.x.ToString("N2") + " y" + savedPlayerPosition.y.ToString("N2") + " z" + savedPlayerPosition.z.ToString("N2"));
    }

    private void QuickloadPlayerPosition(object sender, RoutedEventArgs e)
    {
        QuickloadPlayerPosition();
    }

    private void QuickloadPlayerPosition()
    {
        AILimit.SetPlayerPosition(savedPlayerPosition.x, savedPlayerPosition.y, savedPlayerPosition.z);
        UpdateStatusBar("Position loaded");
    }

    private void SavePlayerPosition(object sender, RoutedEventArgs e)
    {
        string description = Microsoft.VisualBasic.Interaction.InputBox("Description", "Enter description for location");
        if (string.IsNullOrEmpty(description)) { return; }

        TeleportDestination destination = new TeleportDestination();

        (double x, double y, double z) position = AILimit.GetPlayerPosition();

        Debug.Print(position.ToString());

        destination.Description = description;
        destination.x = position.x;
        destination.y = position.y;
        destination.z = position.z;
        destination.Level = (int)AILimit.GetCurrentLevelID();

        teleportDestinations.Add(destination);

        FilterTeleportDestinations();
        SaveTeleportDestinations();
    }

    private void LoadPlayerPosition(object sender, RoutedEventArgs e)
    {
        TeleportDestination destination = listboxTeleportDestinations.SelectedItem as TeleportDestination;

        if (destination != null)
            AILimit.SetPlayerPosition(destination.x, destination.y, destination.z);
    }

    private void RemovePlayerPositionEntry(object sender, RoutedEventArgs e)
    {
        TeleportDestination destination = listboxTeleportDestinations.SelectedItem as TeleportDestination;

        if (destination != null)
        {
            MessageBoxResult confirm = MessageBox.Show("Are you sure you want to delete the teleport point " + destination.DisplayName + "?", "Confirm", MessageBoxButton.YesNo);

            if (confirm == MessageBoxResult.Yes)
            {
                teleportDestinations.Remove(destination);
                FilterTeleportDestinations();
                SaveTeleportDestinations();
            }
        }
    }



    //
    // STATS TAB
    //

    private void SetPlayerStats(object sender, RoutedEventArgs e)
    {
        AILimit.SetPlayerStats(PlayerStats.PlayerLevel, int.Parse(textboxPlayerLevel.Text));
        AILimit.SetPlayerStats(PlayerStats.Life, int.Parse(textboxLife.Text));
        AILimit.SetPlayerStats(PlayerStats.Vitality, int.Parse(textboxVitality.Text));
        AILimit.SetPlayerStats(PlayerStats.Strength, int.Parse(textboxStrength.Text));
        AILimit.SetPlayerStats(PlayerStats.Technique, int.Parse(textboxTechnique.Text));
        AILimit.SetPlayerStats(PlayerStats.Spirit, int.Parse(textboxSpirit.Text));

        UpdateStatusBar("Player stats set");
    }

    private void SetCrystals(object sender, RoutedEventArgs e)
    {
        AILimit.SetPlayerStats(PlayerStats.Crystals, int.Parse(textboxCrystals.Text));
        UpdateStatusBar("Crystals set to " + int.Parse(textboxCrystals.Text));
    }

    private void AddCrystals(object sender, RoutedEventArgs e)
    {
        AddCrystals(int.Parse(textboxAddCrystals.Text));
    }

    private void AddCrystals(int crystals)
    {
        
        uint totalCrystals = AILimit.SetPlayerStats(PlayerStats.Crystals, crystals, true);

        UpdateStatusBar("Added " + int.Parse(textboxAddCrystals.Text) + " crystals, new total " + totalCrystals);
        textboxCrystals.Text = crystals.ToString();
        return;
    }

    //
    // ITEMS TAB
    //

    Dictionary<string, uint> vendorIDs = new Dictionary<string, uint>
    {
        { "Grayrhino Grocery (Campsite)", 1001 },
        { "Moose's (Top Apron)",          1002 },
    };

    Dictionary<string, uint> weaponStoreIDs = new Dictionary<string, uint> // Category 3
    {
        { "Arbiter",                         89 },
        { "Blader Greatsword",               23 },
        { "Blader Longsword",                 1 },
        { "Blader Swords",                   12 },
        { "Bonecrackers",                   155 },
        { "Cast Iron Greatsword",           342 },
        { "Corrupted Blader Greatsword",    320 },
        { "Corrupted Blader Longsword",     232 },
        { "Corrupted Blader Swords",        243 },
        { "Dawnfrost",                      100 },
        { "Envenomed Blade",                276 },
        { "Forged Steel Blade",             265 },
        { "Holy Embrace",                   199 },
        { "Holy Ritual",                    221 },
        { "Hunter's Blades",                298 },
        { "Impact Drill",                   188 },
        { "Knight's Lance",                 210 },
        { "Materialism",                    166 },
        { "Mercy",                          177 },
        { "Opossums Sais",                  287 },
        { "Pardoner",                       144 },
        { "Pickaxe",                         67 },
        { "Reapers",                         78 },
        { "Red Deer",                        34 },
        { "Road Sign",                      309 },
        { "Rusty Longsword",                331 },
        { "Rusty Pipe",                      45 },
        { "Scrap Lance",                    122 },
        { "Serrated Halberd",                56 },
        { "Steel Axes",                     133 },
        { "Stygian Touch",                  111 },
        { "Wilted Foliage",                 254 },
    };

    Dictionary<string, uint> helmetStoreIDs = new Dictionary<string, uint> // Category 4
    {
        { "Bandage",                         20 },
        { "Blader Huntress Mask",             4 },
        { "Blader Visor",                     1 },
        { "Buddha Hat",                       9 },
        { "Corrupted Blader Visor",          18 },
        { "Clergy Headwear",                  5 },
        { "Chlorostil Mask",                 12 },
        { "Frame Glasses",                   19 },
        { "Goggles",                          7 },
        { "Guardian Helmet",                 23 },
        { "Hunter's Helmet",                 14 },
        { "Iron Pot",                         2 },
        { "Listener Guild Hood",             15 },
        { "Long Hair",                       25 },
        { "Maid Hairband",                   17 },        
        { "Monitor Hood",                    21 },
        { "Osprey Hat",                       8 },
        { "Osprey Ponytail",                 13 },
        { "Ponytail",                         6 },
        { "Rabbit Ears",                     16 },
        { "Ragged Hood",                     26 },
        { "Takamagahara Hat",                10 },
        { "The Child's Tiara",               24 },
        { "Thorny Headgear",                  3 },
        { "Twisted Braid",                   11 },
        { "White Sparrow's Mask",            22 },
    };

    Dictionary<string, uint> armourStoreIDs = new Dictionary<string, uint> // Category 5
    {
        { "Blader Armor",                     3 },
        { "Blader Huntress Attire",           8 },
        { "Casual Blader Suit",               2 },
        { "Clergy Robe",                     11 },
        { "Corrupted Blader Armor",          18 },
        { "Enterprise Investigator",          4 },
        { "Fisherman's Suit",                13 },
        { "Guardian Armor",                   7 },   
        { "Hunter's Armor",                  14 },
        { "Listener Guild Uniform",          15 },
        { "Maid Outfit",                     17 },
        { "Patient's Clothing",              20 },
        { "Rabbit Set",                      16 },
        { "Ragged Clothes",                   1 },
        { "Raincoat",                        12 },
        { "Researcher's Robe",               19 },
        { "Scavenger's Clothing",             6 },
        { "The Child's Attire",               9 },
        { "Traveller's Coat",                 5 },
        { "Traveller's Outfit",              10 },
    };

    Dictionary<string, uint> spellStoreIDs = new Dictionary<string, uint> // Category 8
    {
        { "Counter Field",                 2002 },
        { "Piercing Claw",                 2005 },
        { "Shield",                        2003 },
        { "Thunder Step",                  2004 },
        { "Electrification",                 16 },
        { "Ethereal Beam",                   12 },
        { "Ethereal Orb",                    11 },
        { "EX-Railgun",                      18 },
        { "Flame Jet",                        8 },
        { "Ignition",                        15 },
        { "Lightning Cluster",               10 },
        { "Lightning Bolt",                   5 },
        { "Lightning Hammer",                 7 },
        { "Lightning Tornado",                3 },
        { "Minimum Pain",                    13 },
        { "Missile Barrage",                 17 },
        { "Partial Reconstruction",           2 },
        { "Railgun",                          1 },
        { "Scales Blast",                     9 },
        { "Spears of Punishment",             4 },
        { "Terra Inferno",                    6 },
        { "Trailblazer's Oath",              14 },
    };

    Dictionary<string, uint> nucleusStoreIDs = new Dictionary<string, uint> // Category 9
    {
        { "Cleansing Knight's Nucleus",       4 },
        { "Clergy's Nucleus",                 5 },
        { "Divine Vessel's Nucleus",          9 },
        { "Elite Necro's Fusion Nucleus",     7 },
        { "Elite Necro's Nucleus",            3 },
        { "Guardian's Nucleus",              10 },
        { "Mutant Clergy's Nucleus",          8 },
        { "Mutant Blader Nucleus",           11 },
        { "Nucleus on the Child's Tiara",    12 },
        { "Persephone's Nucleus",             6 },
        { "Standard Nucleus",                 1 },
        { "Turbid Nucleus",                  13 },
        { "Void Nucleus",                     2 },
    };

    Dictionary<string, uint> sealStoreIDs = new Dictionary<string, uint> // Category 7
    {
        { "Seal of Clergies",              1060 },
        { "Seal of Executor",              1030 },
        { "Seal of Investigator",          1040 },
        { "Seal of Newborn",               1000 },
        { "Seal of Pilgrim",               1020 },
        { "Seal of the Tree",              1050 },
        { "Standard Seal of Bladers",      1010 },

        { "Breath of Life",                2001 },
        { "Burst",                         2032 },
        { "Brief Condensation",            2020 },
        { "Brief Polymerization",          2019 },
        { "Cleansing Therapy",             2027 },
        { "Cold Therapy",                  2026 },
        { "Collapse",                      2033 },
        { "Conversion",                    2038 },
        { "Deadwood Form",                 2030 },
        { "Deviation",                     2039 },
        { "Divine Guardian",               2012 },
        { "Drizzle",                       2013 },
        { "Emerald Form",                  2023 },
        { "Ethereal Tone",                 2029 },
        { "Everlasting Piercing",          2042 },
        { "Fortified Shield",              2043 },
        { "Gluttony",                      2037 },
        { "Gold Hoarder",                  2009 },
        { "Gush",                          2045 },
        { "Hardened Skin",                 2010 },
        { "Harsh Voice",                   2028 },
        { "Hyperstability",                2006 },
        { "Inertia Transformation",        2016 },
        { "Ingestion",                     2036 },
        { "Insulation Transformation",     2018 },
        { "Jade Form",                     2022 },
        { "Metastability",                 2004 },
        { "Monolith Form",                 2031 },
        { "Moon Dew",                      2024 },
        { "Moon Radiance",                 2025 },
        { "Neutral Transformation",        2017 },
        { "Nova",                          2046 },
        { "Precipitation",                 2021 },
        { "Quagmire",                      2014 },
        { "Sand Accumulator",              2007 },
        { "Scattered Stars",               2041 },
        { "Slayer",                        2034 },
        { "Stability",                     2005 },
        { "Steel Shell",                   2011 },
        { "Stone Builder",                 2008 },
        { "Submerged Coffin",              2015 },
        { "Tide of Life",                  2002 },
        { "Thunder Mastery",               2044 },
        { "Torrent of Life",               2003 },
        { "Tyrant",                        2035 },
        { "Variation",                     2040 },
    };

    //    1 Healing materials and battle consumables
    //  101 Upgrade materials
    // 1001 Junk items 
    // 2001 Spell Frame + abilities
    // 2101 Soil
    // 2601 Keys
    Dictionary<string, uint> generalStoreIDs = new Dictionary<string, uint> // Category 0
    {
        { "Antidote",                        14 },
        { "Devitalized Fetus",               11 },
        { "Disordered Fetus",                12 },
        { "Envenomed Needle",                15 },
        { "Eucharist",                        4 },
        { "Mud Ball",                         2 },
        { "Magnetic Surge",                   9 },
        { "Mini Bomb",                        6 },
        { "Phosphorous Dart",                16 },
        { "Portable Electrification",         7 },
        { "Portable Ignition",               13 },
        { "Portable Lightning Bolt",         19 },
        { "Portable Lightning Orb",          20 },
        { "Pure Crystal",                     8 },
        { "Sacred Blood Crystal",            17 },
        { "Salted Mud Ball",                  3 },
        { "Shriek Core",                     18 },
        { "Stabilizer",                      10 },
        { "Stone",                            5 },

        { "Adrammelech's Badge",           2027 },
        { "Ancient Map",                   2013 },
        { "Azure Branch",                  2023 },
        { "Bunny Doll Arrisa",             2025 },
        { "Covenant: Sewer Town",          2018 },
        { "Covenant: Underground Parish",  2017 },
        { "Crystal Sprout",                2014 },
        { "Dark Branch",                   2024 },
        { "Dew Essence",                   2009 },
        { "Golden Branch",                 2022 },
        { "Living Seed",                   2020 },
        { "Merchant's Map",                2019 },
        { "Nameless Flower",               2021 },
        { "Nutrition Cube",                2016 },
        { "Observation Record",            2028 },
        { "Old Locator Ring",              2015 },
        { "Old Notepad",                   2029 },
        { "Osprey Badge",                  2012 },
        { "Purified Soil",                 2008 },
        { "Seal Needle",                   2010 },
        { "Vessel Remains",                2026 },

        { "Soil: Arboretum",               2104 },
        { "Soil: Flooded Street",          2100 },
        { "Soil: Sewer Town",              2106 },
        { "Soil: Subway Station",          2105 },
        { "Soil: Twilight Hill",           2103 },
        { "Soil: Underground Parish",      2102 },
        { "Soil: Withered Forest",         2101 },

        { "Arboretum Key",                 2602 },
        { "Cemetary Key",                  2603 },
        { "Console Key",                   2606 },
        { "Hideout Key",                   2604 },
        { "Inspector's Emblem",            2608 },
        { "Laboratory Key",                2612 },
        { "Reservoir Key",                 2601 },
        { "Train Pass",                    2611 },
        { "Train Ticket",                  2610 },
        { "Ward Key",                      2607 },
    };

    enum ItemCategories
    {
        Item,               // These categories all share the same item IDs. 
        Materials,          // The category determines what section of the
        Goods,              // store page it appears in.
        Weapon,
        Helmet,
        Armour,
        Nucleus,
        Seal,
        Spell,
    }

    private void SetShopItem(ItemCategories itemCategory)
    {
        uint itemID = 0;
        string itemName = "";

        switch (itemCategory)
        {
            case ItemCategories.Weapon:
                itemID = ((KeyValuePair<string, uint>)comboWeapon.SelectedItem).Value + (uint)comboWeaponLevel.SelectedIndex;
                itemName = ((KeyValuePair<string, uint>)comboWeapon.SelectedItem).Key + " +" + (uint)comboWeaponLevel.SelectedIndex;
                break;
            case ItemCategories.Seal:
                itemID = ((KeyValuePair<string, uint>)comboSeal.SelectedItem).Value;
                itemName = ((KeyValuePair<string, uint>)comboSeal.SelectedItem).Key;
                Debug.Print(itemID + "");
                if (itemID > 1009 && itemID < 2000) // These are seals that have upgrade levels, except for the base seal ID 1000, which cannot be upgraded
                {
                    itemID += (uint)comboSealLevel.SelectedIndex;
                    itemName += " +" + (uint)comboSealLevel.SelectedIndex;
                }
                Debug.Print(itemID + "");
                break;
            case ItemCategories.Helmet:
                itemID = ((KeyValuePair<string, uint>)comboHelmet.SelectedItem).Value;
                itemName = ((KeyValuePair<string, uint>)comboHelmet.SelectedItem).Key;
                break;
            case ItemCategories.Armour:
                itemID = ((KeyValuePair<string, uint>)comboArmour.SelectedItem).Value;
                itemName = ((KeyValuePair<string, uint>)comboArmour.SelectedItem).Key;
                break;
            case ItemCategories.Spell:
                itemID = ((KeyValuePair<string, uint>)comboSpells.SelectedItem).Value;
                if (itemID > 2000)
                    itemCategory = ItemCategories.Item; // Abilities are under generic item category, but placed with spells in the ui because they are similar enough and saves space
                itemName = ((KeyValuePair<string, uint>)comboSpells.SelectedItem).Key;
                break;
            case ItemCategories.Nucleus:
                itemID = ((KeyValuePair<string, uint>)comboNucleus.SelectedItem).Value;
                itemName = ((KeyValuePair<string, uint>)comboNucleus.SelectedItem).Key;
                break;
            case ItemCategories.Item:
                itemID = ((KeyValuePair<string, uint>)comboGeneral.SelectedItem).Value;
                itemName = ((KeyValuePair<string, uint>)comboGeneral.SelectedItem).Key;
                break;
        }

        bool success = AILimit.SetShopItem(((KeyValuePair<string, uint>)comboShop.SelectedItem).Value, itemID, (uint)itemCategory);

        if (success)
            UpdateStatusBar( itemName + " added to " + ((KeyValuePair<string, uint>)comboShop.SelectedItem).Key);
        else
            MessageBox.Show("There was a problem (I don't know what). Try visiting the store, then trying again.");
    }

    private void SetShopWeapon(object sender, RoutedEventArgs e)
    {
        SetShopItem(ItemCategories.Weapon);
    }

    private void SetShopSeal(object sender, RoutedEventArgs e)
    {
        SetShopItem(ItemCategories.Seal);
    }

    private void SetShopHelmet(object sender, RoutedEventArgs e)
    {
        SetShopItem(ItemCategories.Helmet);
    }

    private void SetShopArmour(object sender, RoutedEventArgs e)
    {
        SetShopItem(ItemCategories.Armour);
    }

    private void SetShopSpell(object sender, RoutedEventArgs e)
    {
        SetShopItem(ItemCategories.Spell);
    }

    private void SetShopNucleus(object sender, RoutedEventArgs e)
    {
        SetShopItem(ItemCategories.Nucleus);
    }

    private void SetShopGeneral(object sender, RoutedEventArgs e)
    {
        SetShopItem(ItemCategories.Item);
    }

    //
    // STATE MONITOR TAB
    // 
    private void ClearStateMonitor(object sender, RoutedEventArgs e)
    {
        textboxStateLog.Clear();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(AILimit.GetCurrentLevelName().ToString());
    }

    static Dictionary<string, uint> destinations = new Dictionary<string, uint>();

    private void LoadDestinationList()
    {
        try
        {
            string line;
            var assembly = Assembly.GetExecutingAssembly();
            string resource = Assembly.GetExecutingAssembly().GetManifestResourceNames().Single(str => str.EndsWith("destinations.tsv"));
            using (StreamReader sr = new StreamReader(assembly.GetManifestResourceStream(resource)))
            {

                while ((line = sr.ReadLine()) != null)
                {
                    string[] split = line.Split("\t");
                    if (split.Length == 2)
                    {
                        destinations.Add(split[0], Convert.ToUInt32(split[1]));
                    }
                }
            }
        }
        catch (Exception e)
        {
            //MessageBox.Show("fail?");
        }

        comboDestinations.ItemsSource = destinations;
        comboDestinations.SelectedIndex = 0;
    }

    private void SetWarpPoint(object sender, RoutedEventArgs e)
    {
        KeyValuePair<string, uint> selectedDestination = (KeyValuePair<string, uint>)comboDestinations.SelectedItem;
        AILimit.SetTransferDestination((int)selectedDestination.Value);
        UpdateStatusBar("Broken branch destination set to " + selectedDestination.Value);
    }

    private void textboxLockTargetHP_Copy_TextChanged(object sender, TextChangedEventArgs e)
    {

    }

    List<TeleportDestination> teleportDestinations = new List<TeleportDestination>();

    private void LoadTeleportDestinations()
    {
        //string resource = Assembly.GetExecutingAssembly().GetManifestResourceNames().Single(str => str.EndsWith("teleports.tsv"));

        LoadTeleportDestinations(true);

        string file = System.AppDomain.CurrentDomain.BaseDirectory + "teleports.dat";
        if (File.Exists(file))
            LoadTeleportDestinations(false, file);
    }

    private void LoadTeleportDestinations(bool useDefault, string sourceFile = "")
    {
        try
        {
            string line = "";
            int level = 0;

            Stream sourceStream;

            if (useDefault)
            {
                sourceFile = Assembly.GetExecutingAssembly().GetManifestResourceNames().Single(str => str.EndsWith("teleports.tsv"));
                sourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(sourceFile);
            }
            else
            {
                sourceStream = File.OpenRead(sourceFile);
            }


            using (StreamReader sr = new StreamReader(sourceStream))
            {
                while ((line = sr.ReadLine()) != null)
                {
                    Debug.Print(line);
                    if (line[0] == '!')
                    {
                        level = Convert.ToInt32(line.Substring(1));
                    }
                    else if (line[0] == '&')
                    {
                        // Do nothing. Potential future use.
                    }
                    else
                    {
                        TeleportDestination td = new TeleportDestination();
                        string[] split = line.Split("\t");

                        if (split.Length == 5 && level != 0)
                        {
                            td.Level = level;
                            td.Description = split[0];
                            td.x = Convert.ToDouble(split[1]);
                            td.y = Convert.ToDouble(split[2]);
                            td.z = Convert.ToDouble(split[3]);
                            td.IsDefault = (split[4] == "1") ? true : false;

                            teleportDestinations.Add(td);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.Print("Error: " + e.ToString());
        }

        listboxTeleportDestinations.ItemsSource = teleportDestinations;

        FilterTeleportDestinations();
    }

    private void SaveTeleportDestinations()
    {
        string file = System.AppDomain.CurrentDomain.BaseDirectory + "teleports.dat";
        try
        {
            using (StreamWriter sw = new StreamWriter(file))
            {
                List<TeleportDestination> sortedList = teleportDestinations.OrderBy(o => o.Level).ToList();
                int level = 0;

                foreach (TeleportDestination destination in sortedList)
                {
                    if (!destination.IsDefault)
                    {
                        if (level != destination.Level)
                        {
                            level = destination.Level;
                            sw.WriteLine("!" + destination.Level);
                        }
                        sw.WriteLine(destination.Description + "\t" + destination.x + "\t" + destination.y + "\t" + destination.z + "\t" + "0");
                    }
                }
            }
        }
        catch (Exception e)
        {
            MessageBox.Show("Problem saving teleports file to " + file);
        }
    }

    private void FilterTeleportDestinations()
    {
        ICollectionView view = CollectionViewSource.GetDefaultView(teleportDestinations);
        view.Filter = (entry) =>
        {
            TeleportDestination destination = (TeleportDestination)entry;
            return destination.Level == AILimit.GetCurrentLevelID();
        };
    }

    class TeleportDestination
    {
        public int Level { get; set; }
        public string Description { get; set; }
        public double x { get; set; }
        public double y { get; set; }
        public double z { get; set; }
        public bool IsDefault { get; set; }
        public string DisplayName {
            get
            {
                return Description + " (x" + x.ToString("N2") + " y" + y.ToString("N2") + " z" + z.ToString("N2") + ")";
            }
        }

    }

    private void SetState(object sender, RoutedEventArgs e)
    {
        StateTypes stateType = comboStateType.SelectedIndex == 0 ? StateTypes.GameState : StateTypes.MonsterState;
        int levelID = 0;

        if (stateType == StateTypes.MonsterState)
        {
            levelID = Convert.ToInt32(comboboxStateLevel.Text);
        }

        int newValue = Convert.ToInt32(textboxStateValue.Text);

        if (newValue < 0)
            newValue = 0;

        if (AILimit.SetState(Convert.ToUInt32(textboxStateID.Text), stateType, newValue, levelID) == -1)
        {
            UpdateStatusBar(((stateType == StateTypes.GameState) ? "Game" : "Monster") +
                        " state change " + textboxStateID.Text + " to " + textboxStateValue.Text + " failed");
        }
        else
        {
            UpdateStatusBar("Set " + ((stateType == StateTypes.GameState) ? "game" : "monster") +
                        " state " + textboxStateID.Text + " to " + textboxStateValue.Text);
        }
    }

    private void TextBox_UpdateStateDisplay(object sender, RoutedEventArgs e)
    {
        try
        {
            int returnValue = AILimit.SetState(Convert.ToUInt32(textboxStateID.Text),
                                                (comboStateType.SelectedIndex == 0 ? StateTypes.GameState : StateTypes.MonsterState),
                                                levelID: Convert.ToInt32(comboboxStateLevel.Text));

            if (returnValue == -1)
                textboxStateValue.Text = "";
            else
                textboxStateValue.Text = returnValue.ToString();
        }
        catch (Exception ex)
        {
            textboxStateID.Text = "";
        }
    }

    private void UpdateStatusBar(string newText)
    {
        textLastCommand.Text = newText;
    }
    
    enum SaveData
    {
        LockHPValue,
        PlayerSpeedValue,
        TargetState,
        TargetDefense,
        TargetResist,
    }

    private void SaveSettings()
    {
        using (StreamWriter sw = new StreamWriter(System.AppDomain.CurrentDomain.BaseDirectory + "settings.dat"))
        {
            // Player Tab
            sw.WriteLine("LockHPValue\t" + textboxLockTargetHP.Text.ToString());
            sw.WriteLine("PlayerSpeedValue\t" + textboxMovementSpeed.Text.ToString());

            // Target Display Stuff
            sw.WriteLine("TargetDisplayPosition\t" + targetDisplay.Left + "\t" + targetDisplay.Top);
            sw.WriteLine("TargetState\t" + ((bool)checkboxTargetState.IsChecked ? "1" : "0"));
            sw.WriteLine("TargetDefense\t" + ((bool)checkboxTargetDefense.IsChecked ? "1" : "0"));
            sw.WriteLine("TargetStatusResist\t" + ((bool)checkboxTargetStatusResist.IsChecked ? "1" : "0"));

            // Stats tab
            sw.WriteLine("CalculateLevel\t" + ((bool)checkboxCalculatePlayerLevel.IsChecked ? "1" : "0"));
            sw.WriteLine("AddCrystalsValue\t" + textboxAddCrystals.Text.ToString());

            // Teleport Tab
            sw.WriteLine("QuickPosition\t" + savedPlayerPosition.x + "\t" + savedPlayerPosition.y + "\t" + savedPlayerPosition.z);

            
        }
    }

    private void LoadSettings()
    {
        try
        {
            string file = System.AppDomain.CurrentDomain.BaseDirectory + "settings.dat";

            if (!File.Exists(file))
                return;

            using (StreamReader sr = new StreamReader(file))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] split = line.Split("\t");
                    if (split.Length > 1)
                    {
                        switch (split[0])
                        {
                            case "LockHPValue":
                                textboxLockTargetHP.Text = split[1];
                                break;
                            case "PlayerSpeedValue":
                                textboxMovementSpeed.Text = split[1];
                                break;

                            case "TargetDisplayPosition":
                                targetDisplay.Left = double.Parse(split[1]);
                                targetDisplay.Top = double.Parse(split[2]);
                                break;
                            case "TargetState":
                                checkboxTargetState.IsChecked = (split[1] == "1") ? true : false;
                                break;
                            case "TargetDefense":
                                checkboxTargetDefense.IsChecked = (split[1] == "1") ? true : false;
                                break;
                            case "TargetStatusResist":
                                checkboxTargetStatusResist.IsChecked = (split[1] == "1") ? true : false;
                                break;

                            case "CalculateLevel":
                                checkboxCalculatePlayerLevel.IsChecked = (split[1] == "1") ? true : false;
                                break;

                            case "AddCrystalsValue":
                                textboxAddCrystals.Text = split[1];
                                break;

                            case "QuickPosition":
                                QuicksavePlayerPosition(double.Parse(split[1]), double.Parse(split[2]), double.Parse(split[3]), false);
                                break;
                        }
                    }
                }
            }
        }
        catch (Exception f)
        {
            MessageBox.Show("Problem loading settings file.");
        }
    }

    private void TargetDisplayOptionClicked(object sender, RoutedEventArgs e)
    {
        ResizeTargetDisplayWindow();
    }

    private void ResizeTargetDisplayWindow()
    {
        double size = 0;
        const double lineSize = 15.8;
        int count = -1;
        try
        {
            if ((bool)checkboxTargetState.IsChecked)
            {
                size += lineSize * 1;
                count++;
            }
            if ((bool)checkboxTargetDefense.IsChecked)
            {
                size += lineSize * 4;
                count++;
            }
            if ((bool)checkboxTargetStatusResist.IsChecked)
            {
                size += lineSize * 3;
                count++;
            }

            if (count == -1)
                count = 0;


            size += count * lineSize + 2;

            targetDisplay.ResizeWindow(size);
        }
        catch (Exception f)
        { }
    }

    private Dictionary<string, GameOptions> hotkeyNameList = new Dictionary<string, GameOptions>()
    {
        { "Player Immortal",        GameOptions.Immortal },
        { "Lock Target HP",         GameOptions.LockTargetHP },
        { "Lock Sync",              GameOptions.LockSync },
        { "Player Speed",           GameOptions.PlayerSpeed },
        { "Show Target Info",       GameOptions.TargetInfo },
        { "Infinite Dew",           GameOptions.InfiniteDew },
        { "Add Crystals",           GameOptions.AddCrystals },
        { "Teleport Qucksave",      GameOptions.TeleportQucksave },
        { "Teleport Quickload",     GameOptions.TeleportQuickload },
        { "Next Tab",               GameOptions.NextTab }
    };

    /* not implemented yet    
    private void LabelHotkeyButtons()
    {

    }

    
    private void SetHotkey(object sender, RoutedEventArgs e)
    {
        Button button = sender as Button;
        KeyValuePair<string, GameOptions> paira = (KeyValuePair<string, GameOptions>)button.DataContext;
        MessageBox.Show(paira.Key.ToString());

        foreach (KeyValuePair<string, GameOptions> pair in listboxHotkeys.Items)
        {

        }

    }*/
}