using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace AILimitTool
{
    /// <summary>
    /// Interaction logic for TargetDisplay.xaml
    /// </summary>
    public partial class TargetDisplay : Window
    {
        public bool shouldClose = false;
        private bool defaultValues = true;

        public TargetDisplay()
        {
            InitializeComponent();
            Closing += WindowClosing;
        }

        private void WindowClosing(object sender, CancelEventArgs e)
        {
            if (!shouldClose)
            {
                e.Cancel = true; // Cancel the closing action
                this.Hide();
            }
        }

        public enum MonsterStats
        {
            HP,
            HPMax,
            HPPercent,
            Sync,
            PoisonAccumulation,
            PiercingAccumulation,
            InfectionAccumulation,

            HurtFlagTimer,
            TenacityDecreaseTick,
            Tenacity,
            TenacityMax,
        }

        public void UpdateDisplay(MonsterStats stat, double value, double valueMax = 0, double valueTimer = 0)
        {
            if (defaultValues)
                defaultValues = false;

            switch (stat)
            {
                case MonsterStats.HP:
                    textHP.Text = value.ToString("F0") + "/" + valueMax.ToString("F0");
                    barHP.Value = value;
                    barHP.Maximum = valueMax;
                    break;
                case MonsterStats.Tenacity:
                    if (valueTimer < 0)
                        valueTimer = 0;
                    textTenacity.Text = "(Reset: " + valueTimer.ToString("F1") + "s) " + value.ToString("F1") + "/" + valueMax.ToString("F1");
                    barTenacity.Value = value;
                    barTenacity.Maximum = valueMax;
                    break;
                case MonsterStats.Sync:
                    textSync.Text = value.ToString("F1") + "%";
                    barSync.Value = value;
                    break;
                case MonsterStats.PoisonAccumulation:
                    textPoison.Text = value.ToString("F1") + "%";
                    barPoison.Value = value;
                    break;
                case MonsterStats.PiercingAccumulation:
                    textPiercing.Text = value.ToString("F1") + "%";
                    barPiercing.Value = value;
                    break;
                case MonsterStats.InfectionAccumulation:
                    textInfection.Text = value.ToString("F1") + "%";
                    barInfection.Value = value;
                    break;
            }
        }

        public void UpdateDisplayText(int lines, string headers, string values)
        {
            textHeaders.Text = headers;
            textValues.Text = values;
        }
    }
}
