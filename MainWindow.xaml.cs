using System.Collections.ObjectModel;
using System.ComponentModel;
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

        AILimit = new AILimitLink();

        InitialSetup();
        LoadSettings();
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

    //
    // UI stuff
    //
    private void uiTimer_Tick(object sender, EventArgs e)
    {
        if (!AILimit.linkActive)
        {
            this.Title = "AI Limit Tool - game not found";
            textError.Text = "Game not found. Attempting to find AI Limit process. If the game is open, please try restarting.";
            DisableTabs(true);
        }
        else if (!AILimit.mainObjectsFound)
        {
            this.Title = "AI Limit Tool - searching for offsets";
            textError.Text = "AI Limit process found. Attempting to find offsets. This step cannot be completed until in the game itself.";
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
            {
                UpdateTeleportTab();


            }
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
                    "Electric Defense:\n" +
                    "Psycho Defense:\n" +
                    "Dimension Defense:\n\n";

            values += AILimit.SetTargetMonsterValue(MonsterStats.PhysicalDefense, true).ToString("P1") + "\n";
            values += AILimit.SetTargetMonsterValue(MonsterStats.ElectricDefense, true).ToString("P1") + "\n";
            values += AILimit.SetTargetMonsterValue(MonsterStats.PsychoDefense, true).ToString("P1") + "\n";
            values += AILimit.SetTargetMonsterValue(MonsterStats.DimensionDefense, true).ToString("P1") + "\n\n";
        }

        if ((bool)checkboxTargetStatusResist.IsChecked)
        {
            headers += "Poison Resist:\n" +
                    "Piercing Resist:\n" +
                    "Infect Resist:\n\n";

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
            tabStates.IsEnabled = false;
        }
        else
        {
            tabError.Visibility = Visibility.Hidden;
            tabcontrolMain.SelectedIndex = 0;
            tabMain.IsEnabled = true;
            tabStats.IsEnabled = true;
            tabTeleport.IsEnabled = true;
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

            if (tab == 4)
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
        States,
        Error,
    }

    private void InitialSetup()
    {
        LoadDestinationList();
        LoadTeleportDestinations();

        tabError.Visibility = Visibility.Hidden;

        hotkeyManager = new HotkeyManager(this);
        UpdateUIHotkeyText();

        uiTimer.Tick += uiTimer_Tick;
        uiTimer.Interval = TimeSpan.FromMilliseconds(100);
        uiTimer.Start();
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
    // Buton handlers
    //

    private void buttonSetStats_Click(object sender, RoutedEventArgs e)
    {

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
        AILimit.InfiniteDew((bool)checkboxInfiniteDew.IsChecked);
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
        try
        {
            string line = "";
            int level = 0;

 
            using (StreamReader sr = new StreamReader("D:\\teleports2.txt"))
            {
                while ((line = sr.ReadLine()) != null)
                {


                    if (line[0] == '!')
                    {
                        level = Convert.ToInt32(line.Substring(1));
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
        {  }

        listboxTeleportDestinations.ItemsSource = teleportDestinations;

        FilterTeleportDestinations();
    }

    private void SaveTeleportDestinations()
    {
        using (StreamWriter sw = new StreamWriter("D:\\teleports2.txt"))
        {
            List<TeleportDestination> sortedList = teleportDestinations.OrderBy(o => o.Level).ToList();
            int level = 0;

            foreach (TeleportDestination destination in sortedList)
            {
                if (level != destination.Level)
                {
                    level = destination.Level;
                    sw.WriteLine("!" + destination.Level);
                }
                sw.WriteLine(destination.Description + "\t" + destination.x + "\t" + destination.y + "\t" + destination.z + "\t" + ((destination.IsDefault == true) ? "1" : "0"));
            }
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
        StateTypes stateType = comboboxStateType.SelectedIndex == 0 ? StateTypes.GameState : StateTypes.MonsterState;
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
                                                (comboboxStateType.SelectedIndex == 0 ? StateTypes.GameState : StateTypes.MonsterState),
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
            using (StreamReader sr = new StreamReader(System.AppDomain.CurrentDomain.BaseDirectory + "settings.dat"))
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
}